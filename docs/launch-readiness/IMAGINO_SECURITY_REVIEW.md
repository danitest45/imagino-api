# Security review
Reference: 2026-10-07. Scope: isolated launch worktrees and authorized AI staging.

## Findings
| Priority | Finding | Response |
|---|---|---|
| P0 | Historical image objects publicly readable through staging r2.dev | B1 resolved for all eight known URLs: Cloudflare HEAD/GET 401 after operator disabled Public Development URL; private owner media/download hashes and authorization negatives passed. Other custom-domain/Worker inventory remains unverified in B7 |
| P0 | Provider price/config drift and concurrent budget overspend | Fail-closed reviewed-cost metadata, atomic reservations, emergency stop default true |
| P1 | Eight POSSIBLY_REAL legacy credential candidates in public Git history; zero confirmed active P0 | Value-free eight-record inventory; authenticity, consumers and revocation still need private evidence. No rotation/revocation authorized |
| P1 | Cookie refresh/logout relied on CORS alone | Exact Origin check when refresh cookie is present; missing/evil Origin rejected |
| P1 | Distributed endpoint abuse and per-job path bypass | Mongo TTL counters for IP/account/email; normalized media/job scopes; fail closed on Mongo failure |
| P1 | Email token replay | Atomic consume of unexpired, unused token; trial stays disabled |
| P1 | Row-only account delete leaves media/sessions/jobs | Self-delete returns review-required 409; foreign 404; no records destroyed |
| P1 | Raw provider/storage errors | Generic response, no raw body/meta; privacy regression test and safe operational error logging |
| P2 | Full npm audit: five high development lint-chain findings | Runtime audit clean; restrict lint to trusted repository input; revisit patched braces upstream |
| P2 | Repository evidence growth | Preserve base review evidence; future screenshots/traces in release artifacts, compact summaries in Git |

The initial heuristic scan (807 backend/669 frontend text blobs; 41 Mongo-pattern and five literal-secret matches) is superseded by the B3 audit of fetched reachable branches/tags/PR heads. B3 scanned 1,034 backend and 958 frontend text blobs, grouping 54 candidate/literal records into 28 TEST_ONLY, 18 PLACEHOLDER and eight POSSIBLY_REAL. All eight P1 records are in backend history. No CONFIRMED_REAL/P0 was established; no scan proves absence. Current private credential paths are untracked. See [full inventory and limits](IMAGINO_SECRET_HISTORY_AUDIT.md).

The operator confirmed legacy Render My Workspace / imagino-api on master, with legacy Atlas Imagino.ai / imagino-cluster. Specific RunPod, Replicate and Stripe account/project/status remain UNKNOWN. No new staging consumer is assumed. Local signature comparison against a legitimately issued AI staging JWT matched none of the historical signing candidates; historical Mongo hosts differ from the authorized staging host. This proves only the stated mismatch, not revocation elsewhere. No credential values/fingerprints were persisted or emitted, no old credential was submitted to a provider, and no rotation/revocation/history rewrite occurred.

Both launch branches now include a staged-index secret check, redaction/detection regression tests, an opt-in pre-commit hook and least-permission CI. This check prevents detected new literals; it does not remediate public history or prove GitHub branch protection is configured. See [secret hygiene](IMAGINO_SECRET_HYGIENE.md).

## Secret-name inventory and sequence
Development/AI staging/future production: ImageGeneratorSettings__MongoConnection; Jwt__Secret; R2Settings__AccessKeyId; R2Settings__SecretAccessKey; GenerationV2__BflApiKey; GenerationV2__OpenAiApiKey; GenerationV2__RunwayApiKey. Optional general staging/future production only: Google client ID/secret, Resend API key, Stripe API/webhook secrets and legacy provider keys. Provider settings and finite authorization flags are configuration, not secrets. Exact deployed secret inventory/scope/duplicate-key validity still needs the operator; do not infer from configured names.
Before launch: identify owners/scopes/consumers and historical exposure privately. The current task stops at inventory and a prepared plan, as explicitly requested by the operator. Any future credential operation requires separate approval for the exact environment and follows replacement, install, disabled-provider smoke, new-credential confirmation, then old-credential retirement and safe rejection proof. JWT session/refresh impact needs its own reviewed sequence. Do not change production or new staging credentials by association alone. See [rotation plan](IMAGINO_SECRET_ROTATION_PLAN.md).

Legacy generation/billing surfaces must remain excluded from the production launch profile; AIStaging controller convention already excludes them. Production GenerationV2 remains deliberately staging-restricted. Opening production needs a separate reviewed profile, not removal of guards ad hoc.

Temporary protected Preview access was explicitly authorized for the browser gate. One alias metadata response included its bypass identifier in this private operator conversation before filtering was corrected. It is excluded from Git/docs; the local access file is removed after the gate. The newly created link expires 2026-10-08T20:15:49Z. General deployment protection remains enabled; application ownership/auth still gates private assets. No provider/production secret was rotated or revoked.
