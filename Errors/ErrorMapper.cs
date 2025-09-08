using Microsoft.AspNetCore.Mvc;

namespace Imagino.Api.Errors;

public static class ErrorMapper
{
    public static (int status, string code, string title, string detail, object? meta) Map(Exception ex)
    {
        return ex switch
        {
            InsufficientCreditsException ice => (402, ErrorCodes.INSUFFICIENT_CREDITS,
                "Insufficient credits", ice.Message, new { ice.Current, ice.Needed }),
            TokenExpiredException tex => (401, ErrorCodes.TOKEN_EXPIRED, "Token expired", tex.Message, null),
            ForbiddenFeatureException ffe => (403, ErrorCodes.FORBIDDEN_FEATURE, "Forbidden", ffe.Message, null),
            ValidationAppException ve => (400, ErrorCodes.VALIDATION_FAILED, "Validation failed", ve.Message, ve.Meta),
            RateLimitException rle => (429, ErrorCodes.RATE_LIMITED, "Rate limited", rle.Message, null),
            UpstreamServiceException use => (use.Status ?? 502, ErrorCodes.GENERATION_UPSTREAM_ERROR,
                "Upstream service error", use.Message, new { use.Provider, use.ProviderCode }),
            StorageUploadException sue => (500, ErrorCodes.STORAGE_UPLOAD_FAILED, "Storage upload failed", sue.Message,
                new { sue.Provider, sue.ProviderCode }),
            StripeServiceException sse => (400, ErrorCodes.STRIPE_ERROR, "Stripe error", sse.Message,
                new { sse.StripeCode }),
            WebhookSignatureException wse => (400, ErrorCodes.WEBHOOK_SIGNATURE_INVALID, "Invalid webhook signature", wse.Message, null),
            WebhookProcessingException wpe => (500, ErrorCodes.WEBHOOK_PROCESSING_FAILED, "Webhook processing failed", wpe.Message, null),
            ConflictAppException cae => (409, ErrorCodes.CONFLICT, "Conflict", cae.Message, null),
            _ => (500, ErrorCodes.INTERNAL, "Internal Server Error", ex.Message, null)
        };
    }
}
