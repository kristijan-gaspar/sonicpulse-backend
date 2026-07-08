using Microsoft.AspNetCore.Mvc;

namespace SonicPulse.Api.Extensions;

public static class HttpResponseExtensions
{
    public static Task WriteProblemAsync(this HttpResponse response, int statusCode, string title)
    {
        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title
        };

        response.StatusCode = statusCode;
        response.ContentType = "application/problem+json";
        return response.WriteAsJsonAsync(problem);
    }
}
