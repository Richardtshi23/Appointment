using Appointment.Server.Data;
using Appointment.Server.Interfaces;
using Appointment.Server.Middleware;
using Appointment.Server.Models;
using Appointment.Server.Notifications;
using Appointment.Server.Services;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(o =>
    {
        o.InvalidModelStateResponseFactory = ctx =>
        {
            var errors = ctx.ModelState.Where(e => e.Value?.Errors.Count > 0)
                .ToDictionary(e => e.Key, e => e.Value!.Errors.Select(x => x.ErrorMessage).ToArray());
            var message = string.Join(" ", errors.Values.SelectMany(v => v));

            var problem = new ValidationProblemDetails(errors)
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Validation failed",
                Detail = message,
                Instance = ctx.HttpContext.Request.Path
            };
            problem.Extensions["message"] = message;
            problem.Extensions["traceId"] = ctx.HttpContext.TraceIdentifier;
            return new BadRequestObjectResult(problem) { ContentTypes = { "application/problem+json" } };
        };
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    var xml = Path.Combine(AppContext.BaseDirectory, $"{typeof(Program).Assembly.GetName().Name}.xml");
    if (File.Exists(xml)) o.IncludeXmlComments(xml);
});

builder.Services.AddDbContext<AppDBContext>(o =>
    o.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.Configure<BookingOptions>(builder.Configuration.GetSection(BookingOptions.SectionName));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IAppointmentService, AppointmentService>();
builder.Services.AddScoped<IBranchService, BranchCatalogService>();
builder.Services.AddSingleton<INotificationService, SimulatedNotificationService>();

// Only trust X-Forwarded-For from the private networks the nginx container lives on, so a client
// hitting the API directly can't fake its address and dodge the rate limit
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownNetworks.Clear();
    o.KnownProxies.Clear();
    foreach (var network in new[] { "10.0.0.0/8", "172.16.0.0/12", "192.168.0.0/16" })
    {
        var parts = network.Split('/');
        o.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(System.Net.IPAddress.Parse(parts[0]), int.Parse(parts[1])));
    }
});

builder.Services.AddHealthChecks().AddDbContextCheck<AppDBContext>();

// Per-client limits so the lookup/cancel endpoints can't be brute-forced
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.OnRejected = async (ctx, ct) =>
    {
        ctx.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await ctx.HttpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Title = "Too many requests",
            Extensions =
            {
                ["message"] = "Too many requests. Please wait a minute and try again.",
                ["traceId"] = ctx.HttpContext.TraceIdentifier
            }
        }, (System.Text.Json.JsonSerializerOptions?)null, "application/problem+json", ct);
    };
    o.AddPolicy("lookup", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
    o.AddPolicy("booking", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1) }));
});

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();

await DbInitializer.InitializeAsync(app.Services);

if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Swagger:Enabled"))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseDefaultFiles();
app.UseStaticFiles();

if (!app.Configuration.GetValue<bool>("DisableHttpsRedirection"))
{
    app.UseHttpsRedirection();
}

app.UseForwardedHeaders();
app.UseRateLimiter();

app.MapControllers();
app.MapHealthChecks("/health");

app.Map("/api/{**rest}", () => Results.NotFound());
app.MapFallbackToFile("/index.html");

app.Run();

public partial class Program { }
