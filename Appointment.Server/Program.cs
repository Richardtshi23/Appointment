using Appointment.Server.Data;
using Appointment.Server.Interfaces;
using Appointment.Server.Middleware;
using Appointment.Server.Models;
using Appointment.Server.Notifications;
using Appointment.Server.Services;
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

builder.Services.AddHealthChecks().AddDbContextCheck<AppDBContext>();

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

app.MapControllers();
app.MapHealthChecks("/health");

app.Map("/api/{**rest}", () => Results.NotFound());
app.MapFallbackToFile("/index.html");

app.Run();

public partial class Program { }
