# Production preparation runbook
STOP before production/fiscal/legal/DNS/live billing/OAuth/paid provider operations. This is an ordered plan, not authorization.

## Complete staging first
AI staging now runs `feat/imagino-launch-readiness`, with autoDeploy off. Verified launch profile has LaunchReadinessEnabled=true, PaidGenerationEnabled=false, OpenAiSingleSmokeEnabled=false, RunwayRealSmokeEnabled=false, GenerationCostControls__EmergencyStop=true. Existing finite provider integration flags/credentials remain for read-only status; expired paid smoke flags stay off. Never replace the entire environment.
Verified new frontend branch Preview origin must be explicitly copied into LaunchReadiness__PreviewOrigin and Cors__AllowedOrigins__3; preserve the three existing exact origins and existing Frontend__BaseUrl. No wildcard CORS. Preview-only Vercel branch variables: API URL exact AI staging, generation flag true, MEDIA_ALLOWED_HOSTS existing staging host (legacy build guard only; retired proxy never uses it). Preview protection and noindex remain enabled.
Backend live/readiness and private API gate passed; all eight Completed synthetic owner images migrated with equal SHA/bytes. Video/Range, owner/foreign/anonymous, disabled creation, read-only GC and financial/history invariants passed. Temporary operator role was cleared and own operations access verified 403. Protected Preview browser gate passed, including refresh across navigation and revoked refresh after logout. The operator then disabled only `imagino-images-staging` Public Development URL. Read-only B1 passed at 2026-10-08T00:47:55Z: all eight old r2.dev URLs HEAD/GET 401; owner image/download hashes unchanged; video/download 200 with matching bytes/hash and both Range routes 206; all anonymous 401/foreign 404. Wallet/history unchanged, zero new jobs or paid provider POST. Preserve original objects; GC stays dry-run. Independently inventory custom-domain/Worker exposure before production, then address remaining B3-B7 gates.

## Later production sequence (separate approvals)
1. Approve resources/operating envelope/RPO/RTO and policy.
2. Provision distinct secrets/config and export names/version manifests privately.
3. Initialize versioned catalog, unique history/idempotency/task/ledger/budget and TTL rate indexes in isolated production replica set; read explain.
4. Create private storage and verify anonymous denial; no permanent public asset URLs.
5. Deploy reviewed production profile with providers/checkout/trial off.
6. Auth/refresh/reset/verify/Google negative gate with exact domains.
7. Synthetic and migrated media gate plus memory/Range capacity.
8. Separately approved test billing/webhook/reconciliation gate.
9. Only after hard cost bounds/limits approval, separately authorized bounded provider smoke.
10. Limited approved users; enforce daily/monthly/model/user budgets.
11. Observe metrics/alerts/restore drills; record known-good deployment/config/catalog SHAs.
12. General launch after all P0/P1 and business gates pass.

## Backup and restore
Approve RPO/RTO (proposal <=24h/4h, not promised). Verify Atlas snapshot/PITR feature availability for chosen tier; backup replica-set consistent account/job/ledger/budget/catalog state, encrypted least-privilege access and retention. Do not export live refresh secrets into public artifacts. R2 durability is not an independent backup: approve replicated/exported object manifest/hash and retention/recovery strategy; back up configuration/catalog versions privately without Git secret values.
Restore always to a new isolated database and private destination bucket, provider worker/admission off. Restore indexes and consistent financial state, compare collection counts plus wallet/ledger/reservation invariants, verify object hashes, ownership and accepted pricing snapshots. Never re-submit restored Starting/unknown jobs; reconcile known task IDs or held costs. Restore secret names/config via vault/operator, not stale Git credentials. Run no-paid smoke; obtain approval before cutover. Local synthetic five-collection restore passed; Atlas/R2 recovery drill is still BLOCKED.

Branch-specific public build variables apply only to the new Vercel Preview branch. Exact launch Preview origin: https://imagino-front-git-feat-imagino-laun-348f6b-danitest45s-projects.vercel.app . It was added as the fourth explicit CORS origin only after confirming the compatible backend branch. No production configuration was changed. The temporary named Admin__UserIds__LaunchReadinessOperator entry is now empty; do not grant it again unless an authorized bounded staging operation requires it, and verify removal afterward.
