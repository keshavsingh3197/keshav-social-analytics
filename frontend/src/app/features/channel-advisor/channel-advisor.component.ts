import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { AnalyticsService } from '../../core/services/analytics.service';
import { AuthService } from '../../core/services/auth.service';
import { EcosystemConfigService } from '../../core/services/ecosystem-config.service';
import { AdSlotComponent } from '../shared/ad-slot/ad-slot.component';
import { LoadingStateComponent } from '../shared/loading/loading-state.component';
import {
  AiPromptTemplate,
  ChannelAuditResult,
  ChannelIssueItem,
  ChannelMetrics,
  SocialPlatform,
  VideoAuditItem
} from '../../core/models/analytics.models';

@Component({
  selector: 'app-channel-advisor',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, AdSlotComponent, LoadingStateComponent],
  template: `
    <div class="advisor-page">
      <div class="header-box">
        <div class="header-top">
          <div>
            <h1>🚀 Growth Advisor & Channel Fix Studio</h1>
            <p>
              Deep-audit any YouTube or social channel: spot exact algorithmic & monetization issues,
              optimize video titles & descriptions live, and copy custom AI prompts pre-loaded with your metrics.
            </p>
          </div>
          @if (auditResult(); as audit) {
            <div class="header-cta-group">
              @if (auth.isAuthenticated() && audit.snapshot) {
                <button type="button" class="btn-outline" (click)="saveCurrentToWorkspace()" [disabled]="savingChannel()">
                  @if (savingChannel()) { <span class="btn-spin" aria-hidden="true"></span>Saving... } @else { + Save to My Channels }
                </button>
              }
              <a routerLink="/funnel" class="btn-outline">🔗 Build Funnel Link</a>
              @if (studioPortalUrl(); as studioUrl) {
                <a [href]="studioUrl + '/automation'" target="_blank" rel="noopener" class="btn-outline">⚡ Auto-Post Studio →</a>
              }
            </div>
          }
        </div>
        @if (statusBanner()) {
          <div class="status-banner" [class.error]="statusIsError()">{{ statusBanner() }}</div>
        }
      </div>

      <!-- Channel Search / Selection Bar -->
      <div class="search-bar-card">
        <div class="input-wrap">
          <label>Select Connected Channel or Audit Any Public &#64;Handle / Channel Name</label>
          <div class="input-action-row">
            @if (channels().length > 0) {
              <select (change)="selectConnectedChannel($event)" class="form-select">
                <option value="">-- Connected Workspace Channels --</option>
                @for (ch of channels(); track ch.id) {
                  <option [value]="ch.id" [selected]="ch.channelHandle.toLowerCase() === selectedHandle().toLowerCase()">
                    {{ ch.channelName }} ({{ ch.platform }})
                  </option>
                }
              </select>
            }
            <input
              type="text"
              [ngModel]="selectedHandle()"
              (ngModelChange)="selectedHandle.set($event)"
              (keyup.enter)="runAudit(true)"
              placeholder="@omkarshivbhakti or channel URL"
              class="form-input" />
            <select [ngModel]="selectedPlatform()" (ngModelChange)="selectedPlatform.set($event)" class="form-select">
              <option value="YouTube">YouTube</option>
              <option value="Instagram">Instagram</option>
              <option value="Facebook">Facebook</option>
              <option value="TikTok">TikTok</option>
              <option value="LinkedIn">LinkedIn</option>
            </select>
            <button type="button" (click)="runAudit(true)" [disabled]="auditing()" class="btn-audit">
              @if (auditing()) { <span class="btn-spin" aria-hidden="true"></span>Auditing Live... } @else { 🔍 Run Growth Audit }
            </button>
          </div>
        </div>
      </div>

      @if (!auditResult()) {
        <app-loading-state variant="audit" label="Analyzing channel performance &amp; generating growth plan…" />
      }

      @if (auditResult(); as audit) {
        <!-- Audit Score Overview & Snapshot -->
        <div class="audit-hero-card">
          <div class="hero-main-row">
            <div class="score-circle" [attr.data-grade]="audit.overallGrade || 'B'">
              <span class="score-val">{{ audit.healthScoreOutOf100 }}</span>
              <span class="score-max">/ 100 • {{ audit.overallGrade || healthLabel(audit.healthScoreOutOf100) }}</span>
            </div>
            <div class="audit-summary-text">
              <div class="title-pills">
                <h2>{{ audit.snapshot?.channelName || audit.channelHandle || 'Channel Benchmark' }}</h2>
                @if (audit.snapshot) {
                  <span class="badge-pill live">{{ audit.snapshot.isVerifiedLive ? '● LIVE DATA' : 'PROFILE' }} • {{ audit.snapshot.dataSource }}</span>
                  <span class="badge-pill niche">{{ audit.snapshot.niche }}</span>
                }
              </div>
              <p class="exec-summary">
                {{ audit.executiveSummary || ('Audit for ' + audit.channelHandle + ' computed against engagement and retention targets.') }}
              </p>
              @if (audit.snapshot?.channelUrl) {
                <a [href]="audit.snapshot!.channelUrl" target="_blank" rel="noopener" class="ext-link">
                  Open Channel on {{ audit.snapshot!.platform }} ↗
                </a>
              }
            </div>
          </div>

          @if (audit.snapshot; as snap) {
            <div class="snapshot-kpi-strip">
              <div class="kpi-tile">
                <span>Subscribers</span>
                <strong>{{ snap.followersOrSubscribers > 0 ? (snap.followersOrSubscribers | number) : 'Hidden / 0' }}</strong>
              </div>
              <div class="kpi-tile">
                <span>30-Day Views</span>
                <strong>{{ snap.totalViewsLast30Days | number }}</strong>
              </div>
              <div class="kpi-tile">
                <span>Avg Views / Video</span>
                <strong>{{ snap.avgViewsPerRecentVideo | number }}</strong>
              </div>
              <div class="kpi-tile">
                <span>Shorts / Long Mix</span>
                <strong>{{ snap.shortsCount }} Shorts • {{ snap.longFormCount }} Long</strong>
              </div>
              <div class="kpi-tile">
                <span>Upload Cadence</span>
                <strong>{{ snap.uploadsPerWeek }}/wk • {{ snap.avgTitleLength }}c titles</strong>
              </div>
              <div class="kpi-tile">
                <span>Est. Revenue</span>
                <strong>\${{ snap.estimatedMonthlyRevenue | number:'1.0-0' }}/mo (\${{ snap.estimatedRpmUsd }} RPM)</strong>
              </div>
            </div>
          }
        </div>

        <!-- 5-Pillar Health Breakdown -->
        @if (audit.scorePillars?.length) {
          <div class="section-block">
            <div class="section-head">
              <h2>📊 5-Pillar Channel Health Breakdown</h2>
              <span class="section-sub">Scored from live recent uploads, cadence, metadata & conversion signals</span>
            </div>
            <div class="pillars-grid">
              @for (p of audit.scorePillars; track p.key) {
                <div class="pillar-card">
                  <div class="pillar-top">
                    <span class="pillar-label">{{ p.label }}</span>
                    <span class="pillar-score" [attr.data-status]="p.status.toLowerCase()">{{ p.score }}/100</span>
                  </div>
                  <div class="progress-track">
                    <div class="progress-fill" [style.width.%]="p.score" [attr.data-status]="p.status.toLowerCase()"></div>
                  </div>
                  <p class="pillar-summary">{{ p.summary }}</p>
                </div>
              }
            </div>
          </div>
        }

        <!-- Detected Issues & Direct Fix Actions -->
        @if (audit.detectedIssues?.length) {
          <div class="section-block">
            <div class="section-head">
              <h2>🩺 Detected Channel Issues & Instant Fixes</h2>
              <span class="section-sub">{{ audit.detectedIssues!.length }} actionable items spotted — fix in-app or copy targeted AI prompt</span>
            </div>
            <div class="issues-grid">
              @for (issue of audit.detectedIssues; track issue.id) {
                <div class="issue-card" [attr.data-severity]="issue.severity.toLowerCase()">
                  <div class="issue-top">
                    <span class="sev-badge" [attr.data-severity]="issue.severity.toLowerCase()">{{ issue.severity }}</span>
                    <span class="issue-cat">{{ issue.category }}</span>
                  </div>
                  <h3>{{ issue.title }}</h3>
                  <p class="issue-evidence">{{ issue.evidence }}</p>
                  <div class="issue-impact">Expected Impact: <strong>{{ issue.impactEstimate }}</strong></div>
                  <div class="issue-actions">
                    <button type="button" class="btn-fix" (click)="triggerIssueFix(issue)">
                      🛠️ {{ issue.fixActionLabel }}
                    </button>
                    <button type="button" class="btn-copy" (click)="copyText(issue.fixPrompt, 'issue-' + issue.id)">
                      {{ copiedKey() === 'issue-' + issue.id ? '✓ Prompt Copied!' : '🤖 Copy AI Fix Prompt' }}
                    </button>
                  </div>
                </div>
              }
            </div>
          </div>
        }

        <!-- Quick-Fix Metadata, Mobile Title Preview & Funnel CTA Studio -->
        <div id="fix-studio" class="section-block studio-card">
          <div class="section-head">
            <h2>🛠️ Quick-Fix Packaging & Funnel Studio</h2>
            <span class="section-sub">Rewrite weak video titles with mobile 55-char safety check and copy funnel-ready description & pinned comment blocks</span>
          </div>

          <div class="studio-grid">
            <!-- Left: Title Optimizer -->
            <div class="studio-pane">
              <div class="pane-head">
                <h3>1. Mobile Title & Hook Rewriter</h3>
                @if (audit.videoAudits?.length) {
                  <select class="compact-select" (change)="pickVideoForFix($event)">
                    @for (v of audit.videoAudits; track v.videoId) {
                      <option [value]="v.videoId" [selected]="v.videoId === activeVideoFix()?.videoId">
                        {{ v.performanceTag }}: {{ v.title.slice(0, 44) }}{{ v.title.length > 44 ? '...' : '' }}
                      </option>
                    }
                  </select>
                }
              </div>

              <label class="field-label">
                Draft Title
                <span class="char-counter" [class.over]="draftTitle().length > 55">
                  {{ draftTitle().length }} / 55 mobile safe chars
                </span>
              </label>
              <input
                type="text"
                [ngModel]="draftTitle()"
                (ngModelChange)="draftTitle.set($event)"
                placeholder="Enter a curiosity-driven title under 55 chars..."
                class="studio-input" />

              <div class="mobile-preview-box">
                <span class="preview-label">📱 Mobile YouTube Feed Preview</span>
                <div class="mobile-title-sim">{{ mobilePreviewTitle() }}</div>
                @if (draftTitle().length > 55) {
                  <small class="warn-hint">⚠️ Words after 55 characters are cut off on mobile browse feeds.</small>
                } @else {
                  <small class="ok-hint">✓ Full hook visible on mobile and desktop!</small>
                }
              </div>

              <div class="studio-btn-row">
                @if (activeVideoFix()?.suggestedTitleRewrite) {
                  <button type="button" class="btn-secondary-sm" (click)="applySuggestedRewrite()">
                    ✨ Load Auto-Draft Title
                  </button>
                }
                <button type="button" class="btn-primary-sm" (click)="copyText(draftTitle(), 'fixed-title')">
                  {{ copiedKey() === 'fixed-title' ? '✓ Title Copied!' : '📋 Copy Fixed Title' }}
                </button>
                @if (activeVideoFix()?.fixPrompt) {
                  <button type="button" class="btn-secondary-sm" (click)="loadPromptText(activeVideoFix()!.fixPrompt, 'Custom Video Fix Prompt')">
                    🤖 Send to AI Prompt Studio
                  </button>
                }
              </div>
            </div>

            <!-- Right: Description & Pinned Comment Fixer -->
            <div class="studio-pane">
              <h3>2. Description Funnel & Pinned Comment</h3>
              <label class="field-label">Description CTA Block (Paste at top of video description)</label>
              <textarea
                rows="4"
                [ngModel]="draftDescription()"
                (ngModelChange)="draftDescription.set($event)"
                class="studio-textarea"></textarea>
              <div class="studio-btn-row mb-sm">
                <button type="button" class="btn-primary-sm" (click)="copyText(draftDescription(), 'fixed-desc')">
                  {{ copiedKey() === 'fixed-desc' ? '✓ Description Copied!' : '📋 Copy Description Block' }}
                </button>
                <a routerLink="/funnel" class="btn-secondary-sm">🔗 Generate Tracked URL</a>
              </div>

              <label class="field-label">Engagement Pinned Comment</label>
              <textarea
                rows="2"
                [ngModel]="draftPinnedComment()"
                (ngModelChange)="draftPinnedComment.set($event)"
                class="studio-textarea"></textarea>
              <div class="studio-btn-row">
                <button type="button" class="btn-primary-sm" (click)="copyText(draftPinnedComment(), 'fixed-comment')">
                  {{ copiedKey() === 'fixed-comment' ? '✓ Comment Copied!' : '📋 Copy Pinned Comment' }}
                </button>
              </div>
            </div>
          </div>
        </div>

        <!-- Per-Video Performance & SEO Audit Table -->
        @if (audit.videoAudits?.length) {
          <div class="section-block">
            <div class="section-head">
              <h2>🎬 Recent Uploads Diagnostic Audit</h2>
              <span class="section-sub">Click "Fix Title" or "Prompt" on any video to optimize underperforming packaging</span>
            </div>
            <div class="table-card">
              <table class="audit-table">
                <thead>
                  <tr>
                    <th>Video</th>
                    <th>Format</th>
                    <th>Views (vs Avg)</th>
                    <th>SEO Signals</th>
                    <th>Diagnosis</th>
                    <th>Fix Actions</th>
                  </tr>
                </thead>
                <tbody>
                  @for (v of audit.videoAudits; track v.videoId) {
                    <tr>
                      <td class="vid-cell">
                        <div class="vid-title-wrap">
                          <a [href]="v.url" target="_blank" rel="noopener" class="vid-link">{{ v.title }}</a>
                          <small>{{ v.publishedAt | date:'mediumDate' }}</small>
                        </div>
                      </td>
                      <td>
                        <span class="fmt-pill" [class.short]="v.isShort">{{ v.isShort ? 'Short' : 'Video' }}</span>
                      </td>
                      <td>
                        <div class="views-cell">
                          <strong>{{ v.views | number }}</strong>
                          <span class="perf-tag" [attr.data-perf]="v.performanceTag.toLowerCase()">
                            {{ v.performanceTag }} ({{ (v.viewRatioVsAvg * 100) | number:'1.0-0' }}%)
                          </span>
                        </div>
                      </td>
                      <td>
                        <div class="seo-chips">
                          <span class="seo-chip" [class.bad]="v.titleLength > 58">{{ v.titleLength }} chars</span>
                          <span class="seo-chip" [class.bad]="!v.hasDescriptionLink">
                            {{ v.hasDescriptionLink ? '✓ Link' : '✗ No Link' }}
                          </span>
                        </div>
                      </td>
                      <td class="diag-note">{{ v.diagnosticNote }}</td>
                      <td>
                        <div class="row-actions">
                          <button type="button" class="btn-xs" (click)="selectVideoFix(v)">✏️ Fix Title</button>
                          <button type="button" class="btn-xs ai" (click)="loadPromptText(v.fixPrompt, 'Optimize: ' + v.title)">🤖 Prompt</button>
                        </div>
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          </div>
        }

        <!-- Interactive AI Prompt Studio -->
        @if (audit.promptTemplates?.length) {
          <div id="prompt-studio" class="section-block prompt-studio-card">
            <div class="section-head">
              <h2>🤖 Personalized AI Growth Prompt Studio</h2>
              <span class="section-sub">Copy-paste ready prompts pre-injected with your real metrics, titles & keywords — use in Gemini, ChatGPT, or Claude</span>
            </div>

            <div class="prompt-tabs">
              @for (pt of audit.promptTemplates; track pt.id) {
                <button
                  type="button"
                  class="prompt-tab"
                  [class.active]="selectedPromptId() === pt.id"
                  (click)="selectPromptTemplate(pt)">
                  <span class="pt-cat">{{ pt.category }}</span>
                  <strong>{{ pt.title }}</strong>
                </button>
              }
            </div>

            <div class="prompt-editor-box">
              <div class="prompt-meta-row">
                <div>
                  <strong>{{ activePromptTitle() }}</strong>
                  <p class="prompt-desc">{{ activePromptDesc() }}</p>
                </div>
                <button type="button" class="btn-copy-lg" (click)="copyText(editablePromptText(), 'studio-prompt')">
                  {{ copiedKey() === 'studio-prompt' ? '✓ Copied to Clipboard!' : '📋 Copy Full Prompt' }}
                </button>
              </div>
              <textarea
                rows="10"
                [ngModel]="editablePromptText()"
                (ngModelChange)="editablePromptText.set($event)"
                class="prompt-textarea"></textarea>
            </div>
          </div>
        }

        <!-- Priority Suggestions Grid -->
        <div class="suggestions-section">
          <h2>🎯 Strategic Growth Playbook</h2>
          <div class="suggestions-grid">
            @for (sugg of audit.prioritySuggestions; track sugg.id) {
              <div class="sugg-card">
                <div class="sugg-header">
                  <span class="sugg-category">{{ sugg.category }}</span>
                  <span class="impact-badge" [attr.data-impact]="sugg.impact.toLowerCase()">
                    {{ sugg.impact }} Impact
                  </span>
                </div>
                <h3>{{ sugg.title }}</h3>
                <p class="sugg-desc">{{ sugg.description }}</p>

                <div class="action-box">
                  <strong>Action Required:</strong>
                  <span>{{ sugg.actionItem }}</span>
                </div>

                <div class="gain-chip">
                  Estimated Gain: <strong>{{ sugg.estimatedGain }}</strong>
                </div>
              </div>
            }
          </div>
        </div>

        <!-- Algorithmic Timing & Keywords Row -->
        <div class="meta-advisor-row">
          <div class="meta-card">
            <h3>⏰ Optimal Upload Time Windows</h3>
            <p class="meta-sub">Based on regional subscriber activity peaks:</p>
            <ul class="time-list">
              @for (time of audit.bestUploadTimes; track time) {
                <li>
                  <span class="clock-icon">🕒</span>
                  <span>{{ time }}</span>
                </li>
              }
            </ul>
          </div>

          <div class="meta-card">
            <h3>🔥 High-Demand SEO Keywords & Tags</h3>
            <p class="meta-sub">Click any tag to copy it cleanly into your video tags or prompt:</p>
            <div class="tag-cloud">
              @for (kw of audit.highDemandKeywords; track kw) {
                <button type="button" class="kw-tag" (click)="copyText('#' + kw, 'kw-' + kw)">
                  {{ copiedKey() === 'kw-' + kw ? '✓ #' + kw : '#' + kw }}
                </button>
              }
            </div>
          </div>
        </div>
      }

      <app-ad-slot placement="infeed" />
    </div>
  `,
  styles: [`
    .advisor-page { max-width: 1320px; margin: 0 auto; padding: 1.5rem 1rem 3rem; display: flex; flex-direction: column; gap: 1.5rem; }
    .header-top, .pane-head, .prompt-meta-row, .section-head { display: flex; justify-content: space-between; align-items: center; gap: 0.75rem; flex-wrap: wrap; }
    .header-top { align-items: flex-start; }
    .header-box h1 { font-size: 1.85rem; font-weight: 800; color: #0f172a; margin: 0 0 0.45rem; }
    .header-box p { color: #64748b; margin: 0; font-size: 0.96rem; max-width: 820px; line-height: 1.5; }
    .header-cta-group, .input-action-row, .issue-actions, .studio-btn-row, .row-actions, .tag-cloud { display: flex; gap: 0.55rem; flex-wrap: wrap; }
    .btn-outline, .btn-secondary-sm, .btn-copy { background: #fff; color: #1e293b; border: 1px solid #cbd5e1; border-radius: 7px; padding: 0.45rem 0.85rem; font-weight: 600; font-size: 0.82rem; text-decoration: none; cursor: pointer; }
    .btn-outline:hover { border-color: #2563eb; color: #2563eb; }
    .status-banner { margin-top: 0.75rem; padding: 0.65rem 0.95rem; border-radius: 8px; background: #ecfdf5; color: #065f46; border: 1px solid #a7f3d0; font-size: 0.88rem; font-weight: 600; }
    .status-banner.error { background: #fef2f2; color: #b91c1c; border-color: #fecaca; }
    .search-bar-card, .pillar-card, .sugg-card, .meta-card, .table-card { background: #fff; border: 1px solid #e2e8f0; border-radius: 12px; padding: 1.15rem; }
    .table-card { padding: 0; overflow-x: auto; }
    .studio-card, .prompt-studio-card { background: #fff; border: 1px solid #cbd5e1; border-radius: 14px; padding: 1.25rem; }
    .input-wrap label { display: block; font-size: 0.84rem; font-weight: 600; color: #475569; margin-bottom: 0.4rem; }
    .form-input { flex: 1; min-width: 220px; padding: 0.58rem 0.8rem; border: 1px solid #cbd5e1; border-radius: 8px; font-size: 0.94rem; }
    .form-select { padding: 0.58rem 0.8rem; border: 1px solid #cbd5e1; border-radius: 8px; background: #f8fafc; font-size: 0.88rem; }
    .btn-audit, .btn-primary-sm, .btn-fix, .btn-copy-lg { background: #0f172a; color: #fff; border: none; border-radius: 7px; padding: 0.48rem 0.9rem; font-weight: 700; font-size: 0.82rem; cursor: pointer; }
    .btn-fix, .btn-copy-lg { background: #2563eb; }
    .btn-audit { padding: 0.58rem 1.15rem; font-size: 0.88rem; }
    .btn-audit:disabled { opacity: 0.65; cursor: not-allowed; }
    .audit-hero-card { background: linear-gradient(135deg, #1e293b 0%, #0f172a 100%); color: #fff; border-radius: 14px; padding: 1.5rem; display: flex; flex-direction: column; gap: 1.15rem; }
    .hero-main-row { display: flex; align-items: center; gap: 1.4rem; flex-wrap: wrap; }
    .score-circle { width: 108px; height: 108px; border-radius: 50%; background: rgba(16,185,129,0.14); border: 4px solid #10b981; display: flex; flex-direction: column; align-items: center; justify-content: center; flex-shrink: 0; }
    .score-circle[data-grade="C"], .score-circle[data-grade="D"] { border-color: #f59e0b; background: rgba(245,158,11,0.14); }
    .score-val { font-size: 1.9rem; font-weight: 800; color: #34d399; line-height: 1; }
    .score-circle[data-grade="C"] .score-val, .score-circle[data-grade="D"] .score-val { color: #fbbf24; }
    .score-max { font-size: 0.7rem; color: #cbd5e1; font-weight: 700; margin-top: 0.2rem; }
    .audit-summary-text { flex: 1; min-width: 260px; }
    .title-pills { display: flex; align-items: center; gap: 0.55rem; flex-wrap: wrap; margin-bottom: 0.35rem; }
    .title-pills h2 { margin: 0; font-size: 1.35rem; font-weight: 800; }
    .badge-pill { font-size: 0.7rem; font-weight: 700; padding: 0.2rem 0.55rem; border-radius: 999px; background: rgba(255,255,255,0.14); color: #e2e8f0; }
    .badge-pill.live { background: rgba(16,185,129,0.22); color: #6ee7b7; }
    .exec-summary { margin: 0 0 0.35rem; color: #cbd5e1; font-size: 0.92rem; line-height: 1.5; }
    .ext-link { color: #60a5fa; font-size: 0.84rem; font-weight: 600; text-decoration: none; }
    .snapshot-kpi-strip { display: grid; grid-template-columns: repeat(auto-fit, minmax(150px, 1fr)); gap: 0.7rem; padding-top: 0.9rem; border-top: 1px solid rgba(255,255,255,0.12); }
    .kpi-tile { background: rgba(255,255,255,0.06); padding: 0.65rem 0.8rem; border-radius: 9px; }
    .kpi-tile span { display: block; font-size: 0.7rem; color: #94a3b8; text-transform: uppercase; margin-bottom: 0.15rem; }
    .kpi-tile strong { font-size: 0.92rem; color: #f8fafc; font-weight: 700; }
    .section-block { display: flex; flex-direction: column; gap: 0.85rem; }
    .section-head h2, .suggestions-section h2 { margin: 0; font-size: 1.2rem; font-weight: 800; color: #0f172a; }
    .suggestions-section h2 { margin-bottom: 0.9rem; }
    .section-sub { font-size: 0.84rem; color: #64748b; }
    .pillars-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(220px, 1fr)); gap: 0.85rem; }
    .pillar-card { padding: 0.95rem; }
    .pillar-top, .sugg-header, .issue-top { display: flex; justify-content: space-between; align-items: center; gap: 0.4rem; }
    .pillar-label { font-size: 0.86rem; font-weight: 700; color: #1e293b; }
    .pillar-score { font-size: 0.84rem; font-weight: 800; color: #059669; }
    .pillar-score[data-status="needs work"] { color: #d97706; }
    .pillar-score[data-status="critical"] { color: #dc2626; }
    .progress-track { height: 7px; background: #e2e8f0; border-radius: 999px; overflow: hidden; margin: 0.45rem 0; }
    .progress-fill { height: 100%; background: #10b981; border-radius: 999px; }
    .progress-fill[data-status="needs work"] { background: #f59e0b; }
    .progress-fill[data-status="critical"] { background: #ef4444; }
    .pillar-summary, .issue-evidence, .sugg-desc { margin: 0; font-size: 0.83rem; color: #475569; line-height: 1.45; }
    .issues-grid, .suggestions-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(320px, 1fr)); gap: 0.95rem; }
    .issue-card { background: #fff; border: 1px solid #e2e8f0; border-left: 4px solid #f59e0b; border-radius: 12px; padding: 1.05rem; display: flex; flex-direction: column; gap: 0.5rem; }
    .issue-card[data-severity="critical"] { border-left-color: #ef4444; }
    .issue-card[data-severity="opportunity"] { border-left-color: #2563eb; }
    .sev-badge, .impact-badge, .fmt-pill { font-size: 0.7rem; font-weight: 800; text-transform: uppercase; padding: 0.18rem 0.55rem; border-radius: 999px; }
    .sev-badge { background: #fef3c7; color: #b45309; }
    .sev-badge[data-severity="critical"], .impact-badge[data-impact="high"] { background: #fee2e2; color: #b91c1c; }
    .sev-badge[data-severity="opportunity"] { background: #dbeafe; color: #1d4ed8; }
    .impact-badge[data-impact="medium"] { background: #fef3c7; color: #b45309; }
    .impact-badge[data-impact="low"] { background: #dcfce7; color: #15803d; }
    .issue-cat, .sugg-category { font-size: 0.74rem; font-weight: 700; color: #2563eb; text-transform: uppercase; }
    .issue-card h3, .sugg-card h3 { margin: 0; font-size: 0.98rem; color: #0f172a; font-weight: 700; }
    .issue-impact, .gain-chip { font-size: 0.8rem; color: #047857; background: #ecfdf5; padding: 0.38rem 0.65rem; border-radius: 6px; }
    .studio-grid, .meta-advisor-row { display: grid; grid-template-columns: repeat(auto-fit, minmax(320px, 1fr)); gap: 1.15rem; }
    .studio-pane, .prompt-editor-box { background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 10px; padding: 0.95rem; display: flex; flex-direction: column; gap: 0.55rem; }
    .studio-pane h3, .meta-card h3 { margin: 0; font-size: 0.98rem; font-weight: 800; color: #0f172a; }
    .compact-select { max-width: 240px; padding: 0.32rem 0.5rem; border: 1px solid #cbd5e1; border-radius: 6px; font-size: 0.8rem; background: #fff; }
    .field-label { display: flex; justify-content: space-between; font-size: 0.78rem; font-weight: 700; color: #475569; }
    .char-counter { font-size: 0.75rem; color: #059669; }
    .char-counter.over, .perf-tag[data-perf="underperforming"] { color: #dc2626; }
    .studio-input, .studio-textarea, .prompt-textarea { width: 100%; box-sizing: border-box; padding: 0.55rem 0.75rem; border: 1px solid #cbd5e1; border-radius: 8px; font-size: 0.85rem; font-family: inherit; background: #fff; }
    .prompt-textarea { font-family: ui-monospace, Menlo, monospace; line-height: 1.48; }
    .mobile-preview-box { background: #0f172a; color: #f8fafc; border-radius: 8px; padding: 0.7rem; }
    .preview-label { display: block; font-size: 0.68rem; color: #94a3b8; text-transform: uppercase; margin-bottom: 0.2rem; }
    .mobile-title-sim { font-size: 0.9rem; font-weight: 700; line-height: 1.3; }
    .warn-hint { display: block; margin-top: 0.25rem; color: #fbbf24; font-size: 0.74rem; }
    .ok-hint { display: block; margin-top: 0.25rem; color: #34d399; font-size: 0.74rem; }
    .mb-sm { margin-bottom: 0.4rem; }
    .audit-table { width: 100%; border-collapse: collapse; min-width: 760px; }
    .audit-table th { text-align: left; font-size: 0.74rem; text-transform: uppercase; color: #64748b; padding: 0.7rem 0.85rem; border-bottom: 1px solid #e2e8f0; background: #f8fafc; }
    .audit-table td { padding: 0.7rem 0.85rem; border-bottom: 1px solid #f1f5f9; font-size: 0.84rem; vertical-align: top; }
    .vid-title-wrap, .views-cell, .seo-chips { display: flex; flex-direction: column; gap: 0.2rem; max-width: 270px; }
    .vid-link { font-weight: 700; color: #0f172a; text-decoration: none; }
    .vid-title-wrap small, .meta-sub, .prompt-desc { color: #64748b; font-size: 0.78rem; margin: 0.15rem 0 0; }
    .fmt-pill { background: #eff6ff; color: #1d4ed8; }
    .fmt-pill.short { background: #fdf2f8; color: #be185d; }
    .perf-tag { font-size: 0.72rem; font-weight: 700; color: #475569; }
    .perf-tag[data-perf="breakout"] { color: #059669; }
    .seo-chip { font-size: 0.72rem; font-weight: 600; color: #047857; background: #ecfdf5; padding: 0.12rem 0.4rem; border-radius: 5px; width: fit-content; }
    .seo-chip.bad { color: #b91c1c; background: #fef2f2; }
    .diag-note { color: #475569; max-width: 230px; line-height: 1.38; }
    .btn-xs { border: 1px solid #cbd5e1; background: #fff; border-radius: 6px; padding: 0.28rem 0.5rem; font-size: 0.74rem; font-weight: 700; cursor: pointer; }
    .btn-xs.ai { background: #eff6ff; border-color: #bfdbfe; color: #1d4ed8; }
    .prompt-tabs { display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 0.6rem; }
    .prompt-tab { text-align: left; background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 9px; padding: 0.7rem 0.85rem; cursor: pointer; display: flex; flex-direction: column; gap: 0.18rem; }
    .prompt-tab.active { background: #eff6ff; border-color: #2563eb; }
    .pt-cat { font-size: 0.68rem; font-weight: 800; text-transform: uppercase; color: #2563eb; }
    .prompt-tab strong { font-size: 0.86rem; color: #0f172a; }
    .sugg-card { display: flex; flex-direction: column; gap: 0.65rem; }
    .action-box { background: #f8fafc; border-left: 3px solid #3b82f6; padding: 0.6rem 0.8rem; border-radius: 4px; font-size: 0.82rem; color: #334155; display: flex; flex-direction: column; gap: 0.2rem; }
    .time-list { list-style: none; padding: 0; margin: 0.6rem 0 0; display: flex; flex-direction: column; gap: 0.5rem; }
    .time-list li { display: flex; align-items: center; gap: 0.45rem; font-size: 0.88rem; color: #334155; font-weight: 500; }
    .kw-tag { background: #eff6ff; color: #1d4ed8; border: 1px solid #bfdbfe; padding: 0.3rem 0.65rem; border-radius: 999px; font-size: 0.82rem; font-weight: 600; cursor: pointer; }
  `]
})
export class ChannelAdvisorComponent implements OnInit {
  private analytics = inject(AnalyticsService);
  readonly auth = inject(AuthService);
  private ecosystem = inject(EcosystemConfigService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);

