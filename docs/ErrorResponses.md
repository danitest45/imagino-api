# Error Responses

| HTTP Status | Code | Scenario | Meta |
|-------------|------|----------|------|
| 401 | `TOKEN_EXPIRED` | JWT expired | - |
| 401 | `TOKEN_INVALID` | JWT invalid | - |
| 402 | `INSUFFICIENT_CREDITS` | User without credits | `{ current, needed }` |
| 403 | `FORBIDDEN_FEATURE` | Plan lacks feature | `{ feature, requiredPlan }` |
| 404 | `NOT_FOUND` | Resource not found | - |
| 409 | `CONFLICT` | Conflict detected | - |
| 422 | `VALIDATION_FAILED` | Model validation or explicit validation errors | `{ errors }` |
| 429 | `RATE_LIMITED` | Too many requests | `{ retryAfter }` |
| 500 | `WEBHOOK_PROCESSING_FAILED` | Failure processing webhook | `{ eventId }` |
| 500 | `INTERNAL` | Unhandled errors | - |
| 502 | `STORAGE_UPLOAD_FAILED` | Failed to upload to storage | `{ key, reason }` |
| 502 | `GENERATION_UPSTREAM_ERROR` | RunPod/Replicate error | `{ provider, providerCode }` |
| 400/402/409 | `STRIPE_ERROR` | Stripe API error | `{ provider, providerCode }` |
| 400 | `WEBHOOK_SIGNATURE_INVALID` | Invalid webhook signature | - |
