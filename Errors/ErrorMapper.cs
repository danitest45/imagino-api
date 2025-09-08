using Imagino.Api.Errors.Exceptions;
using Microsoft.AspNetCore.Http;

namespace Imagino.Api.Errors;

public static class ErrorMapper
{
    public static (int status, string code, string title, string? detail, object? meta) Map(Exception? ex)
    {
        return ex switch
        {
            InsufficientCreditsException ice => (
                StatusCodes.Status402PaymentRequired,
                ErrorCodes.INSUFFICIENT_CREDITS,
                "Insufficient credits",
                $"Need {ice.Needed} credits, have {ice.Current}.",
                new { current = ice.Current, needed = ice.Needed }),

            TokenExpiredException => (
                StatusCodes.Status401Unauthorized,
                ErrorCodes.TOKEN_EXPIRED,
                "Token expired",
                "Token has expired.",
                null),

            TokenInvalidException => (
                StatusCodes.Status401Unauthorized,
                ErrorCodes.TOKEN_INVALID,
                "Token invalid",
                "Token is invalid.",
                null),

            ForbiddenFeatureException ffe => (
                StatusCodes.Status403Forbidden,
                ErrorCodes.FORBIDDEN_FEATURE,
                "Forbidden feature",
                ffe.Message,
                new { feature = ffe.Feature, requiredPlan = ffe.RequiredPlan }),

            ValidationAppException ve => (
                StatusCodes.Status422UnprocessableEntity,
                ErrorCodes.VALIDATION_FAILED,
                "Validation failed",
                ve.Message,
                new { errors = ve.Errors }),

            RateLimitException rl => (
                StatusCodes.Status429TooManyRequests,
                ErrorCodes.RATE_LIMITED,
                "Too many requests",
                rl.Message,
                new { retryAfter = rl.RetryAfterSeconds }),

            StorageUploadException sue => (
                StatusCodes.Status502BadGateway,
                ErrorCodes.STORAGE_UPLOAD_FAILED,
                "Storage upload failed",
                sue.Message,
                new { key = sue.Key, reason = sue.Reason }),

            UpstreamServiceException usex => MapUpstream(usex),

            WebhookSignatureException => (
                StatusCodes.Status400BadRequest,
                ErrorCodes.WEBHOOK_SIGNATURE_INVALID,
                "Webhook signature invalid",
                "Webhook signature invalid.",
                null),

            WebhookProcessingException wpe => (
                StatusCodes.Status500InternalServerError,
                ErrorCodes.WEBHOOK_PROCESSING_FAILED,
                "Webhook processing failed",
                wpe.Message,
                new { eventId = wpe.EventId }),

            ConflictAppException ce => (
                StatusCodes.Status409Conflict,
                ErrorCodes.CONFLICT,
                "Conflict",
                ce.Message,
                null),

            NotFoundAppException nfe => (
                StatusCodes.Status404NotFound,
                ErrorCodes.NOT_FOUND,
                "Not found",
                nfe.Message,
                null),

            _ => (
                StatusCodes.Status500InternalServerError,
                ErrorCodes.INTERNAL,
                "Internal server error",
                ex?.Message,
                null)
        };
    }

    private static (int status, string code, string title, string? detail, object? meta) MapUpstream(UpstreamServiceException usex)
    {
        return usex.Provider switch
        {
            "Stripe" => (
                usex.ProviderCode switch
                {
                    "payment_required" => StatusCodes.Status402PaymentRequired,
                    "conflict" => StatusCodes.Status409Conflict,
                    _ => StatusCodes.Status400BadRequest
                },
                ErrorCodes.STRIPE_ERROR,
                "Stripe error",
                usex.Message,
                new { provider = usex.Provider, providerCode = usex.ProviderCode }),
            "RunPod" or "Replicate" => (
                StatusCodes.Status502BadGateway,
                ErrorCodes.GENERATION_UPSTREAM_ERROR,
                $"{usex.Provider} error",
                usex.Message,
                new { provider = usex.Provider, providerCode = usex.ProviderCode }),
            _ => (
                StatusCodes.Status503ServiceUnavailable,
                ErrorCodes.GENERATION_UPSTREAM_ERROR,
                $"{usex.Provider} error",
                usex.Message,
                new { provider = usex.Provider, providerCode = usex.ProviderCode })
        };
    }
}