  readonly studioPortalUrl = this.ecosystem.studioPortalUrl;

  readonly channels = signal<ChannelMetrics[]>([]);
  readonly selectedHandle = signal('');
  readonly selectedPlatform = signal<SocialPlatform>('YouTube');
  readonly auditResult = signal<ChannelAuditResult | null>(null);
  readonly auditing = signal(false);
  readonly savingChannel = signal(false);
  readonly statusBanner = signal('');
  readonly statusIsError = signal(false);
  readonly copiedKey = signal('');

  // Quick-Fix Studio signals
  readonly activeVideoFix = signal<VideoAuditItem | null>(null);
  readonly draftTitle = signal('');
  readonly draftDescription = signal('');
  readonly draftPinnedComment = signal('');

  // AI Prompt Studio signals
  readonly selectedPromptId = signal('');
  readonly activePromptTitle = signal('');
  readonly activePromptDesc = signal('');
  readonly editablePromptText = signal('');

  readonly mobilePreviewTitle = computed(() => {
    const t = this.draftTitle().trim();
    if (!t) return 'Enter a title draft above to test the 55-char mobile cutoff...';
    return t.length > 55 ? t.slice(0, 52) + '...' : t;
  });

  ngOnInit(): void {
    this.ecosystem.load();
    const qHandle = this.route.snapshot.queryParamMap.get('handle')?.trim() ?? '';
    const qPlatform = (this.route.snapshot.queryParamMap.get('platform')?.trim() as SocialPlatform) || 'YouTube';
    const active = this.analytics.activeLookup();

    if (qHandle) {
      this.selectedHandle.set(qHandle);
      this.selectedPlatform.set(qPlatform);
      if (active && (active.channel.channelHandle.toLowerCase() === qHandle.toLowerCase() || active.channel.channelName.toLowerCase() === qHandle.toLowerCase())) {
        this.hydrateAudit(active.audit);
      } else {
        this.runAudit(false);
      }
    } else if (active) {
      this.selectedHandle.set(active.channel.channelHandle || active.channel.channelName);
      this.selectedPlatform.set(active.channel.platform);
      this.hydrateAudit(active.audit);
    }

    this.analytics.getChannels().subscribe({
      next: (list) => {
        this.channels.set(list);
        if (!qHandle && !active && list.length > 0) {
          this.selectedHandle.set(list[0].channelHandle || list[0].channelName);
          this.selectedPlatform.set(list[0].platform);
          this.runAudit(false);
        } else if (!qHandle && !active && list.length === 0) {
          this.analytics.auditChannel('', 'YouTube').subscribe({
            next: (res) => this.hydrateAudit(res)
          });
        }
      },
      error: () => {
        if (!this.auditResult()) {
          this.analytics.auditChannel('', 'YouTube').subscribe({
            next: (res) => this.hydrateAudit(res)
          });
        }
      }
    });
  }

