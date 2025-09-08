# Error Responses

This document lists the HTTP status codes returned by the API along with their corresponding `code` values from `ErrorCodes`.

| HTTP Status | Code | Scenario |
|-------------|------|---------|
| 401 | TOKEN_EXPIRED | Token inválido ou expirado |
| 402 | INSUFFICIENT_CREDITS | Usuário sem créditos suficientes |
| 403 | FORBIDDEN_FEATURE | Plano não permite recurso |
| 400 | VALIDATION_FAILED | Erros de validação ou recursos não encontrados |
| 409 | CONFLICT | Conflito de estado |
| 429 | RATE_LIMITED | Limite de requisições atingido |
| 502/503 | GENERATION_UPSTREAM_ERROR | Falha em providers de geração (RunPod/Replicate) |
| 500 | STORAGE_UPLOAD_FAILED | Falha ao enviar arquivo para storage |
| 400 | STRIPE_ERROR | Erros na API da Stripe |
| 400 | WEBHOOK_SIGNATURE_INVALID | Assinatura de webhook inválida |
| 500 | WEBHOOK_PROCESSING_FAILED | Falha ao processar webhook |
| 500 | INTERNAL | Erro interno não tratado |
