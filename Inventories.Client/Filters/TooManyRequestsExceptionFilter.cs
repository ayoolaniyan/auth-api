using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Inventories.Client.Filters
{
    /// <summary>
    /// Shows a "too many requests" page when the API Gateway rate-limits a call (429), instead of
    /// the generic error page.
    /// </summary>
    public class TooManyRequestsExceptionFilter : IExceptionFilter
    {
        public void OnException(ExceptionContext context)
        {
            if (context.Exception is HttpRequestException { StatusCode: HttpStatusCode.TooManyRequests })
            {
                context.Result = new ViewResult
                {
                    ViewName = "TooManyRequests",
                    StatusCode = StatusCodes.Status429TooManyRequests
                };
                context.ExceptionHandled = true;
            }
        }
    }
}
