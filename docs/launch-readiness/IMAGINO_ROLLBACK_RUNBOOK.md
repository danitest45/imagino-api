# Rollback runbook
Prepare exact known-good SHAs/deployment IDs and configuration manifest before enabling users. No rollback executed in production here.

| Component | Trigger and reversible response |
|---|---|
| Providers/catalog | Set emergency stop and disable affected offer; preserve accepted snapshots; never release unknown cost without evidence |
| Frontend | Promote known-good Vercel deployment only after verifying it supports authenticated private media; old public-URL frontend is not a safe privacy rollback |
| Backend | Deploy known-good compatible Render commit/config, disable new admission first; keep existing leases/jobs/financial records |
| Jobs | Drain known task IDs via GET/poll; recover OutputStored then settle once; expired Starting with lost response refunds user once and keeps provider cost held; never repeat POST blindly |
| Pricing | Disable model; restore reviewed catalog version for new quotes only; accepted jobs keep historical price/version |
| Billing | Disable new checkout/subscriptions under separate approval; preserve customers/invoices/webhook event ledger; no destructive cancellation |
| Database | No destructive schema rollback; additive fields/indexes and old-job compatibility; restore only to new isolated database |
| Storage | Preserve private objects and hashes; never restore public access to resolve UI outage; compatible byte proxy must stay available |
| Auth | Preserve signing/refresh compatibility unless compromise demands approved invalidation; confirm Origin/cookie/session behavior |

Observe backlog, budgets, refunds, 5xx/readiness and media ownership after rollback. Verify no duplicate charge/POST. Record operator/time/config/deploy/journal identifiers without token/prompt/raw provider payload.
Staging prior deploy `dep-db37rcss728c73bkoc40` is baseline for general behavior but exposes historical public image URLs: it cannot be called a privacy-safe final rollback target. A private-media known-good deployment and rehearsal still need the final staging gate.
