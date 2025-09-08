namespace Imagino.Api.Errors;

public static class ErrorCodes
{
    public const string INSUFFICIENT_CREDITS = "INSUFFICIENT_CREDITS";
    public const string TOKEN_EXPIRED = "TOKEN_EXPIRED";
    public const string FORBIDDEN_FEATURE = "FORBIDDEN_FEATURE";
    public const string VALIDATION_FAILED = "VALIDATION_FAILED";
    public const string RATE_LIMITED = "RATE_LIMITED";
    public const string GENERATION_UPSTREAM_ERROR = "GENERATION_UPSTREAM_ERROR";
    public const string STORAGE_UPLOAD_FAILED = "STORAGE_UPLOAD_FAILED";
    public const string STRIPE_ERROR = "STRIPE_ERROR";
    public const string WEBHOOK_SIGNATURE_INVALID = "WEBHOOK_SIGNATURE_INVALID";
    public const string WEBHOOK_PROCESSING_FAILED = "WEBHOOK_PROCESSING_FAILED";
    public const string CONFLICT = "CONFLICT";
    public const string INTERNAL = "INTERNAL";
}
