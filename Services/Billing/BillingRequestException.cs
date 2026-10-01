namespace Imagino.Api.Services.Billing;
public sealed class BillingRequestException(int status, string message) : Exception(message) { public int Status { get; } = status; }
