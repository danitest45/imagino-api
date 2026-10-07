# Imagino — launch readiness
Reference: 2026-10-07. TECHNICAL LAUNCH READINESS = BLOCKED.

## Scope and provenance
Backend: `feat/imagino-launch-readiness`, isolated worktree `work/imagino-api-launch-readiness`, base `codex/imagino-ai-revival-v2` at `841248051a6ce79d1adf5d2f28c842b9782590fb`; dependency [PR #56](https://github.com/danitest45/imagino-api/pull/56).
Frontend: same launch branch in `work/imagino-front-launch-readiness`, base `feat/imagino-creative-hub-core` at `109c9dbbb8bbde55b93436949c1db961590ab570`; dependency [PR #89](https://github.com/danitest45/imagino-front/pull/89).
No merges, base rewrites, paid AI calls, commercial changes or production mutations.

## Evidence and limits
Local backend: 398 passed, zero failed/skipped; Release publish passed. Tests include an isolated Mongo 8.0.16 replica set: ten simultaneous reservations admit exactly five against a synthetic USD 0.10 ceiling, reserve maximum USD 0.02 each, settle once and retain unknown submission costs. Forty simultaneous login-counter increments admit exactly ten. Synthetic backup/restore copies five critical collection classes into a new throwaway database and compares BSON; this does not prove Atlas snapshot or R2 recovery.
Frontend: 41 unit tests; 60 existing Chromium tests plus a private-media test. Includes keyboard/mobile/theme/axe, quote invalidation, ambiguous submit/manual idempotent retry, expired session, unavailable history/download, reuse and Animate preparation. All browser external requests intercepted.
Production npm dependencies and NuGet audit: no reported vulnerabilities after patches. Full npm audit still reports five high findings in the trusted development lint dependency chain (unpatched braces); runtime audit excludes them. See security review.
Staging read smoke: 2026-10-07T20:21Z, service `srv-db1tmv17lnhs73efdjp0`, health/image download/video download 200, anonymous 401, foreign 404, refresh/logout successful, wallet unchanged, eleven historical jobs, zero creation/provider POST/email.
Historical public image HEAD returned 200: privacy remains P0.
Atlas read-only: owner_history IXSCAN for bounded owner-history query, no sort, dummy owner/zero rows/2ms; maximum observed job BSON 4,054 bytes. Not a load benchmark.
Render remains on revival branch; live deploy `dep-db37rcss728c73bkoc40`, commit `1a16c3eb4cf6629f6ad4d52ee00c72ae414e08b4`. Connector cannot switch branch; UI runtime failed during Windows sandbox initialization. New backend has not passed remote final gate.

## Matrix
PASS below means the documented technical contract passed the stated evidence; it does not authorize production.

| Area | Status | Evidence | Blocker | Human action |
|---|---|---|---|---|
| auth | PASS | JWT/refresh/CSRF/owner negatives; staging login/refresh/logout | Production origin/cookie smoke remains in launch sequence | Approve exact production hosts |
| media privacy | BLOCKED | Owner API/blob implementation, local 401/404/Range; old public HEAD 200 | Historical public access; new branch remote gate | Enable staging branch, then private migration/access removal |
| jobs | PASS | Durable leases, idempotency, stored-output recovery; local tests | Remote reconciliation smoke follows deploy | Review operational roles |
| credits | PASS | Transactional reserve/charge/refund; replay/race tests | Commercial expiry/rollover/trial not approved | Decide policy before enabling offers |
| pricing engine | PASS | Owner-signed quote; accepted model/price snapshot; stale/tamper tests | Catalog/plan commercial approval | Approve versions and included credits |
| cost budgets | PASS | Durable atomic provider/day/month/model/user gates; fail-closed defaults | No production limits approved | Approve reviewed per-offer maximum costs and limits |
| rate limiting | PASS | Durable counters/TTL/concurrency tests; polling separate | Trusted proxy IP mapping requires deployed evidence | Supply exact trusted proxy configuration |
| providers | BLOCKED | Existing finite contracts preserved; no paid calls this phase | OpenAI full-request ceiling; historical finite smoke policies | Approve cost bounds and separately authorize any future paid smoke |
| storage | BLOCKED | Private key/integrity/size/MIME/input storage code | R2 migration/public-off proof and media memory capacity | Cloudflare access and isolated staging smoke |
| database | PASS | Replica-set transactions; staging indexes/explain/document sizes | New budget/rate indexes not deployed | Staging deployment; production isolation later |
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
| rollback | PASS | Versioned deploy/config/catalog/job-drain runbook | Deployed private-media rollback rehearsal pending | Preserve previous deployments and authorize staged rollout |

Read [LAUNCH_BLOCKERS.md](LAUNCH_BLOCKERS.md) before any deployment or commercial enablement.