  selectConnectedChannel(event: Event): void {
    const id = (event.target as HTMLSelectElement).value;
    const matched = this.channels().find(c => c.id === id);
    if (matched) {
      this.selectedHandle.set(matched.channelHandle || matched.channelName);
      this.selectedPlatform.set(matched.platform);
      this.runAudit(false);
    }
  }

  healthLabel(score: number): string {
    if (score >= 85) return 'Excellent';
    if (score >= 70) return 'Strong';
    if (score >= 55) return 'Moderate';
    return 'Needs Attention';
  }

  runAudit(forceLiveLookup = false): void {
    const handle = this.selectedHandle().trim();
    const platform = this.selectedPlatform();
    if (!handle) {
      this.analytics.auditChannel('', platform).subscribe({
        next: (res) => this.hydrateAudit(res)
      });
      return;
    }

    this.auditing.set(true);
    this.statusBanner.set('');
    const isSaved = this.channels().some(
      c => (c.channelHandle.toLowerCase() === handle.toLowerCase() || c.channelName.toLowerCase() === handle.toLowerCase())
        && c.platform === platform
    );

    if (isSaved && !forceLiveLookup) {
      this.analytics.auditChannel(handle, platform).subscribe({
        next: (res) => { this.hydrateAudit(res); this.auditing.set(false); },
        error: () => this.auditing.set(false)
      });
      return;
    }

    this.analytics.lookupChannel({ query: handle, platform }).subscribe({
      next: (res) => {
        this.hydrateAudit(res.audit);
        this.auditing.set(false);
      },
      error: () => {
        this.analytics.auditChannel(handle, platform).subscribe({
          next: (res) => { this.hydrateAudit(res); this.auditing.set(false); },
          error: () => {
            this.auditing.set(false);
            this.statusIsError.set(true);
            this.statusBanner.set('Could not run live lookup for that handle right now. Check the @handle spelling and try again.');
          }
        });
      }
    });
  }

