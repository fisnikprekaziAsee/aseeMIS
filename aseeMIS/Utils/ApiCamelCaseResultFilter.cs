using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Text.Json;

public sealed class ApiCamelCaseResultFilter : IAsyncResultFilter
{
    private static readonly JsonSerializerOptions Camel = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        // Only for controllers in Area = "Api"
        var area = context.RouteData.Values.TryGetValue("area", out var a) ? a?.ToString() : null;
        if (!string.Equals(area, "API", StringComparison.OrdinalIgnoreCase))
        {
            await next();
            return;
        }

        // Only touch ObjectResult (Ok(), BadRequest(), etc.) and JsonResult
        if (context.Result is ObjectResult obj)
        {
            context.Result = new JsonResult(obj.Value, Camel)
            {
                StatusCode = obj.StatusCode
            };
        }
        else if (context.Result is JsonResult json)
        {
            context.Result = new JsonResult(json.Value, Camel)
            {
                StatusCode = json.StatusCode
            };
        }

        await next();
    }
}