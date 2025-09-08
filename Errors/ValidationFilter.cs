using System.Linq;
using Imagino.Api.Errors.Exceptions;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Imagino.Api.Errors;

public class ValidationFilter : IActionFilter
{
    public void OnActionExecuting(ActionExecutingContext context)
    {
        if (!context.ModelState.IsValid)
        {
            var errors = context.ModelState
                .Where(kvp => kvp.Value?.Errors.Count > 0)
                .ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value!.Errors.Select(e => e.ErrorMessage).ToArray());

            throw new ValidationAppException(errors);
        }
    }

    public void OnActionExecuted(ActionExecutedContext context) { }
}
