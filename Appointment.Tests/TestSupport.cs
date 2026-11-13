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

    public List<string> Cancelled { get; } = new();

    public Task SendCancellationAsync(Server.Models.Appointment appointment, string serviceName, CancellationToken ct = default)
    {
        Cancelled.Add(appointment.ConfirmationCode);
        return Task.CompletedTask;
    }
}

public sealed class TestDb : IDisposable
{
    public static readonly Guid BranchId =     Guid.Parse("99999999-9999-9999-9999-999999999991");
    public static readonly Guid OtherBranchId = Guid.Parse("99999999-9999-9999-9999-999999999992");
    public static readonly Guid TwoRoomBranchId = Guid.Parse("99999999-9999-9999-9999-999999999993");
    public static readonly Guid HourServiceId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    public static readonly Guid HalfHourServiceId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    public static readonly Guid UnofferedServiceId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

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
            new Branch { Id = OtherBranchId, Name = "B", Address = "addr", PhoneNumber = "2" },
            new Branch { Id = TwoRoomBranchId, Name = "C", Address = "addr", PhoneNumber = "3", Capacity = 2 });
        ctx.Services.AddRange(
            new Service { Id = HourServiceId, Name = "Hour", Description = "", DurationMinutes = 60 },
            new Service { Id = HalfHourServiceId, Name = "Half", Description = "", DurationMinutes = 30 },
            new Service { Id = UnofferedServiceId, Name = "Unoffered", Description = "", DurationMinutes = 30 });
        ctx.BranchServices.AddRange(
            new BranchService { BranchId = BranchId, ServiceId = HourServiceId },
            new BranchService { BranchId = BranchId, ServiceId = HalfHourServiceId },
            new BranchService { BranchId = OtherBranchId, ServiceId = HalfHourServiceId },
            new BranchService { BranchId = TwoRoomBranchId, ServiceId = HalfHourServiceId },
            new BranchService { BranchId = TwoRoomBranchId, ServiceId = HourServiceId });
        ctx.SaveChanges();
    }

    public AppDBContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDBContext>().UseSqlite(_connection).Options);

    public AppointmentService CreateService(AppDBContext ctx, string timeZoneId = "UTC") =>
        new(ctx, Notifier, Options.Create(new BookingOptions { TimeZoneId = timeZoneId }), new FixedTimeProvider(Now),
            NullLogger<AppointmentService>.Instance);

    public static CreateAppointmentDto Dto(DateTime start, Guid? serviceId = null, Guid? branchId = null) => new()
    {
        BranchId = branchId ?? BranchId,
        ServiceId = serviceId ?? HalfHourServiceId,
        CustomerName = "Jane Doe",
        CustomerEmail = "jane@example.com",
        CustomerPhone = "0215550100",
        StartTime = start
    };

    public void Dispose() => _connection.Dispose();
}
