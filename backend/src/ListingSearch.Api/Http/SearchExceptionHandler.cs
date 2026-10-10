using ListingSearch.Application.Searching;
using Microsoft.AspNetCore.Diagnostics;

namespace ListingSearch.Api.Http;

/// <summary>
/// Translates the application's expected failures into RFC 7807 problem responses with one consistent
/// shape: <c>errors</c> maps each offending parameter to its messages. Anything else is left to the
/// framework, which logs it and returns a generic 500 without leaking internals.
/// </summary>
internal sealed class SearchExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var problem = exception switch
        {
            InvalidSearchRequestException invalid => InvalidRequest(invalid),
            UnknownCityException unknown => UnknownCity(unknown),
            _ => null,
        };

        if (problem is null)
        {
            return false;
        }

        httpContext.Response.StatusCode = problem.Status!.Value;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
    }

    private static HttpValidationProblemDetails InvalidRequest(InvalidSearchRequestException exception) => new(
        exception.Errors
            .GroupBy(error => error.Field)
            .ToDictionary(group => group.Key, group => group.Select(error => error.Message).ToArray()))
    {
        Status = StatusCodes.Status400BadRequest,
        Title = "The search request is invalid.",
        Detail = "Fix the parameters listed in 'errors' and try again.",
    };

    private static HttpValidationProblemDetails UnknownCity(UnknownCityException exception)
    {
        var hint = exception.Suggestions.Count > 0
            ? $" Did you mean {string.Join(", ", exception.Suggestions)}?"
            : " Use GET /api/cities to see the available cities.";

        return new HttpValidationProblemDetails(new Dictionary<string, string[]>
        {
            ["city"] = [$"No listings exist for city '{exception.City}'.{hint}"],
        })
        {
            Status = StatusCodes.Status404NotFound,
            Title = "City not found.",
            Detail = $"No feed contains listings for '{exception.City}'.",
            Extensions = { ["suggestions"] = exception.Suggestions },
        };
    }
}
