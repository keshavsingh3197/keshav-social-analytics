using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using SocialAnalytics.Api.Models;

namespace SocialAnalytics.Api.Services;

/// <summary>
/// AnimStudio-style per-user YouTube OAuth 2.0 integration with PKCE and encrypted token storage.
/// Secrets are resolved dynamically from Admin Integration Settings or server configuration/env.
/// </summary>
public sealed class YouTubeOAuthService
{
    private const string ConnectionCollection = "user_youtube_connections";
    private const string AuthorizeEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
    private const string TokenEndpoint = "https://oauth2.googleapis.com/token";
    private const string RevokeEndpoint = "https://oauth2.googleapis.com/revoke";
    private const string ReadOnlyScope = "https://www.googleapis.com/auth/youtube.readonly";

    private sealed record PendingOAuthState(string UserId, string CodeVerifier, string ReturnPath, string RedirectUri, DateTime ExpiresAtUtc);

    private readonly ConcurrentDictionary<string, PendingOAuthState> _pending = new(StringComparer.Ordinal);
    private readonly HybridDataStore _store;
    private readonly SocialConfigService _configService;
    private readonly AnalyticsEngineService _analyticsService;
    private readonly YouTubeLiveFetchService _ytFetch;
    private readonly IHttpClientFactory _httpFactory;
    private readonly IDataProtector _protector;
    private readonly ILogger<YouTubeOAuthService> _logger;

    public YouTubeOAuthService(
        HybridDataStore store,
        SocialConfigService configService,
        AnalyticsEngineService analyticsService,
        YouTubeLiveFetchService ytFetch,
        IHttpClientFactory httpFactory,
        IDataProtectionProvider dataProtectionProvider,
        ILogger<YouTubeOAuthService> logger)
    {
        _store = store;
        _configService = configService;
        _analyticsService = analyticsService;
        _ytFetch = ytFetch;
        _httpFactory = httpFactory;
        _protector = dataProtectionProvider.CreateProtector("SocialAnalytics.YouTubeOAuthTokens.v1");
        _logger = logger;
    }

    public async Task<YouTubeOAuthConfigPublicView> GetPublicConfigAsync(string? originBaseUrl)
    {
        var apiKey = await _configService.GetEffectiveYouTubeApiKeyAsync();
        var (clientId, clientSecret, redirectUri) = await _configService.GetEffectiveGoogleOAuthAsync();
        var effectiveRedirect = ResolveRedirectUri(redirectUri, originBaseUrl);
        return new YouTubeOAuthConfigPublicView(
            IsOAuthConfigured: !string.IsNullOrWhiteSpace(clientId) && !string.IsNullOrWhiteSpace(clientSecret),
            IsApiKeyConfigured: !string.IsNullOrWhiteSpace(apiKey),
            RedirectUriHint: effectiveRedirect);
    }

