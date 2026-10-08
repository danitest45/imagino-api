# Security review
Reference: 2026-10-07. Scope: isolated launch worktrees and authorized AI staging.

## Findings
| Priority | Finding | Response |
|---|---|---|
| P0 | Historical image objects publicly readable through staging r2.dev | B1 resolved for all eight known URLs: Cloudflare HEAD/GET 401 after operator disabled Public Development URL; private owner media/download hashes and authorization negatives passed. Other custom-domain/Worker inventory remains unverified in B7 |
| P0 | Provider price/config drift and concurrent budget overspend | Fail-closed reviewed-cost metadata, atomic reservations, emergency stop default true |
| P0 | Backend Git history contains credential candidates | Names/blob identifiers only in evidence; review and approved rotation required |
| P1 | Cookie refresh/logout relied on CORS alone | Exact Origin check when refresh cookie is present; missing/evil Origin rejected |
| P1 | Distributed endpoint abuse and per-job path bypass | Mongo TTL counters for IP/account/email; normalized media/job scopes; fail closed on Mongo failure |
| P1 | Email token replay | Atomic consume of unexpired, unused token; trial stays disabled |
| P1 | Row-only account delete leaves media/sessions/jobs | Self-delete returns review-required 409; foreign 404; no records destroyed |
| P1 | Raw provider/storage errors | Generic response, no raw body/meta; privacy regression test and safe operational error logging |
| P2 | Full npm audit: five high development lint-chain findings | Runtime audit clean; restrict lint to trusted repository input; revisit patched braces upstream |
| P2 | Repository evidence growth | Preserve base review evidence; future screenshots/traces in release artifacts, compact summaries in Git |

Backend history heuristic scan: 807 unique text blobs, 41 Mongo credential-pattern candidates and five literal-secret candidates. These counts include versions/placeholders and do not establish active credentials or distinct keys. Frontend: 669 blobs, zero candidates. Neither scan proves absence. No secret value emitted, no rotation or history rewrite. Current private credential paths are untracked.

## Secret-name inventory and sequence
Development/AI staging/future production: ImageGeneratorSettings__MongoConnection; Jwt__Secret; R2Settings__AccessKeyId; R2Settings__SecretAccessKey; GenerationV2__BflApiKey; GenerationV2__OpenAiApiKey; GenerationV2__RunwayApiKey. Optional general staging/future production only: Google client ID/secret, Resend API key, Stripe API/webhook secrets and legacy provider keys. Provider settings and finite authorization flags are configuration, not secrets. Exact deployed secret inventory/scope/duplicate-key validity still needs the operator; do not infer from configured names.
Before launch: identify owners/scopes and historical exposure privately; provision distinct production credentials; test disabled-provider deployment; obtain explicit approval; rotate suspected exposed keys one integration at a time; smoke read-only; revoke old keys; invalidate compromised sessions if JWT implicated; audit access; only then discuss history cleanup. Never reuse staging credentials.

Legacy generation/billing surfaces must remain excluded from the production launch profile; AIStaging controller convention already excludes them. Production GenerationV2 remains deliberately staging-restricted. Opening production needs a separate reviewed profile, not removal of guards ad hoc.

Temporary protected Preview access was explicitly authorized for the browser gate. One alias metadata response included its bypass identifier in this private operator conversation before filtering was corrected. It is excluded from Git/docs; the local access file is removed after the gate. The newly created link expires 2026-10-08T20:15:49Z. General deployment protection remains enabled; application ownership/auth still gates private assets. No provider/production secret was rotated or revoked.
