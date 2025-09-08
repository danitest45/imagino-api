using Imagino.Api.Controllers;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Imagino.Api.Tests;

public class HealthCheckTests
{
    [Fact]
    public void Get_ReturnsOkStatus()
    {
        var controller = new HealthController();

        var result = controller.Get();

        var ok = Assert.IsType<OkObjectResult>(result);
        var statusProperty = ok.Value?.GetType().GetProperty("status");
        var value = statusProperty?.GetValue(ok.Value) as string;
        Assert.Equal("ok", value);
    }
}
