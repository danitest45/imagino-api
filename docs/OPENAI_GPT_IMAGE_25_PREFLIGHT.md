# OpenAI GPT Image 2.5 — staging adapter and financial preflight

Historical preflight record, completed before paid authorization. A later, separate single-call operator authorization was executed successfully; current paid count, usage and closed flags are documented in [OPENAI_FLARE_SINGLE_REAL_SMOKE.md](OPENAI_FLARE_SINGLE_REAL_SMOKE.md). The three-call plan below remains unexecuted and closed.

Status: **software checks and real model-metadata preflight PASS; paid image homologation BLOCKED / NOT EXECUTED.**

Reviewed on 6 October 2026. Authorization remains at most three paid POST attempts and an absolute aggregate US$0.50 usage cap. This pass has sent **0 paid OpenAI requests, US$0**. The user manually inserted the staging key into Render. Its field name was observed with the value masked; the successful backend metadata requests confirm a usable credential without exposing it. The agent has not created, copied or inspected its value, purchased credit or opened a paid flag. Funded API billing and disabled auto-recharge remain user-reported facts, not a billing-account inspection.

## Confirmed official contract and planned calls

| Call | Snapshot | Endpoint | Input |
| --- | --- | --- | --- |
| 1, Fast | `gpt-image-2.5-flare-2026-09-08` | POST /v1/images/generations | Exact BFL Fast perfume brief |
| 2, Studio | `gpt-image-2.5-sunburst-2026-09-08` | POST /v1/images/generations | Exact BFL Studio car brief |
| 3, Reference | `gpt-image-2.5-sunburst-2026-09-08` | POST /v1/images/edits | Exact controlled BFL reference and preservation brief |

