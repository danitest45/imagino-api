# OpenAI GPT Image 2.5 — staging adapter and financial preflight

Status: **mock contract/software checks PASS; real provider homologation BLOCKED / NOT EXECUTED.**

Reviewed on 6 October 2026. Authorization remains at most three paid POST attempts and an absolute aggregate US$0.50 usage cap. This pass has sent **0 paid OpenAI requests, US$0**. No credential was created/copied, no credit purchase or auto-recharge occurred, and no paid flag was opened.

## Confirmed official contract and planned calls

| Call | Snapshot | Endpoint | Input |
| --- | --- | --- | --- |
| 1, Fast | `gpt-image-2.5-flare-2026-09-08` | POST /v1/images/generations | Exact BFL Fast perfume brief |
| 2, Studio | `gpt-image-2.5-sunburst-2026-09-08` | POST /v1/images/generations | Exact BFL Studio car brief |
| 3, Reference | `gpt-image-2.5-sunburst-2026-09-08` | POST /v1/images/edits | Exact controlled BFL reference and preservation brief |

[Flare](https://developers.openai.com/api/docs/models/gpt-image-2.5-flare) and [Sunburst](https://developers.openai.com/api/docs/models/gpt-image-2.5-sunburst) list these snapshots and endpoints. All three planned calls are 1024×1024, medium explicitly, n=1, PNG, opaque, stream=false, without partial images, mask, input_fidelity, extra variations or external tool.

The [generation schema](https://developers.openai.com/api/reference/resources/images/methods/generate) returns base64 output. The [edit JSON schema](https://developers.openai.com/api/reference/resources/images/methods/edit) accepts an images array of image_url data URLs. Legacy response_format and unsupported fidelity parameters are omitted. No Files upload, signed reference URL, Responses API, provider job ID or polling is invented.

The [image guide](https://developers.openai.com/api/docs/guides/image-generation) directs GPT Image 2.5 measurement through response usage. The API reference's usage description still mentions GPT Image 1; actual 2.5 usage remains to be confirmed in a real response. The adapter requires consistent nonnegative integral totals and modality counts and rejects unpriced output modalities. Missing/inconsistent usage halts the run; it never substitutes estimates for observed consumption.

Public model pages list Tier 1 100,000 TPM and 5 IPM. Actual account access/limits remain unknown.

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

The official calculator's medium grid=24 projects:

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

## Human credential/billing handoff — no key requested yet

When the financial prerequisite is resolved and the closed adapter is deployed:

1. In the chosen OpenAI API project, the user creates a key on [API keys](https://platform.openai.com/api-keys), as described by the [official quickstart](https://developers.openai.com/api/docs/quickstart). The agent must not create/copy it; no key is sent in chat.
2. GPT Image access **may** require API Organization Verification according to the image guide. The current account's verification state has not been inspected.
3. Check API [billing](https://platform.openai.com/settings/organization/billing) and model access/limits manually. The [rate-limit guide](https://developers.openai.com/api/docs/guides/rate-limits) lists Tier 1 qualification as US$5 paid; this is not evidence that this account needs a new US$5 purchase. The [error guide](https://developers.openai.com/api/docs/guides/error-codes) documents prepaid-credit exhaustion. Payment method, balance and any required purchase remain account-specific and unverified.
4. If a purchase is required, stop and report the displayed amount/options before purchase. The US$0.50 usage cap authorizes no prepaid purchase or auto-recharge.
5. Only the user inserts GenerationV2__OpenAiApiKey in [the existing AI staging Render Environment](https://dashboard.render.com/web/srv-db1tmv17lnhs73efdjp0/env). Never insert it into the older service, production, frontend, repository or local test run. Keep PaidGenerationEnabled=false. Do not add it while the live backend still lacks the closed-adapter configuration.

## Validation and remaining evidence

dotnet build: PASS, 0 errors, 25 existing nullable warnings. dotnet test: **280 passed, 0 failed, 0 skipped**, including 37 new OpenAI cases. [Sanitized test results](evidence/openai-gpt-image-25/tests.json) retain case names/outcomes without payloads.

Coverage: request mapping/generation/edit, exact size/quality, base64 bounds, malformed output, usage consistency/cache accounting/prices, HTTP/moderation category, timeout with one POST, unsupported snapshot/owner/reference, serialized finite slots, aggregate cap/fourth-call denial, unknown-bound closure, catalog serialization, synchronous storage-before-settlement/failure refund and restart without reposting. Existing async-provider and ownership checks continue passing.

These tests use simulated HTTP, repository and storage boundaries plus BSON round trips. Mongo transaction/restart behavior of the new ledger and real R2/OpenAI settlement have **not** been exercised live. The original exactly-once wallet transaction/CreditState filter is preserved; there is no new wallet replacement. No real OpenAI idempotency guarantee is asserted.

The current live runtime/deployment and health/catalog checks are recorded in preflight.json. Render PaidGenerationEnabled=false was observed by revealing only this non-secret boolean; other credential values stayed masked. No configuration was edited. Screenshot capture was unavailable; no screenshot evidence is claimed.

All three real calls, usage/cost/latency/bytes/dimensions, jobs/credits/outputs, side-by-side BFL comparison and product recommendations remain **NOT EXECUTED / N/A**. The earlier BFL results remain US$0.014 / 0.030 / 0.045, total US$0.089 and 5 / 10 / 15 credits. No OpenAI winner or Gemini recommendation follows from mock tests.

PR #56 is the sole backend continuation. PR #88 is unchanged. No production, old Render service, Stripe, Gemini, video, Compare or merge.

