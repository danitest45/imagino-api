# Email production checklist
Resend remains absent from AI staging. Local tests use synthetic sender and fixture recipients; this phase sent no email.

Before enabling real email:
- Approve sender identity, From/reply-to/support address and distinct production Resend credential/domain; configure and verify SPF/DKIM/DMARC under separate DNS authorization.
- Review verification/reset template branding, mobile/plain-text version, accessible link, expiry and generic account-existence wording. Links use approved HTTPS frontend origin; never include password/JWT/provider keys/media.
- Token is hashed, expires and consumed once; resend/reset/login durable limits complement identity/IP counts. Reset revokes refresh sessions. Define handling of token-consumed/mutation-failed retry through a fresh token request.
- Separate sandbox/test recipients and sender from production. Synthetic tests must never call production sender or use real customer addresses.
- Bind verified webhook handling for hard bounce/complaint/suppression, idempotent event IDs and support escalation; minimize event payload retention. Suppressed recipient must not get an endless resend loop.
- Test controlled verification/reset delivery, expired/replayed links, bounce/suppression, outage and retry without disclosing account existence.
- Record owner, delivery/error metrics and approved retention. Domain verification, deliverability and production webhook operations are still unproved.

Resend documents [account-wide suppression](https://www.resend.com/changelog/suppression-list-support). The checklist does not claim a deployed webhook or verified DNS.
