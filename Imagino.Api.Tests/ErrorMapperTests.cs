using System.Collections.Generic;
using Imagino.Api.Errors;
using Imagino.Api.Errors.Exceptions;
using Xunit;

namespace Imagino.Api.Tests;

public class ErrorMapperTests
{
    [Fact]
    public void MapsInsufficientCredits()
    {
        var ex = new InsufficientCreditsException(1, 5);
        var (status, code, _, _, meta) = ErrorMapper.Map(ex);

        Assert.Equal(402, status);
        Assert.Equal(ErrorCodes.INSUFFICIENT_CREDITS, code);
        Assert.NotNull(meta);
    }

    [Fact]
    public void MapsValidation()
    {
        var errors = new Dictionary<string, string[]> { ["field"] = new[] { "msg" } };
        var ex = new ValidationAppException(errors);
        var (status, code, _, _, meta) = ErrorMapper.Map(ex);

        Assert.Equal(422, status);
        Assert.Equal(ErrorCodes.VALIDATION_FAILED, code);
        Assert.NotNull(meta);
    }
}
