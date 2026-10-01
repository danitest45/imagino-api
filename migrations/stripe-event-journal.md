# Stripe event journal deployment gate

This is a deployment manifest, not an automatic production migration.
Do not run against production during Phase 0B.2D.

1. Pause billing webhook delivery during the migration window.
2. In the target database, preflight stripe_events by grouping EventId; require
   zero duplicate groups and no null/empty EventId. Do not delete conflicts.
3. Create { EventId: 1 } with name stripe_event_id_unique and unique: true,
   without sparse or partial options. Verify listIndexes reports unique: true.
4. Deploy the claim implementation, then resume delivery and replay one completed
   event. Confirm no second journal row or economic effect.

Only imagino_staging is authorized now. Production requires a separately
approved migration window and duplicate reconciliation plan.

The repository refuses processing without the full unique index. EventId is
also the new row's _id. Insert/DuplicateKey is the atomic claim; completed
duplicates return 200, in-progress duplicates return 503 for Stripe retry.
Failures retain minimal metadata and become retryable. A 5-minute expired
lease can be reclaimed atomically; completion/failure uses the claim ID.
Credit increments use a single Users update with invoice-credit-{invoiceId}
guard and AddToSet. A crash between credits and journal completion is safe
on retry. No raw payload or error detail is persisted.

Both invoice.paid and invoice.payment_succeeded remain enabled as requested.
Initial checkout and both invoice events share the same invoice credit key.
Only paid, nonzero, single-item initial/renewal invoices for known server
prices and quantity 1 grant allowances. Proration/upgrade/manual invoices do not.

Subscription state is reconciled from a fresh Stripe subscription. Event
created time never decreases; Users CAS on BillingRevision prevents stale
concurrent reads replacing newer state. Equal-second events reconcile current
Stripe state and retry CAS on contention. Canceled/incomplete_expired sets
Plan null and access Free; existing credits are preserved. No upgrade flow
is implemented: an existing nonterminal subscription blocks new checkout.

The TEST fixtures cover synchronous payment completion. Asynchronous payment
coverage, invoice.payment_failed policy and tax obligations/Stripe Tax remain
explicit pre-production decisions; no additional webhook types or tax settings
were activated in this phase.