  saveCurrentToWorkspace(): void {
    const audit = this.auditResult();
    if (!audit || !this.auth.isAuthenticated()) return;
    this.savingChannel.set(true);
    this.statusBanner.set('');
    this.analytics.autoAddMyChannel({
      query: audit.snapshot?.channelHandle || audit.channelHandle,
      platform: (audit.snapshot?.platform as SocialPlatform) || this.selectedPlatform(),
      niche: audit.snapshot?.niche
    }).subscribe({
      next: (saved) => {
        this.savingChannel.set(false);
        this.statusIsError.set(false);
        this.statusBanner.set(`Saved "${saved.channelName}" to your private workspace.`);
        this.channels.update(prev => [saved, ...prev.filter(c => c.id !== saved.id)]);
      },
      error: (err) => {
        this.savingChannel.set(false);
        this.statusIsError.set(true);
        if (err?.status === 401) {
          this.statusBanner.set(
            err?.error?.message ||
            'Session could not be verified (HTTP 401). Sign out & back in via SSO, or ensure Jwt__SigningKey on social-analytics-api matches admin-api.'
          );
          return;
        }
        this.statusBanner.set(err?.error?.message || 'Could not save channel to workspace.');
      }
    });
  }

  triggerIssueFix(issue: ChannelIssueItem): void {
    if (issue.fixRoute) {
      this.router.navigate([issue.fixRoute]);
      return;
    }
    if (issue.fixActionType === 'metadata-studio') {
      const targetVideo = this.auditResult()?.videoAudits?.find(v => v.titleLength > 55)
        || this.auditResult()?.videoAudits?.[0]
        || null;
      if (targetVideo) {
        this.selectVideoFix(targetVideo);
      } else {
        document.getElementById('fix-studio')?.scrollIntoView({ behavior: 'smooth' });
      }
      return;
    }
    this.loadPromptText(issue.fixPrompt, issue.title);
  }

