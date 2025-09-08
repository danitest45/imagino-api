namespace Imagino.Api.Errors.Exceptions;

public class InsufficientCreditsException : Exception
{
    public int Current { get; }
    public int Needed { get; }

    public InsufficientCreditsException(int current, int needed)
        : base("Insufficient credits.")
    {
        Current = current;
        Needed = needed;
    }
}
