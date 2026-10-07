# Launch blockers
TECHNICAL LAUNCH READINESS = BLOCKED. No readiness percentage.

| ID | Priority | Exact unresolved gate | Required next evidence/action |
|---|---|---|---|
| B1 | P0 | Historical staging image remains unauthenticated-public (HEAD 200 after private copy) | All eight Completed owner images copied privately with equal hashes; operator must disable old bucket r2.dev/custom-domain/Worker access, then prove old URL denial |
| B3 | P0 | Backend history contains credential candidates; validity/revocation unknown | Private operator review and explicit rotation/revocation approval; no values or rotations in this task |
| B4 | P0 | Real production offer complete-request cost bounds and budgets not approved, especially OpenAI | Human reviewed maximum cost/version/expiry and production limits; remain disabled; future paid smoke needs separate authorization |
| B5 | P1 | Atlas snapshot/PITR and R2 independent restore/capacity not proved | Choose tier/RPO/RTO, restore to separate resources; measure large-media buffering/Range under actual tier |
| B6 | P1 | Metrics exporter/alert operator, proxy topology and scalable OAuth state unresolved | Bind monitoring/alerts; verify trusted IP mapping; choose single-instance or shared OAuth state and exercise deployed gates |
| B7 | P1 | Production isolation/domains/auth/email/legal/deletion policy not approved | Approve resources/credentials/DNS/Google/sender and retention workflow; current self-delete is gated |

Commercial gates are separate: fiscal/tax/live Stripe, final included credits/expiry/rollover/trial, legal terms/privacy/provider rights. See dedicated checklists. Production deployment, secrets/resources, DNS, paid calls and billing remain outside authorization.
Private API gate: readiness 200, eight image SHA/byte matches and repeat-migration success, video 200/206, anonymous 401, foreign 404, foreign operator 403, disabled creation 403, Completed reconcile 404, GC dry-run/zero candidates. Wallet/history unchanged, zero creation/provider POST/email. Temporary operator entry cleared and own operator access verified 403 after cleanup deploy. See compact staging evidence.
Resolved B2: launch branch deployed with autoDeploy off; protected real browser gate passed with existing assets, zero paid calls and unchanged wallet/history. Specific temporary Preview access was authorized by the user. General Preview protection remains enabled. The slow-hydration login defect found in that gate was fixed and covered by a regression test.
User is unavailable for the Cloudflare action now. Work stops at B1; on return disable only the old staging bucket public exposure, preserve all original objects, then recheck unauthenticated old-URL denial and authenticated image/video/download access. Do not enable paid generation or production as part of that follow-up.
Authenticated proxying does not revoke a previously public R2 object. No current proof is an approval to enable paid offers.
