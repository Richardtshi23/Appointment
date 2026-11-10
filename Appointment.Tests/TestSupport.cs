using Appointment.Server.Data;
using Appointment.Server.Models;
using Appointment.Server.Notifications;
using Appointment.Server.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Appointment.Tests;

public class FixedTimeProvider : TimeProvider
{
    private readonly DateTimeOffset _now;
    public FixedTimeProvider(DateTimeOffset now) => _now = now;
    public override DateTimeOffset GetUtcNow() => _now;
}

public class RecordingNotifier : INotificationService
{
    public List<string> Sent { get; } = new();

    public Task SendBookingConfirmationAsync(Server.Models.Appointment appointment, string serviceName, CancellationToken ct = default)
    {
        Sent.Add(appointment.ConfirmationCode);
        return Task.CompletedTask;
    }
}

/// <summary>Real SQLite (in-memory) so queries run against the same provider as production.</summary>
public sealed class TestDb : IDisposable
{
    public static readonly Guid BranchId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid OtherBranchId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid HourServiceId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    public static readonly Guid HalfHourServiceId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    public static readonly Guid UnofferedServiceId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

    // 2030-01-01 08:00 UTC, so 2030-01-02 is always in the future
    public static readonly DateTimeOffset Now = new(2030, 1, 1, 8, 0, 0, TimeSpan.Zero);
    public static readonly DateTime Tomorrow = new(2030, 1, 2, 0, 0, 0, DateTimeKind.Utc);

    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    public RecordingNotifier Notifier { get; } = new();

    public TestDb()
    {
        _connection.Open();
        using var ctx = CreateContext();
        ctx.Database.EnsureCreated();

        ctx.Branches.AddRange(
            new Branch { Id = BranchId, Name = "A", Address = "addr", PhoneNumber = "1" },
            new Branch { Id = OtherBranchId, Name = "B", Address = "addr", PhoneNumber = "2" });
        ctx.Services.AddRange(
            new Service { Id = HourServiceId, Name = "Hour", Description = "", DurationMinutes = 60 },
            new Service { Id = HalfHourServiceId, Name = "Half", Description = "", DurationMinutes = 30 },
            new Service { Id = UnofferedServiceId, Name = "Unoffered", Description = "", DurationMinutes = 30 });
        ctx.BranchServices.AddRange(
            new BranchService { BranchId = BranchId, ServiceId = HourServiceId },
            new BranchService { BranchId = BranchId, ServiceId = HalfHourServiceId },
            new BranchService { BranchId = OtherBranchId, ServiceId = HalfHourServiceId });
        ctx.SaveChanges();
    }

    public AppDBContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDBContext>().UseSqlite(_connection).Options);

    public AppointmentService CreateService(AppDBContext ctx) =>
        new(ctx, Notifier, Options.Create(new BookingOptions()), new FixedTimeProvider(Now),
            NullLogger<AppointmentService>.Instance);

    public static CreateAppointmentDto Dto(DateTime start, Guid? serviceId = null, Guid? branchId = null) => new()
    {
        BranchId = branchId ?? BranchId,
        ServiceId = serviceId ?? HalfHourServiceId,
        CustomerName = "Jane Doe",
        CustomerEmail = "jane@example.com",
        CustomerPhone = "+27 21 555 0100",
        StartTime = start
    };

    public void Dispose() => _connection.Dispose();
}