    public async Task<List<YouTubeConnectionView>> ListConnectionsForUserAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId)) return [];
        var all = await _store.GetListAsync<UserYouTubeConnection>(ConnectionCollection, () => []);
        return all
            .Where(c => string.Equals(c.UserId, userId, StringComparison.Ordinal))
            .OrderByDescending(c => c.ConnectedAtUtc)
            .Select(ToView)
            .ToList();
    }

    public async Task<string> BeginConnectAsync(string userId, string? returnPath, string? originBaseUrl)
    {
        var (clientId, clientSecret, configuredRedirect) = await _configService.GetEffectiveGoogleOAuthAsync();
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
        {
            throw new InvalidOperationException(
                "YouTube OAuth is not configured yet. Ask an Admin to add Google Client ID and Client Secret in Integrations settings.");
        }

        PrunePending();
        if (_pending.Count >= 1000)
            throw new InvalidOperationException("Too many OAuth sign-ins in progress. Please try again in a minute.");

        var state = Base64Url(RandomNumberGenerator.GetBytes(32));
        var verifier = Base64Url(RandomNumberGenerator.GetBytes(48));
        var challenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        var redirectUri = ResolveRedirectUri(configuredRedirect, originBaseUrl);

        _pending[state] = new PendingOAuthState(
            userId,
            verifier,
            SafeReturnPath(returnPath),
            redirectUri,
            DateTime.UtcNow.AddMinutes(10));

        var query = new Dictionary<string, string>
        {
            ["client_id"] = clientId.Trim(),
            ["redirect_uri"] = redirectUri,
            ["response_type"] = "code",
            ["scope"] = ReadOnlyScope,
            ["access_type"] = "offline",
            ["prompt"] = "consent select_account",
            ["include_granted_scopes"] = "false",
            ["state"] = state,
            ["code_challenge"] = challenge,
            ["code_challenge_method"] = "S256"
        };

        return AuthorizeEndpoint + "?" + string.Join("&", query.Select(p => $"{p.Key}={Uri.EscapeDataString(p.Value)}"));
    }

    public async Task<YouTubeOAuthCompleteResponse> CompleteConnectAsync(
        string userId,
        bool isAdmin,
        string? code,
        string? state,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(state) ||
            !_pending.TryRemove(state, out var started) ||
            started.ExpiresAtUtc < DateTime.UtcNow ||
            !CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(started.UserId),
                Encoding.UTF8.GetBytes(userId)))
        {
            throw new InvalidOperationException("OAuth sign-in state expired, invalid, or belongs to a different user.");
        }

        if (string.IsNullOrWhiteSpace(code))
            throw new InvalidOperationException("Google did not return an authorization code.");

        var (clientId, clientSecret, _) = await _configService.GetEffectiveGoogleOAuthAsync();
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
            throw new InvalidOperationException("YouTube OAuth client credentials are missing on the server.");

        var tokenPayload = await ExchangeTokenAsync(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code.Trim(),
            ["code_verifier"] = started.CodeVerifier,
            ["client_id"] = clientId.Trim(),
            ["client_secret"] = clientSecret.Trim(),
            ["redirect_uri"] = started.RedirectUri
        }, ct);

        var channel = new ChannelMetrics
        {
            Platform = SocialPlatform.YouTube,
            OwnerUserId = userId,
            IsAdminOwned = isAdmin,
            RpmUsd = 3.2
        };

        if (!await _ytFetch.EnrichWithOAuthTokenAsync(channel, tokenPayload.AccessToken, ct) ||
            string.IsNullOrWhiteSpace(channel.ExternalChannelId))
        {
            throw new InvalidOperationException("Could not read a YouTube channel from that Google account.");
        }

        var connId = UserYouTubeConnection.IdFor(userId, channel.ExternalChannelId);
        var protectedRefresh = string.IsNullOrWhiteSpace(tokenPayload.RefreshToken)
            ? string.Empty
            : _protector.Protect(tokenPayload.RefreshToken);

        var savedConn = new UserYouTubeConnection
        {
            Id = connId,
            UserId = userId,
            ChannelId = channel.ExternalChannelId,
            ChannelTitle = channel.ChannelName,
            ChannelHandle = channel.ChannelHandle,
            AvatarUrl = channel.AvatarUrl,
            ProtectedRefreshToken = protectedRefresh,
            Scopes = tokenPayload.Scope,
            ConnectedAtUtc = DateTime.UtcNow,
            LastSyncedAtUtc = DateTime.UtcNow,
            RevokedAtUtc = null
        };

        await _store.UpsertItemAsync<UserYouTubeConnection>(ConnectionCollection, savedConn.Id, c => c.Id, savedConn, () => []);
        var savedChannel = await _analyticsService.UpsertUserChannelAsync(userId, isAdmin, channel) ?? channel;

        _logger.LogInformation("Connected YouTube OAuth channel {ChannelId} for user {UserId}", channel.ExternalChannelId, userId);
        return new YouTubeOAuthCompleteResponse(ToView(savedConn), savedChannel, started.ReturnPath);
    }

    public async Task<ChannelMetrics?> SyncConnectionAsync(string userId, bool isAdmin, string channelId, CancellationToken ct = default)
    {
        var connections = await _store.GetListAsync<UserYouTubeConnection>(ConnectionCollection, () => []);
        var conn = connections.FirstOrDefault(c =>
            string.Equals(c.UserId, userId, StringComparison.Ordinal) &&
            string.Equals(c.ChannelId, channelId, StringComparison.OrdinalIgnoreCase));

        var userChannels = await _analyticsService.GetChannelsForUserAsync(userId, isAdmin, includeInactive: true);
        var target = userChannels.FirstOrDefault(c => c.ExternalChannelId.Equals(channelId, StringComparison.OrdinalIgnoreCase))
            ?? new ChannelMetrics
            {
                ExternalChannelId = channelId,
                ChannelHandle = conn?.ChannelHandle ?? channelId,
                ChannelName = conn?.ChannelTitle ?? channelId,
                Platform = SocialPlatform.YouTube,
                OwnerUserId = userId,
                IsAdminOwned = isAdmin,
                RpmUsd = 3.2
            };

        var synced = false;
        if (conn is not null && !string.IsNullOrWhiteSpace(conn.ProtectedRefreshToken))
        {
            try
            {
                var (clientId, clientSecret, _) = await _configService.GetEffectiveGoogleOAuthAsync();
                if (!string.IsNullOrWhiteSpace(clientId) && !string.IsNullOrWhiteSpace(clientSecret))
                {
                    var refreshToken = _protector.Unprotect(conn.ProtectedRefreshToken);
                    var token = await ExchangeTokenAsync(new Dictionary<string, string>
                    {
                        ["grant_type"] = "refresh_token",
                        ["refresh_token"] = refreshToken,
                        ["client_id"] = clientId.Trim(),
                        ["client_secret"] = clientSecret.Trim()
                    }, ct);

                    synced = await _ytFetch.EnrichWithOAuthTokenAsync(target, token.AccessToken, ct);
                    if (synced)
                    {
                        conn.LastSyncedAtUtc = DateTime.UtcNow;
                        conn.ChannelTitle = target.ChannelName;
                        conn.ChannelHandle = target.ChannelHandle;
                        conn.AvatarUrl = target.AvatarUrl;
                        await _store.UpsertItemAsync<UserYouTubeConnection>(ConnectionCollection, conn.Id, c => c.Id, conn, () => []);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "OAuth token refresh failed for user {UserId}, falling back to public lookup.", userId);
                if (conn is not null)
                {
                    conn.RevokedAtUtc = DateTime.UtcNow;
                    await _store.UpsertItemAsync<UserYouTubeConnection>(ConnectionCollection, conn.Id, c => c.Id, conn, () => []);
                }
            }
        }

        if (!synced)
        {
            var apiKey = await _configService.GetEffectiveYouTubeApiKeyAsync();
            synced = await _ytFetch.EnrichChannelAsync(target, apiKey, ct);
        }

        if (!synced) return null;
        return await _analyticsService.UpsertUserChannelAsync(userId, isAdmin, target);
    }

    public async Task<bool> DisconnectAsync(string userId, string channelId, CancellationToken ct = default)
    {
        var connId = UserYouTubeConnection.IdFor(userId, channelId);
        var connections = await _store.GetListAsync<UserYouTubeConnection>(ConnectionCollection, () => []);
        var conn = connections.FirstOrDefault(c => c.Id == connId);
        if (conn is null) return false;

        if (!string.IsNullOrWhiteSpace(conn.ProtectedRefreshToken))
        {
            try
            {
                var token = _protector.Unprotect(conn.ProtectedRefreshToken);
                var http = _httpFactory.CreateClient("LiveYouTube");
                using var content = new FormUrlEncodedContent(new Dictionary<string, string> { ["token"] = token });
                await http.PostAsync(RevokeEndpoint, content, ct);
            }
            catch
            {
                // Best effort token revocation
            }
        }

        return await _store.DeleteItemAsync<UserYouTubeConnection>(ConnectionCollection, connId, c => c.Id, () => []);
    }

    private sealed record GoogleTokenPayload(string AccessToken, string? RefreshToken, string Scope);

    private async Task<GoogleTokenPayload> ExchangeTokenAsync(Dictionary<string, string> form, CancellationToken ct)
    {
        var http = _httpFactory.CreateClient("LiveYouTube");
        using var content = new FormUrlEncodedContent(form);
        using var resp = await http.PostAsync(TokenEndpoint, content, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);

        if (!resp.IsSuccessStatusCode)
        {
            _logger.LogWarning("Google OAuth token endpoint returned {Status}", (int)resp.StatusCode);
            throw new InvalidOperationException("Google refused the authorization code or refresh token. Please try connecting again.");
        }

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        var access = root.TryGetProperty("access_token", out var at) ? at.GetString() ?? "" : "";
        if (string.IsNullOrWhiteSpace(access))
            throw new InvalidOperationException("Google did not issue an access token.");

        var refresh = root.TryGetProperty("refresh_token", out var rt) ? rt.GetString() : null;
        var scope = root.TryGetProperty("scope", out var sc) ? sc.GetString() ?? ReadOnlyScope : ReadOnlyScope;
        return new GoogleTokenPayload(access, refresh, scope);
    }

    private static YouTubeConnectionView ToView(UserYouTubeConnection c) => new(
        c.ChannelId,
        c.ChannelTitle,
        c.ChannelHandle,
        c.AvatarUrl,
        c.ConnectedAtUtc,
        c.LastSyncedAtUtc,
        NeedsReconnect: c.RevokedAtUtc is not null || string.IsNullOrWhiteSpace(c.ProtectedRefreshToken));

    private static string ResolveRedirectUri(string? configured, string? originBaseUrl)
    {
        if (!string.IsNullOrWhiteSpace(configured)) return configured.Trim();
        var origin = !string.IsNullOrWhiteSpace(originBaseUrl) ? originBaseUrl.TrimEnd('/') : "https://social.keshavsingh.in";
        return $"{origin}/youtube/callback";
    }

    private static string SafeReturnPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return "/dashboard";
        var trimmed = path.Trim();
        return trimmed.StartsWith('/') && !trimmed.StartsWith("//") ? trimmed : "/dashboard";
    }

    private void PrunePending()
    {
        var now = DateTime.UtcNow;
        foreach (var kv in _pending)
        {
            if (kv.Value.ExpiresAtUtc < now) _pending.TryRemove(kv.Key, out _);
        }
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
