# Stripe launch checklist
BLOCKED. Stripe integration was not reopened; no products/prices/subscriptions/live keys or commercial configuration changed. Old staging fixtures are not launch pricing.

- Approve legal entity, fiscal registration, account eligibility and settlement bank/country.
- Approve final Creator/Pro/Studio products/prices and included credits, expiration/rollover/trial; no candidate amount is automatically live.
- Resolve USD display/charge vs BRL settlement, exchange/fees/refunds and customer currency disclosure.
- Decide tax responsibility/calculation/invoicing with fiscal counsel; do not infer from test fixtures.
- Separate test/live keys, prices, Customers and webhook endpoint/secrets; secret rotation is separate approval.
- Signed webhook validation; immutable event IDs/idempotency; retry/out-of-order reconciliation; ledger-backed grants must not double grant on event replay.
- Hosted Customer Portal, cancel/renew/proration/payment-failure/refund policy and account entitlement transitions.
- Checkout idempotency and server-authoritative product/price/credits mapping; reject client economic fields.
- Reconcile invoice/payment/refund/credit grant totals, manual finance workflow and alert owner.
- Controlled test gate before any separately authorized live billing smoke; disable new checkout as rollback while preserving existing records.
