# OpenAI Flare: single paid staging smoke

Status: prepared; no paid OpenAI POST submitted yet.

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

Real results and closure evidence will be appended after the single call; no second call is authorized.