  pickVideoForFix(event: Event): void {
    const vidId = (event.target as HTMLSelectElement).value;
    const matched = this.auditResult()?.videoAudits?.find(v => v.videoId === vidId);
    if (matched) this.selectVideoFix(matched);
  }

  selectVideoFix(v: VideoAuditItem): void {
    this.activeVideoFix.set(v);
    this.draftTitle.set(v.title);
    document.getElementById('fix-studio')?.scrollIntoView({ behavior: 'smooth' });
  }

  applySuggestedRewrite(): void {
    const v = this.activeVideoFix();
    if (v?.suggestedTitleRewrite) {
      this.draftTitle.set(v.suggestedTitleRewrite);
    }
  }

  selectPromptTemplate(pt: AiPromptTemplate): void {
    this.selectedPromptId.set(pt.id);
    this.activePromptTitle.set(pt.title);
    this.activePromptDesc.set(pt.description);
    this.editablePromptText.set(pt.promptText);
  }

  loadPromptText(text: string, title: string): void {
    this.selectedPromptId.set('custom');
    this.activePromptTitle.set(title);
    this.activePromptDesc.set('Loaded from channel issue/video diagnosis — customize freely and copy.');
    this.editablePromptText.set(text);
    document.getElementById('prompt-studio')?.scrollIntoView({ behavior: 'smooth' });
  }

  copyText(text: string, key: string): void {
    if (!text) return;
    navigator.clipboard?.writeText(text);
    this.copiedKey.set(key);
    setTimeout(() => {
      if (this.copiedKey() === key) this.copiedKey.set('');
    }, 2200);
  }

  private hydrateAudit(audit: ChannelAuditResult): void {
    this.auditResult.set(audit);
    this.draftDescription.set(audit.recommendedDescriptionTemplate ?? '');
    this.draftPinnedComment.set(audit.recommendedPinnedComment ?? '');

    const weakestOrFirst = audit.videoAudits?.find(v => v.performanceTag === 'Underperforming')
      || audit.videoAudits?.[0]
      || null;
    this.activeVideoFix.set(weakestOrFirst);
    this.draftTitle.set(weakestOrFirst?.title ?? '');

    const firstPrompt = audit.promptTemplates?.[0];
    if (firstPrompt) {
      this.selectPromptTemplate(firstPrompt);
    }
  }
}
