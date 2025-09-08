namespace Imagino.Api.Errors.Exceptions;

public class ForbiddenFeatureException : Exception
{
    public string Feature { get; }
    public string RequiredPlan { get; }

    public ForbiddenFeatureException(string feature, string requiredPlan)
        : base($"Feature '{feature}' requires plan '{requiredPlan}'.")
    {
        Feature = feature;
        RequiredPlan = requiredPlan;
    }
}
