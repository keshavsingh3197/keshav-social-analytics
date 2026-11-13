namespace SocialAnalytics.Api.Models;

public class UserYouTubeConnection
{
    public string Id { get; set; } = string.Empty; // "{userId}:{channelId}"
    public string UserId { get; set; } = string.Empty;
    public string ChannelId { get; set; } = string.Empty;
    public string ChannelTitle { get; set; } = string.Empty;
    public string? ChannelHandle { get; set; }
    public string? AvatarUrl { get; set; }
    public string ProtectedRefreshToken { get; set; } = string.Empty;
    public string Scopes { get; set; } = string.Empty;
    public DateTime ConnectedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastSyncedAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }

    public static string IdFor(string userId, string channelId) => $"{userId}:{channelId}";
}

public record YouTubeConnectionView(
    string ChannelId,
    string ChannelTitle,
    string? ChannelHandle,
    string? AvatarUrl,
    DateTime ConnectedAtUtc,
    DateTime? LastSyncedAtUtc,
    bool NeedsReconnect);

public record YouTubeOAuthConfigPublicView(
    bool IsOAuthConfigured,
    bool IsApiKeyConfigured,
    string RedirectUriHint);

public record YouTubeOAuthStatusResponse(
    bool IsOAuthConfigured,
    List<YouTubeConnectionView> Connections);

public record YouTubeOAuthStartRequest(string? ReturnPath);

public record YouTubeOAuthStartResponse(string Url);

public record YouTubeOAuthCompleteRequest(string? Code, string? State);

public record YouTubeOAuthCompleteResponse(
    YouTubeConnectionView Connection,
    ChannelMetrics Channel,
    string ReturnPath);
