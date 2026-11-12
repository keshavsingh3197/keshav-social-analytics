# SocialPulse — Keshav Social Analytics & Growth Platform

**Domain:** `https://social.keshavsingh.in` (and `https://analytics.keshavsingh.in`)  
**Backend:** .NET 10 Web API (`SocialAnalytics.Api`)  
**Frontend:** Angular (Standalone Components, Signals, Modern CSS)  
**Authentication:** Central Single Sign-On (SSO) via `admin.keshavsingh.in` / `id.keshavsingh.in`

---

## 🎯 Purpose & Vision

SocialPulse is an analytics and creator monetization platform engineered to:
1. **Multi-Platform Channel Telemetry:** Track views, watch hours, follower velocity, and engagement across YouTube, Instagram, and Facebook.
2. **Creator Earnings & RPM Estimator:** Interactive financial simulation tool computing AdSense RPM, sponsorship rate cards, and affiliate conversion revenue.
3. **AI Growth Advisor:** Actionable, algorithm-informed recommendations for hook optimization, title CTR improvements, retention fixes, and optimal regional upload time windows.
4. **Traffic Funneling & Cross-Promotion:** Channel viral social traffic directly into sibling platforms (`studio.keshavsingh.in`, `panchang.keshavsingh.in`, `git.keshavsingh.in`) to multiply user lifetime value and diversify revenue streams.

---

## 🏗️ Architecture & Ecosystem Links

SocialPulse integrates natively into the **keshavsingh.in** federated ecosystem:
- **Central Identity Provider (IdP):** `admin.keshavsingh.in` / `id.keshavsingh.in`
- **Animation Studio AI:** `studio.keshavsingh.in` (Keshav-AnimStudio-AI)
- **Hindu Panchang & Cultural Hub:** `panchang.keshavsingh.in` (hindu-panchang)
- **Ghar Ledger:** `ghar.keshavsingh.in`
- **Pratiyogita Exam Platform:** `pratiyogita.keshavsingh.in`
- **Engineering Content Blog:** `git.keshavsingh.in`

### Single Sign-On (SSO) Flow
- The frontend operates in **Public Visitor Mode** by default so search traffic and YouTube viewers can access earnings calculators and public benchmarks with zero friction.
- For authenticated creator workspace features, the client calls `POST https://id.keshavsingh.in/api/sso/session` with credentials (`ks_sso` cookie scoped to `.keshavsingh.in`).
- If unauthenticated, creators are redirected to `https://admin.keshavsingh.in/login?return=<url>&app=social-analytics`.
- The backend validates the symmetric HS256 JWT issued by `keshavsingh-idp` with audience `keshavsingh-apps`.

---

## 🚀 Running Locally

### Backend (.NET 10 API)
```powershell
cd D:\GITHUB\social-analytics\backend\SocialAnalytics.Api
dotnet run
# Runs on http://localhost:5080 (Swagger available at /swagger)
```

### Frontend (Angular)
```powershell
cd D:\GITHUB\social-analytics\frontend
npm install
npm start
# Runs on http://localhost:4300
```

---

## 📦 Deployment

- **Frontend:** Automatically built and published to GitHub Pages via `.github/workflows/deploy-frontend.yml` pointing to custom domain `social.keshavsingh.in`.
- **Backend:** Dockerized web service deployable to Render via `render.yaml`.
