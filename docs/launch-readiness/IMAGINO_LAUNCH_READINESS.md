# Imagino — launch readiness
Reference: 2026-10-07. TECHNICAL LAUNCH READINESS = BLOCKED.

## Scope and provenance
Backend: `feat/imagino-launch-readiness`, isolated worktree `work/imagino-api-launch-readiness`, base `codex/imagino-ai-revival-v2` at `841248051a6ce79d1adf5d2f28c842b9782590fb`; dependency [PR #56](https://github.com/danitest45/imagino-api/pull/56).
Frontend: same launch branch in `work/imagino-front-launch-readiness`, base `feat/imagino-creative-hub-core` at `109c9dbbb8bbde55b93436949c1db961590ab570`; dependency [PR #89](https://github.com/danitest45/imagino-front/pull/89).
No merges, base rewrites, paid AI calls, commercial changes or production mutations.

## Evidence and limits
Local backend: 398 passed, zero failed/skipped; Release publish passed. Tests include an isolated Mongo 8.0.16 replica set: ten simultaneous reservations admit exactly five against a synthetic USD 0.10 ceiling, reserve maximum USD 0.02 each, settle once and retain unknown submission costs. Forty simultaneous login-counter increments admit exactly ten. Synthetic backup/restore copies five critical collection classes into a new throwaway database and compares BSON; this does not prove Atlas snapshot or R2 recovery.
Frontend: 41 unit tests and all 64 Chromium tests passed, including private-media/logout, noindex/baseline, provider-disabled Animate preparation and slow-JavaScript login hydration. Includes keyboard/mobile/theme/axe, quote invalidation, ambiguous submit/manual idempotent retry, expired session, unavailable history/download, reuse and Animate preparation. All local browser external requests intercepted; real remote evidence is recorded separately.
Production npm dependencies and NuGet audit: no reported vulnerabilities after patches. Full npm audit still reports five high findings in the trusted development lint dependency chain (unpatched braces); runtime audit excludes them. See security review.
Staging read smoke: 2026-10-07T20:21Z, service `srv-db1tmv17lnhs73efdjp0`, health/image download/video download 200, anonymous 401, foreign 404, refresh/logout successful, wallet unchanged, eleven historical jobs, zero creation/provider POST/email.
Historical public image HEAD returned 200: privacy remains P0.
Atlas read-only: owner_history IXSCAN for bounded owner-history query, no sort, dummy owner/zero rows/2ms; maximum observed job BSON 4,054 bytes. Not a load benchmark.
Post-deployment Atlas audit confirmed eight Completed image jobs with private StoredOutput metadata, eight Charged images, one Charged video and two Refunded failures, still eleven jobs. Jobs retain five expected indexes; request_limits_v1 has _id and expiry indexes. Cost collections are not instantiated with paid admission disabled; deterministic _id keys and reservation races were verified in the isolated local replica set, not through a paid staging request.
After the operator changed only the AI staging branch, launch backend commit `e991ea9782859402cac624d27b4361984618a334` became live. Cleanup deploy `dep-db3b9fh42hec7396ac7g` keeps autoDeploy off, paid/smoke flags false and emergency stop true. Exact launch Preview origin was appended without replacing the three prior origins.
Private staging API gate at 21:07Z: eight Completed image copies with equal hashes/bytes, repeated migration success and accepted credits unchanged; video 200/Range 206; anonymous 401, foreign 404/operator 403; readiness 200; synthetic quote one credit; disabled creation 403; Completed reconcile 404; GC dry-run zero candidates. Wallet/history unchanged, zero new jobs/provider POST/email. Temporary operator entry removed and own operations access confirmed 403 at 21:11Z. Historical public object still HEAD 200; no source object deleted.

## Matrix
PASS below means the documented technical contract passed the stated evidence; it does not authorize production.

| Area | Status | Evidence | Blocker | Human action |
|---|---|---|---|---|
| auth | PASS | JWT/refresh/CSRF/owner negatives; staging login/refresh/logout | Production origin/cookie smoke remains in launch sequence | Approve exact production hosts |
| media privacy | BLOCKED | Eight private image hash matches, 401/404; video 206; real UI/blob/logout pass | Historical public HEAD 200 | Disable only old staging public exposure; no object deletion |
| jobs | PASS | Durable leases/idempotency/recovery tests; deployed Completed reconcile 404 | Operational role assignment for production | Review roles |
| credits | PASS | Transactional reserve/charge/refund; replay/race tests | Commercial expiry/rollover/trial not approved | Decide policy before enabling offers |
| pricing engine | PASS | Owner-signed quote; accepted model/price snapshot; stale/tamper tests | Catalog/plan commercial approval | Approve versions and included credits |
| cost budgets | PASS | Durable atomic provider/day/month/model/user gates; fail-closed defaults | No production limits approved | Approve reviewed per-offer maximum costs and limits |
| rate limiting | PASS | Durable counter races; deployed 30 synthetic quotes then 429/Retry-After 60; no financial change | Trusted proxy IP mapping requires topology review | Supply exact trusted proxy configuration |
| providers | BLOCKED | Existing finite contracts preserved; no paid calls this phase | OpenAI full-request ceiling; historical finite smoke policies | Approve cost bounds and separately authorize any future paid smoke |
| storage | BLOCKED | Deployed private copies/hash/no-store; video Range; private inputs code | Old public access and intended-tier memory/restore capacity | Disable old staging exposure; approve capacity/restore target |
| database | PASS | Replica-set transactions; staging deployment/indexes/explain/document sizes | Production resource/isolation approval | Approve isolated production resources later |
| backups | BLOCKED | Non-destructive synthetic restore; defined runbook | Atlas snapshot/PITR and R2 restore not demonstrated | Choose RPO/RTO; demonstrate restore to separate resources |
| observability | BLOCKED | Safe logs, Meter instruments, operational stuck-job endpoint | Export/alert destination and deployed end-to-end proof | Choose operator/contact and monitoring binding |
| email | BLOCKED | Token CAS, anti-enumeration, rate limits, synthetic sender tests | Sender DNS/suppression operations and production delivery | Approve domain/sender; run controlled email gate |
| OAuth | BLOCKED | State/nonce/PKCE/cookie checks in existing suite | Live Google configuration; memory state limits multi-instance | Approve OAuth domains and single-instance vs durable state |
| Stripe | BLOCKED | Checklist only; integration unopened | Fiscal/pricing/tax/live decisions | Business/fiscal approval |
| legal | BLOCKED | DRAFT product facts/retention/deletion proposal | Counsel approval; deletion workflow intentionally gated | Approve policy and operator process |
| analytics | DEFERRED | Privacy event schema; no tracking vendor | No vendor deployment needed for technical smoke | Approve analytics if desired |
| SEO | PASS | Preview noindex; gated robots/sitemap/canonical/OG/Twitter | Indexing off until approved production origin | Approve PUBLIC_SITE_URL and INDEX_PUBLIC_SITE |
| domains | BLOCKED | Read-only Vercel inventory; architecture proposal | Target domains/DNS/OAuth/email alignment | Approve hostnames/TLS/DNS separately |
| production infra | BLOCKED | Staging isolation and production design documented | No resources/budgets/credentials authorized | Approve resources and operating envelope |
| rollback | PASS | Versioned compatible deploy/config/catalog/job-drain runbook; staging cleanup redeploy preserved migrated assets | Production rehearsal requires separate rollout approval | Preserve tested private-media deployment; no rollback to public contract |

Read [LAUNCH_BLOCKERS.md](LAUNCH_BLOCKERS.md) before any deployment or commercial enablement.
Final no-paid staging checks: full-browser refresh after navigation passed; refresh after logout returned 401. The deployed quote-only limit accepted 30 synthetic quotes and rejected attempt 31 with 429/Retry-After 60, without jobs or wallet changes. Post-browser application log sample: 14 entries, no further page, zero scanned secret/prompt/media markers. Historical public access remains unresolved; the operator is unavailable now. Stop here until that staging bucket exposure can be removed, then recheck old URL denial and all owner downloads.


Draft PRs: [backend #57](https://github.com/danitest45/imagino-api/pull/57), [frontend #90](https://github.com/danitest45/imagino-front/pull/90). Frontend code deployment `dpl_CCtNNdePCeWZXCaesVLmupSanZcQ`, commit `7c911b3c21ab8efd4bafd8f0f3b8aabc826d6704`, is READY at [launch Preview](https://imagino-front-git-feat-imagino-laun-348f6b-danitest45s-projects.vercel.app). Protected HTTP verification: landing 200/noindex/no canonical, robots 200 disallow-all, empty sitemap 200, retired image proxy 410. Real browser gate: login, Assets/private image, authenticated download/hash, reuse, owned Animate preparation with Generate disabled, MP4 playback, themes and logout/blob revocation passed, with wallet/history unchanged and zero job/provider POST. This gate revealed and fixed early input loss before login hydration; a delayed-JavaScript regression now protects it. Deployment protection remains enabled.

