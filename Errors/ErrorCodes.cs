namespace Imagino.Api.Errors;

public static class ErrorCodes
{
    public const string INSUFFICIENT_CREDITS = "INSUFFICIENT_CREDITS";     // 402
    public const string TOKEN_EXPIRED = "TOKEN_EXPIRED";                   // 401
    public const string TOKEN_INVALID = "TOKEN_INVALID";                   // 401
    public const string FORBIDDEN_FEATURE = "FORBIDDEN_FEATURE";           // 403
    public const string VALIDATION_FAILED = "VALIDATION_FAILED";           // 422
    public const string RATE_LIMITED = "RATE_LIMITED";                     // 429
    public const string STORAGE_UPLOAD_FAILED = "STORAGE_UPLOAD_FAILED";   // 502
    public const string GENERATION_UPSTREAM_ERROR = "GENERATION_UPSTREAM_ERROR"; // 502/503
    public const string STRIPE_ERROR = "STRIPE_ERROR";                     // 400/402/409
    public const string WEBHOOK_SIGNATURE_INVALID = "WEBHOOK_SIGNATURE_INVALID"; // 400
    public const string WEBHOOK_PROCESSING_FAILED = "WEBHOOK_PROCESSING_FAILED"; // 500
    public const string CONFLICT = "CONFLICT";                             // 409
    public const string NOT_FOUND = "NOT_FOUND";                           // 404
    public const string INTERNAL = "INTERNAL";                             // 500
}
