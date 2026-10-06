# OpenAI Flare: single paid staging smoke

**OpenAI Flare real smoke = PASS.** Exactly one paid provider POST completed, with one settlement, zero application POST retries and one consumed slot. No other paid generation was performed.

The operator superseded the previous three-call strict-bound plan with one direct Images API call and a US$0.10 observed-use ceiling. No provider-enforced per-request hard cap is claimed. The old three-call ledger remains closed, unchanged and separate.

## Pre-POST pricing and projection

Official Standard pricing checked on 2026-10-06: text input US$5/million, image input US$8/million, image output US$30/million. Direct Images API does not receive cached-input discounts. Sources: [pricing](https://developers.openai.com/api/docs/pricing), [image-generation guide](https://developers.openai.com/api/docs/guides/image-generation), [Flare model](https://developers.openai.com/api/docs/models/gpt-image-2.5-flare).

The official dedicated GPT Image 2.5 calculator uses a medium quality grid of 24 and estimates 439 image-output tokens for 1024×1024: ceil(24×24×(2,000,000+1024×1024)/4,000,000). With a conservative planning allowance of 512 text-input tokens for the short fixed brief and zero input images, the projection is US$0.01573. This is an estimate, not a maximum or a cap. No indication of spending above US$0.10 was found for this configuration. The experimental quote is 6 Imagino credits using the existing formula ceil((provider cost×1.10+0.002)/((1−0.65)×0.01)); permanent catalog pricing is unchanged.

## Temporary authorization

- Service: imagino-api-ai-staging / srv-db1tmv17lnhs73efdjp0; AIStaging environment and existing staging branch, Mongo database and R2 bucket.
- Owner: existing synthetic staging owner 6ac038cb05509cea703277eb.
- POST /v1/images/generations; gpt-image-2.5-flare-2026-09-08.
- 1024×1024, medium, PNG, n=1, opaque background, stream=false; no partial images, references or edits.
- Exact BFL Fast perfume brief, verified by fingerprint.
- Ledger: openai-flare-single-smoke-20261006; exactly one persisted slot.
- Idempotency key: openai-flare-single-smoke-20261006-only-slot.
- PaidGenerationEnabled and OpenAiSingleSmokeEnabled must both be true temporarily. Other paid offers remain unavailable.
- Mongo reservation and wallet debit share one transaction. Submission authorization changes Reserved to SubmissionAttempted once before the provider POST. No provider HTTP retries or redirects. Failures, ambiguity, restart and settlement cannot reopen the slot.
- The ledger halts on any terminal result, including successful completion. Both runtime flags will then be false and health must return 200.

## Validation before opening the paid window

Backend build and 312 tests passed, zero failures/skips. Thirteen new cases cover exact scope/config/key, one-slot serialization, consumption/halt/expiry, closure, blocked other models, observed-ceiling failure, synchronous storage ordering and no polling. Existing HTTP error/timeout tests verify one POST attempt; existing restart tests verify no repost. Initial new-test assertion was corrected to accept the already unavailable migration-required offer as blocked; no runtime behavior changed for that correction.

## Real result

| Item | Observed result |
|---|---|
| Imagino job | 6ac56f781375278f112d5654 |
| Endpoint / snapshot | POST /v1/images/generations / gpt-image-2.5-flare-2026-09-08 |
| Configuration | 1024×1024, quality=medium, output_format=png, n=1, background=opaque, stream=false; no partial_images/reference/edit |
| Quote | HTTP 200; projected US$0.01573; 6 credits |
| Reservation and result | QueuedReserved → WorkerClaim → OpenAiPostAttemptAuthorized → SynchronousResponseReceived → OutputStored → CompletedCharged |
| Provider response | HTTP 200, one inline base64 image, decoded and validated before storage |
| Text input tokens | 46 |
| Image input tokens | 0 |
| Image output tokens | 439 |
| Other usage | input_tokens=46; output_tokens=439; total_tokens=485; cached input=0; output is entirely image tokens; no unpriced output modality |
| Real calculated provider cost | text: 46×5/1,000,000 = US$0.00023; input image US$0; output: 439×30/1,000,000 = US$0.01317; **total US$0.01340** |
| Observed-use ceiling | US$0.10; actual calculated usage is 13.4% of that ceiling; no hard per-request cap was claimed |
| Credits | 6 reserved, 6 charged, 0 refunded; owner balance 16→10; history 8→9 jobs |
| Actual-cost experimental suggestion | ceil((0.0134×1.10+0.002)/((1−0.65)×0.01)) = **5 credits**; hypothetical margin 66.52% at 5 credits; no permanent pricing change |

The cost uses actual returned token counts and the official Standard rates, not a balance delta or invoice. Cached input is normalized to zero when absent; raw cached-field presence was not retained, and no discount was applied. The six-credit settlement follows the approved planning quote; the five-credit calculation is only a suggestion based on actual usage. The direct API provides no provider job/polling ID, and none was invented. The legacy MaximumUsd=0 field in the new slot is unused; ProjectedUsd is an estimate and the old VerifiedMaximumUsd remains unknown.

## Latency and output

| Measurement | Seconds |
|---|---:|
| OpenAI POST, response body read and JSON parse | 19.4047114 |
| Numeric usage parse and base64 decode | 0.0967120 |
| PNG signature/IHDR/dimension validation | 0.0002378 |
| Decode plus validation | 0.0969498 |
| R2 storage | 1.8867941 |
| Server end-to-end, QueuedReserved to CompletedCharged | **22.412** |

Created 2026-10-06T22:00:24.619Z; completed 22:00:47.031Z. These server timestamps include queue, ledger, wallet and settlement overhead. The API-client elapsed time is unavailable because its creation-response read failed; it is not substituted for the server measurement. The provider POST timer includes reading the full inline response and JSON parsing; base64 decoding is separately timed.

Output: PNG, 1024×1024, **1,426,902 bytes**, SHA-256 **955f0e497567dec36fa8ff84dafa39d9ae022e657c860c5f6a4ca10540580bb4**. Signature and dimensions passed both worker validation and authenticated download verification. R2 bucket: imagino-images-staging; key: generation-v2/6ac038cb05509cea703277eb/6ac56f781375278f112d5654.png.

[Real staging R2 output](https://pub-56f86851d1884a3b8e7a73f1624e4239.r2.dev/generation-v2/6ac038cb05509cea703277eb/6ac56f781375278f112d5654.png).

## Working Studio, ownership and count proof

The existing Working Studio Preview shows OpenAI Fast (experimental), Completed, 6 credits / Charged and balance 10. The selected output and recent thumbnail are loaded at natural 1024×1024 dimensions. Library shows nine creations, with this same Completed/Charged job. Screenshots: evidence/openai-flare-single-smoke/working-studio.png and library.png. Frontend code/PR #88 were unchanged.

Owner authenticated download returned **200**, valid PNG and Cache-Control no-store, private. The existing foreign synthetic account received **404** for this job, its download and the operator proof endpoint. Both temporary HTTP sessions were logged out with 200; the existing browser session remained authenticated. These API 404s do not imply that the existing public R2 URL is private.

The final durable ledger has exactly one call, state Completed, reconciled=true, halted=true, haltReason=single_call_completed. There is exactly one OpenAiPostAttemptAuthorized, one SynchronousResponseReceived and one CompletedCharged in the job journal. Render logs contain one measured OpenAI result, one output storage and one settlement, with no generation failure. HTTP code has no retry handler or redirects. This proves one application-level provider POST for this run; it is not a raw network capture. Render incoming request logs were unavailable for the queried window. No second/fourth slot exists in this run, and the separate old three-call plan remains financially closed.

## Initial technical comparison with BFL klein call 1

| Criterion | OpenAI Flare, this call | Previous FLUX.2 klein call 1 |
|---|---|---|
| Prompt adherence | Cobalt glass perfume, beige stone, luxury composition and no text; added dry flowers and a stone on the left, with stronger directional light | Cobalt glass perfume, beige stone, simpler composition and softer studio lighting; closer to the requested minimal setting |
| Realism | Crisp glass edges/reflections and detailed stone texture; darker cobalt and large faceted cap | Convincing translucent glass, visible spray mechanism/tube and plausible stone/light |
| Unwanted text | None visible | None visible |
| Server end-to-end | 22.412 s | 8.717 s |
| Provider cost | US$0.01340 | US$0.01400 |
| Credits in the test | 6 charged; actual-cost suggestion 5 | 5 charged |

The Flare cost is US$0.00060 / 4.29% lower in this pair; its server elapsed time is approximately 2.57× longer. Both succeeded technically. One image per model is not a statistical quality or speed benchmark and does not justify a permanent catalog/pricing change. The existing BFL image was downloaded for inspection; there was no new BFL provider call.

## Bugs and limitations

The local PowerShell client reported InvalidOperationException while reading the single Imagino creation response. The job had already been committed and the worker submitted it once. The operator closed the paid flags immediately after this ambiguous client result and recovered the job/usage exclusively by GET; no POST was repeated. No matching backend generation error was present. The original client exception details were not retained, so its root cause remains undiagnosed; do not label it an OpenAI failure or claim a clean 202 receipt. execute.json records this client failure, while check.json and verify.json record the successfully recovered provider/storage/settlement result.

The preflight's conservative text planning allowance produced a six-credit quote versus the five-credit suggestion from measured cost; this is the expected fixed-quote settlement behavior, not a second debit. No live failure/refund/restart was deliberately induced during the sole paid call. Those protections are covered by existing mocked tests and the consumed durable ledger, which survived the closing redeploy. Actual organization/project limits were not queried; successful generation confirms this call's image access and billing ability only.

## Mandatory closure

**PaidGenerationEnabled=false and OpenAiSingleSmokeEnabled=false** confirmed by the live authenticated proof endpoint after deploy **dep-db2mv2ugekts73fiim70**, finished 2026-10-06T22:01:42.040033Z, runtime **7b4a18d8c9634a376c8b4687ca8feb7c09f59f00**. Health **200**. Flare, Sunburst, BFL and Gemini offers return approval_required; video offers remain migration_required. The only Flare ledger is halted and consumed, so even reopening an environment flag would not create a second provider attempt without new code/authorization and a different persisted run. No new authorization was created.

OpenAiHomologationEnabled stays true solely to retain the existing manually inserted staging key and read-only metadata/operational support; the real single-smoke permission is false. The key was never read or copied by the agent. Sunburst, edit, second generation, Gemini, Compare, video, Stripe and production were not executed. No merge. Work stops after documentation and PR evidence.

Evidence: verify.json contains final live flags, one-slot ledger, sanitized usage, journals, wallet state, download/foreign status, R2 URL and SHA-256; tests.json records 312 passing cases; logs.json contains four sanitized generation messages; ui.json and the two screenshots record frontend display. OPENAI_FLARE_OUTPUT.png is the authenticated owner download. BFL_KLEIN_PREVIOUS_OUTPUT.png is the existing benchmark download.