[Flare](https://developers.openai.com/api/docs/models/gpt-image-2.5-flare) and [Sunburst](https://developers.openai.com/api/docs/models/gpt-image-2.5-sunburst) list these snapshots and endpoints. All three planned calls are 1024×1024, medium explicitly, n=1, PNG, opaque, stream=false, without partial images, mask, input_fidelity, extra variations or external tool.

The [generation schema](https://developers.openai.com/api/reference/resources/images/methods/generate) returns base64 output. The [edit JSON schema](https://developers.openai.com/api/reference/resources/images/methods/edit) accepts an images array of image_url data URLs. Legacy response_format and unsupported fidelity parameters are omitted. No Files upload, signed reference URL, Responses API, provider job ID or polling is invented.

The [image guide](https://developers.openai.com/api/docs/guides/image-generation) directs GPT Image 2.5 measurement through response usage. The API reference's usage description still mentions GPT Image 1; actual 2.5 usage remains to be confirmed in a real response. The adapter requires consistent nonnegative integral totals and modality counts and rejects unpriced output modalities. Missing/inconsistent usage halts the run; it never substitutes estimates for observed consumption.

The current public model pages list Free as unsupported and Build at 250,000 TPM / 20 IPM for both snapshots. This supersedes the earlier Tier 1 figures in this report. Actual project limits, verification and image-endpoint permissions remain unknown; public tier figures do not prove account access.

## Software implemented without spending

- Native OpenAiImageGenerationProvider follows IGenerationProvider and sends only the two approved synchronous Images endpoints.
- Shared worker handles an inline completed result through durable numeric usage/cost recording, PNG magic/IHDR/1024×1024 validation, existing R2 staging storage, then existing transactional settlement. Async provider binding/polling remains intact.
- Numeric usage and cost are persisted before storage; invalid base64 or missing single output retains known usage and refunds rather than charging. Missing usage, timeouts or ambiguous submissions halt subsequent slots. No raw provider response/base64/key is persisted or logged.
- The Mongo ledger uses one stable run ID and three slots, setOnInsert initialization, transactionally shared wallet/job reservation, an atomic submission-attempt marker, and serialized monetary decimals. Restart never resets attempted slots. Previous completion/reconciliation and aggregate committed/observed costs gate each next call. Any failure or third completion closes the run.
- Owner, exact briefs, snapshot/version, settings, fixed idempotency keys and reference SHA-256 match the BFL benchmark. Only service srv-db1tmv17lnhs73efdjp0, AIStaging profile, codex/imagino-ai-revival-v2 and imagino-api-ai-staging.onrender.com are accepted.
- Versioned experimental OpenAI Fast and OpenAI Studio & Edit catalog entries preserve BFL. Shared frontend fields render size/quality/outputFormat and existing reference capabilities without frontend code changes.
- The catalog's 5-credit starting figure is explicitly an **output-only planning estimate**, excluding text/reference input. It is neither a full quote nor observed cost. Reservable quotes fail closed until complete request maxima are verified.
- PaidGenerationEnabled=false is required outside the controlled window. OpenAI registration requires its separate authorization flag; the configuration rejects paid OpenAI authorization while bounds are unknown. There is no env-var override that can replace an authoritative maximum with an arbitrary estimate.

Run ID: openai-three-calls-20261006. Expiry: 2026-10-08T00:00:00Z. This source expiry is not permission to spend or reset the ledger.

## Token pricing, projection and experimental credits

[Standard pricing](https://developers.openai.com/api/docs/pricing), checked 2026-10-06, is identical for both snapshots:

| Modality | USD / million tokens |
| --- | ---: |
| Text input | 5 |
| Image input | 8 |
| Image output | 30 |

Cached discounts are not applicable to direct Images API requests, including edits. Cached numeric counts, when returned, are retained for audit without discounting.

`observed_USD = (5 × text_input_tokens + 8 × image_input_tokens + 30 × image_output_tokens) / 1,000,000`

The image-generation guide's calculator has a dedicated **GPT Image 2.5 (Sunburst and Flare)** selection. Its medium grid=24 projects:

`ceil(24 × 24 × (2,000,000 + 1024 × 1024) / 4,000,000) = 439 output tokens`

This gives US$0.01317 per output and **US$0.03951 for three outputs**, excluding all inputs. The calculator asset was read, not executed. Its URL/hash is in [preflight.json](evidence/openai-gpt-image-25/preflight.json). These are estimates, not guaranteed maxima or real costs.

The existing experimental planning formula is implemented separately:

`ceil((observed_USD × 1.10 + 0.002) / ((1 − 0.65) × 0.01))`

Provision/overhead is planning cost, not an OpenAI fee. Mock usage of 50 text input, 100 image input and 439 output tokens yields US$0.01422 and 6 experimental credits. That test sample is not a benchmark observation. No commercial Imagino or Stripe prices change.

## Blocking financial condition

The [image input cost documentation](https://developers.openai.com/api/docs/guides/images-vision#calculating-costs) separates GPT Image input accounting from general vision rules, but the consulted sources do not establish a maximum billable input count for this reference on the two GPT Image 2.5 snapshots. Older-model rules cannot prove the requested edit's maximum.

Direct Images schemas have no max_output_tokens or total-dollar/token request budget. The [spend-limit guide](https://developers.openai.com/api/docs/guides/spend-limits) says enforcement can lag and spend may exceed a hard threshold. An account hard limit or prepaid balance is not an absolute zero-exceedance request guarantee.

Therefore a complete bound satisfying `already_spent + next_request_maximum <= US$0.50` is unverified. VerifiedMaximumUsd deliberately remains null for all three slots, closing availability, quote, reservation, worker dispatch and paid configuration. After-response cost checks cannot prevent in-flight overspend.

**Do not send a paid POST or raise the authorized cap.** Before the real window, obtain authoritative complete applicable cost bounds/enforceable request controls and review the resulting fixed maxima. Then complete the human credential/billing gate. Only after both gates can the three calls proceed, with actual reconciliation between them.

## Human credential handoff and metadata preflight

The user has already created the key and configured funded API billing with auto-recharge disabled. No new key or purchase is requested. GPT Image access **may** require API Organization Verification according to the image guide; the current account's state has not been inspected. Any further purchase remains a separate human decision.

The following fields are now configured in [the existing AI staging Render Environment](https://dashboard.render.com/web/srv-db1tmv17lnhs73efdjp0/env). The user inserted the key; the agent added the missing non-secret authorization flag and enforced the already-false paid flag through a merge-only environment update, without retrieving existing secrets:

| Environment key | Value |
| --- | --- |
| GenerationV2__OpenAiApiKey | Inserted manually by the user; its value stays masked |
| GenerationV2__OpenAiHomologationEnabled | true for this metadata preflight |
| GenerationV2__PaidGenerationEnabled | false |

The key and authorization flag must be configured together: the existing startup guard rejects an OpenAI key with its authorization flag false. Keeping the paid flag false leaves generation closed; complete-cost bounds remain unverified in source. No key was inserted into another service, frontend, repository or local test environment. The clipboard and Render credential value were not accessed.

An authenticated synthetic-owner-only `GET /api/generation/openai/preflight` now performs at most one metadata GET per approved snapshot through [Retrieve model](https://developers.openai.com/api/reference/resources/models/methods/retrieve):

- GET https://api.openai.com/v1/models/gpt-image-2.5-flare-2026-09-08
- GET https://api.openai.com/v1/models/gpt-image-2.5-sunburst-2026-09-08

The probe requires the exact AIStaging service/branch/hostname, OpenAI authorization and a **closed paid flag**. Foreign owners and other scopes receive 404 before network access. It returns only model IDs, HTTP statuses, sanitized categories and a timestamp; keys, headers and raw provider responses are neither returned nor logged. Each GET has a 15-second deadline and no retry. It has no job, wallet or storage dependency and sends no image-generation POST. Responses are private/no-store.

The real preflight at **2026-10-06T21:41:26.7425073Z** returned matching metadata with **HTTP 200 for both snapshots**. Exactly two upstream GETs were attempted, with no retry or paid POST. Successful matching model metadata confirms metadata access only. It does **not** establish Images API permission, organization verification, available quota, actual image rate limits, successful billing or a full cost maximum. `imageEndpointAccessVerified` and `costBoundsVerified` remain false.

Current deploy **dep-db2ml81ca7us73fkjfs0** is Live at SHA **2cad86a2bc2aadb87704bd7dd58c2e6377707fbb**, finished 2026-10-06T21:40:57.702261Z. The environment update also initiated deploy dep-db2ml49srm7s73c17dkg; the subsequent explicit redeploy is the current Live instance. Source code is unchanged from the 299-test pass. The preflight's authenticated HTTP 200 demonstrates that its exact runtime/owner/OpenAI-flag/closed-paid prerequisites all passed.

Postchecks returned health/catalog 200 and both offers approval_required. The isolated synthetic HTTP session returned login/logout 200; browser sessions were not changed. The known owner retained **16 credits and the same eight job IDs** before/after. Only model metadata was requested. [Sanitized live results](evidence/openai-gpt-image-25/model-access.json).

Application logs since 21:39Z: 44 entries, hasMore=false, zero failure prefixes and four existing DataProtection warnings across the two restarts. No listed credential/payload/private-URL/full-brief marker matched. This is a marker check, not exhaustive secret validation.

## Validation and remaining evidence

dotnet build: PASS, 0 errors (25 existing API nullable warnings on the original full compile; the latest incremental API build is clean). dotnet test: **299 passed, 0 failed, 0 skipped**, including 37 adapter cases and 19 new metadata-preflight cases. [Sanitized test results](evidence/openai-gpt-image-25/tests.json) retain case names/outcomes without payloads. Metadata coverage verifies fixed GET routes/no bodies, exact owner/runtime/closed-paid guards, missing keys without network access, HTTP errors without raw output or retry, malformed/mismatched metadata and sanitized timeouts. All HTTP tests use mock keys/handlers.

Coverage: request mapping/generation/edit, exact size/quality, base64 bounds, malformed output, usage consistency/cache accounting/prices, HTTP/moderation category, timeout with one POST, unsupported snapshot/owner/reference, serialized finite slots, aggregate cap/fourth-call denial, unknown-bound closure, catalog serialization, synchronous storage-before-settlement/failure refund and restart without reposting. Existing async-provider and ownership checks continue passing.

These tests use simulated HTTP, repository and storage boundaries plus BSON round trips. Mongo transaction/restart behavior of the new ledger and real R2/OpenAI settlement have **not** been exercised live. The original exactly-once wallet transaction/CreditState filter is preserved; there is no new wallet replacement. No real OpenAI idempotency guarantee is asserted.

The original closed-adapter deploy **dep-db2jas1srm7s73bkqgvg** was Live at source SHA **de47b5875696f93e0329ff490f70f0f301ed6232**, finished 2026-10-06T17:53:41.913554Z, and has now been superseded. Its health/catalog checks returned 200; catalog revision 2026-10-06.1 contained eight offers, including two OpenAI approval_required offers and two unchanged BFL offers. That earlier adapter pass changed no environment configuration. Render PaidGenerationEnabled=false was observed again during this metadata pass by revealing only that non-secret boolean, then masking it; credential values stayed masked. The temporary agent tab was closed. Separate earlier Working Studio screenshots below succeeded.

The live protected Working Studio Preview renders both OpenAI offers as Awaiting activation. Selecting Sunburst displays 1024x1024 / medium / png, reference capability, the exact snapshot and the output-only estimate caveat through existing generic controls. Create stays disabled and current cost is blank. The existing synthetic session still shows 16 credits and eight prior creations; the selected result is the earlier BFL reference image, not an OpenAI output. No upload, quote or generation was requested. Initial Fast Image selection/details were restored and the agent's temporary tab closed. [Catalog proof](evidence/openai-gpt-image-25/catalog-approval-required.png) and [Sunburst controls proof](evidence/openai-gpt-image-25/studio-closed-offer.png).

Deployment application logs: 21 entries since 17:53Z, hasMore=false; 0 failure prefixes, 2 warning prefixes (2 DataProtection). No listed credential, image/base64, signed-URL or benchmark-prompt marker matched. These are marker checks, not an exhaustive proof of every possible secret.

All three real calls, usage/cost/latency/bytes/dimensions, jobs/credits/outputs, side-by-side BFL comparison and product recommendations remain **NOT EXECUTED / N/A**. The earlier BFL results remain US$0.014 / 0.030 / 0.045, total US$0.089 and 5 / 10 / 15 credits. No OpenAI winner or Gemini recommendation follows from mock tests.

PR #56 is the sole backend continuation. PR #88 is unchanged. No production, old Render service, Stripe, Gemini, video, Compare or merge.

