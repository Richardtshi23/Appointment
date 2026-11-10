using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Appointment.Server.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Appointment.Tests;

public class ApiIntegrationTests : IClassFixture<ApiIntegrationTests.Factory>
{
    // Seeded by DbInitializer
    private static readonly Guid Branch = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Service = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    public class Factory : WebApplicationFactory<Program>
    {
        private readonly string _dbFile = Path.Combine(Path.GetTempPath(), $"appt-tests-{Guid.NewGuid():N}.db");

        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            builder.UseSetting("DisableHttpsRedirection", "true");
            builder.ConfigureServices(services =>
            {
                var existing = services.Where(d =>
                    d.ServiceType == typeof(DbContextOptions<AppDBContext>) ||
                    d.ServiceType.FullName!.Contains("IDbContextOptionsConfiguration")).ToList();
                foreach (var d in existing) services.Remove(d);
                services.AddDbContext<AppDBContext>(o => o.UseSqlite($"Data Source={_dbFile}"));
            });
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (File.Exists(_dbFile)) File.Delete(_dbFile);
        }
    }

    private readonly HttpClient _client;

    public ApiIntegrationTests(Factory factory) => _client = factory.CreateClient();

    [Fact]
    public async Task Health_endpoint_is_healthy()
    {
        var response = await _client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Branches_and_services_are_seeded()
    {
        var branches = await _client.GetFromJsonAsync<List<BranchDto>>("/api/branches");
        Assert.True(branches!.Count >= 3);

        var services = await _client.GetFromJsonAsync<List<ServiceDto>>($"/api/branches/{Branch}/services");
        Assert.NotEmpty(services!);
    }

    [Fact]
    public async Task Unknown_branch_returns_problem_details_404()
    {
        var response = await _client.GetAsync($"/api/branches/{Guid.NewGuid()}/services");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Branch not found.", doc.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Invalid_payload_returns_400_with_message()
    {
        var response = await _client.PostAsJsonAsync("/api/appointments", new { branchId = Branch, serviceId = Service, customerName = "", customerEmail = "nope", customerPhone = "x", startTime = DateTime.UtcNow });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.False(string.IsNullOrWhiteSpace(doc.RootElement.GetProperty("message").GetString()));
    }

    [Fact]
    public async Task Full_booking_flow_then_conflict()
    {
        var date = DateTime.UtcNow.Date.AddDays(3);
        var dateStr = date.ToString("yyyy-MM-dd");

        var slots = await _client.GetFromJsonAsync<List<string>>(
            $"/api/appointments/available-slots?branchId={Branch}&serviceId={Service}&date={dateStr}");
        Assert.Contains("10:00", slots!);

        var body = new CreateAppointmentDto
        {
            BranchId = Branch, ServiceId = Service, CustomerName = "Jane Doe",
            CustomerEmail = "jane@example.com", CustomerPhone = "+27215550100",
            StartTime = DateTime.SpecifyKind(date.AddHours(10), DateTimeKind.Utc)
        };

        var created = await _client.PostAsJsonAsync("/api/appointments", body);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var result = await created.Content.ReadFromJsonAsync<AppointmentResultDto>();
        Assert.StartsWith("APT-", result!.ConfirmationCode);

        var slotsAfter = await _client.GetFromJsonAsync<List<string>>(
            $"/api/appointments/available-slots?branchId={Branch}&serviceId={Service}&date={dateStr}");
        Assert.DoesNotContain("10:00", slotsAfter!);

        var conflict = await _client.PostAsJsonAsync("/api/appointments", body);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
    }
}
