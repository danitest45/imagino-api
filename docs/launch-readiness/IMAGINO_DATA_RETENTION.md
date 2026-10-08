# Data retention and deletion
DRAFT — LEGAL REVIEW REQUIRED. Proposals below are inactive; no production deletion.

| Class | Proposed retention | Constraint |
|---|---|---|
| Paid generated assets | Until account deletion or published plan policy | Never silently expire purchased outputs; policy approval needed |
| Trial assets | Trial end plus approved grace interval | Trial off; disclose expiry before any grant |
| Temporary references | Seven days after terminal job, unless reuse/policy requires longer | Private; active jobs always retain; current GC retains all known jobs |
| Failed-job inputs | Seven days after terminal state | Unknown submissions/operator evidence may require hold |
| Provider temporary URLs | Only while binding/download/reconciliation requires | Never user-facing; provider retention is separate |
| Application/security logs | Proposed 14/30 days | Approve purpose, access and incident/legal holds |
| Billing/financial records | Jurisdiction-dependent approved schedule | Counsel/fiscal decision; no cascade delete |
| Pricing/budget/audit ledgers | Approved accounting/incident period | Immutable financial reconciliation; no TTL yet |

Safe staging GC: admin-only `GET /api/generation/operations/media-gc` scans at most 500 objects per prefix (1,000 total), records truncation, validates generation-v2/generation-inputs owner+job IDs, seven-day minimum age, and proposes only objects with no job. Every known job is retained, including active/Completed/Failed. No delete capability shipped. Do not treat a truncated sample as complete inventory. Deletion mode later requires explicit operator flag, approved retention, dry-run manifest and recheck immediately before delete.

Account deletion is intentionally gated: row-only API delete now returns 409 for owner and 404 for foreign. Proposed workflow: authenticate/support identity → mark pending/revoke sessions → block new jobs → drain/reconcile reserved jobs → enumerate references/assets → delete approved private objects → provider deletion request if supported → remove/pseudonymize account/job personal data → retain only approved financial/security facts → audit completion. Define maximum completion time, appeals/holds and evidence. Until approval and implementation, disclose support-mediated deletion and do not claim automatic erasure.
