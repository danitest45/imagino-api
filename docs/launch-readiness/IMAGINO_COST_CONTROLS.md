# Cost controls
No commercial credits/prices/budgets were enabled or finalized.

## Enforced contract
Each accepted job snapshots modelVersion, pricingVersion, provider/providerModel, quote/accepted credits, normalized pricing settings and accepted timestamp. HMAC-signed quotes bind owner and payload/expiry; catalog edits cannot reprice historical jobs or accepted idempotent replay.
GenerationCostControls defaults EmergencyStop=true and zero limits. Paid offers require enabled provider/model, matching versions/binding, human cost basis, ReviewedAtUtc, future ValidUntilUtc, positive expected and maximum request USD, allowed variance and daily model/provider/monthly provider limits. Missing/stale/mismatched data fails closed. No live price scraping.
One Mongo transaction reserves job, wallet, maximum approved request USD across provider UTC day/month/model day, user daily credits and user/video concurrency. Provider POST requires durable CAS marking attempted, live lease and a fresh current catalog/config review. Unknown/attempted cost remains encumbered even after user refund; unattempted maximum USD is released once. Concurrency closes once. Ledger/job reserve/charge/refund stays authoritative.
Synthetic concurrency evidence: ten workers, five admitted against 0.10 USD with 0.02 USD maximum each; no overspend; duplicate submission/settlement CAS rejected. No final budget values configured.

## Operations and limits
Emergency stop prevents new paid submissions; known-task GET/poll and stored-output settlement remain recoverable. Environment flag changes require deployment/restart in this hosting setup; code uses IOptionsMonitor but cannot magically refresh Render env. Version every approval. Daily rollover is UTC; retain unknown reservations across period boundaries until provider invoice/task evidence reconciles them. Current conservative implementation has no automatic budget release for an attempted job. Review maximum-vs-actual surplus manually; never edit counters casually or interpret credit refund as provider refund.

Known OpenAI pricing still lacks a verified hard complete-request ceiling; retain disabled. Finite BFL/OpenAI/Runway authorizations are historical staging contracts, not production offers. Current launch profile rejects PaidGenerationEnabled, OpenAiSingleSmokeEnabled and RunwayRealSmokeEnabled.

Trial remains off: proposal verified identity → ten credits/fourteen days/image only/two paid attempts/no video requires counsel/business approval. Future grant must use unique verified-identity key plus transactional immutable grant ledger/expiry/attempt counter; email token consume alone is not anti-farming. Options: no paid trial, waitlist/manual grant, or bounded verified trial with platform/user budgets. Never accept economic fields from client.
