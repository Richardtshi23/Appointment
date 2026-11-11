using Appointment.Server.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace Appointment.Server.Middleware
{
    public class ExceptionHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionHandlingMiddleware> _logger;

        public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
            {
                _logger.LogInformation("Request {Method} {Path} was cancelled by the client", context.Request.Method, context.Request.Path);
                context.Response.StatusCode = 499;
            }
            catch (Exception ex)
            {
                await HandleAsync(context, ex);
            }
        }

        private async Task HandleAsync(HttpContext context, Exception ex)
        {
            ProblemDetails problem;

            if (ex is AppException appEx)
            {
                _logger.LogWarning(ex, "Handled application exception ({Status}) on {Method} {Path}: {Message}",
                    appEx.StatusCode, context.Request.Method, context.Request.Path, ex.Message);

                problem = new ProblemDetails { Status = appEx.StatusCode, Title = appEx.Title, Detail = ex.Message };
            }
            else
            {
                _logger.LogError(ex, "Unhandled exception on {Method} {Path}", context.Request.Method, context.Request.Path);

                problem = new ProblemDetails
                {
                    Status = StatusCodes.Status500InternalServerError,
                    Title = "An unexpected error occurred",
                    Detail = "Something went wrong on our side. Please try again later."
                };
            }

            problem.Instance = context.Request.Path;
            problem.Extensions["traceId"] = context.TraceIdentifier;
            problem.Extensions["message"] = problem.Detail;

            if (context.Response.HasStarted)
            {
                _logger.LogWarning("Response already started; cannot write problem details");
                return;
            }

            context.Response.Clear();
            context.Response.StatusCode = problem.Status!.Value;
            await context.Response.WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json");
        }
    }
}
