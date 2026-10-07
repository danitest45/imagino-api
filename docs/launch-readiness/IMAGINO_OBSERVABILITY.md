# Observability and incident responses
System.Diagnostics.Metrics meter `Imagino.Generation` v1 emits request count/status/group and seconds, provider/model generation attempts and settlements, queue/duration/storage seconds, provider cost estimate USD, charged credits and submission_unknown. Labels exclude prompt, owner/email, object key, token or full URL. Process metrics reset on restart; Mongo jobs/reservations are durable truth. The admin operational response also provides backlog and current approved provider daily/monthly limits and encumbered amounts; estimates are not invoices.
Admin-only staging `GET /api/generation/operations/stuck` lists at most 100 operational identities/reasons; reconcile is lease-CAS, journaled and never submits creation POST. GC is dry-run only. Liveness `/health/live` checks process; `/health/ready` checks Mongo with two-second bound and worker initialization/tick age. No health call contacts paid providers. Storage configuration/integrity is checked by media operations; readiness currently does not probe R2, so readiness alone does not prove storage.

## Alerts to bind before launch
| Signal | Proposed trigger | Action |
|---|---|---|
| Provider/technical refund spike | >=10 attempts/15m and >20% failures/refunds | Pause affected offer; inspect safe codes, provider status and storage |
| Budget consumption | 50/75/90% of approved daily/monthly maximum | 50 review trend, 75 reduce admission, 90 pause new offers; unknown stays held |
| Provider low prepaid balance | Provider read-only balance below approved reserve | Pause offer, finance review; never auto-top-up |
| Backlog/stuck | Oldest queued >5m or lease/deadline expired >2m | Check readiness; claim bounded reconciliation; no repeat POST |
| Mongo/readiness failures | Three consecutive checks/one minute | Stop admission; preserve jobs, restore dependency |
| Auth abuse | 429/failure spike above known baseline | Confirm trusted proxy mapping; investigate hashed identity counts |
| Unexpected exception | New error type or repeated 5xx | Inspect code/status/type/trace only; rollback if release correlated |

Thresholds are proposals, not measured SLOs or enabled notifications. Monitoring exporter, alert destination, operator and retention are not configured: P1 BLOCKED.
Structured logs permit trace/job/provider/model and sanitized error type/code; never serialize exceptions/provider bodies. HTTP private-client loggers removed; hosting request logs reduced to avoid query strings; no prompt/media/token labels. New-backend staging sample after deployment/private-copy gate: 65 application entries, no further page, zero scanned JWT/Bearer/credential-URI/secret-assignment/base64/signed-URL/provider-URL/prompt markers. Heuristic absence is not exhaustive assurance. Compact counts only are committed; raw logs stay out of evidence.
Analytics proposal: signup, first_generation_completed, asset_accepted, asset_reused, animate_prepared, returning_session, checkout_conversion. Fields: eventId, timestamp, schemaVersion, opaque pseudonymous actor, coarse provider/model/media type/result/credits bucket; no prompt, bytes, email, raw URL. Vendor/retention/consent approval deferred.
Post-browser verification sampled another 14 staging app log entries without further pages; scanned JWT/Bearer/credential URI/secret assignments/base64/signed URL/prompt markers were absent. Deployed quote limiter returned 429/Retry-After 60 on attempt 31 after 30 synthetic quotes; zero jobs and unchanged wallet/history. Exporter/alerts are still a launch gate.
