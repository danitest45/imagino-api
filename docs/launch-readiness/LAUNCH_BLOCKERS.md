# Launch blockers
TECHNICAL LAUNCH READINESS = BLOCKED. No readiness percentage.

| ID | Priority | Exact unresolved gate | Required next evidence/action |
|---|---|---|---|
| B1 | P0 | Historical staging image is unauthenticated-public (HEAD 200) | Deploy compatible branch; bounded private copy/CAS; verify all images; disable old r2.dev/custom-domain/Worker exposure; old URL denial |
| B2 | P1 | New backend branch not deployed, complete remote final gate not run | Operator changes only AI staging branch; connector lacks branch update and browser runtime initialization failed; run runbook with paid flags false |
| B3 | P0 | Backend history contains credential candidates; validity/revocation unknown | Private operator review and explicit rotation/revocation approval; no values or rotations in this task |
| B4 | P0 | Real production offer complete-request cost bounds and budgets not approved, especially OpenAI | Human reviewed maximum cost/version/expiry and production limits; remain disabled; future paid smoke needs separate authorization |
| B5 | P1 | Atlas snapshot/PITR and R2 independent restore/capacity not proved | Choose tier/RPO/RTO, restore to separate resources; measure large-media buffering/Range under actual tier |
| B6 | P1 | Metrics exporter/alert operator, proxy topology and scalable OAuth state unresolved | Bind monitoring/alerts; verify trusted IP mapping; choose single-instance or shared OAuth state and exercise deployed gates |
| B7 | P1 | Production isolation/domains/auth/email/legal/deletion policy not approved | Approve resources/credentials/DNS/Google/sender and retention workflow; current self-delete is gated |

Commercial gates are separate: fiscal/tax/live Stripe, final included credits/expiry/rollover/trial, legal terms/privacy/provider rights. See dedicated checklists. Production deployment, secrets/resources, DNS, paid calls and billing remain outside authorization.
Local code and tests cannot turn B1/B2 into PASS. Authenticated proxying does not revoke a previously public R2 object. No current proof is an approval to enable paid offers.
