using Appointment.Server.Exceptions;

namespace Appointment.Tests;

public class AppointmentServiceTests : IDisposable
{
    private readonly TestDb _db = new();

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Slots_for_empty_day_cover_opening_hours()
    {
        using var ctx = _db.CreateContext();
        var slots = await _db.CreateService(ctx)
            .GetAvailableSlotsAsync(TestDb.BranchId, TestDb.HourServiceId, TestDb.Tomorrow);

        Assert.Equal(new TimeSpan(9, 0, 0), slots.First());
        Assert.Equal(new TimeSpan(16, 0, 0), slots.Last()); // 1h service must finish by 17:00
        Assert.Equal(15, slots.Count);
    }

    [Fact]
    public async Task Booking_returns_confirmation_and_notifies()
    {
        using var ctx = _db.CreateContext();
        var result = await _db.CreateService(ctx)
            .BookAppointmentAsync(TestDb.Dto(TestDb.Tomorrow.AddHours(10)));

        Assert.StartsWith("APT-", result.ConfirmationCode);
        Assert.Equal("Confirmed", result.Status);
        Assert.Equal(TestDb.Tomorrow.AddHours(10).AddMinutes(30), result.EndTime);
        Assert.Contains(result.ConfirmationCode, _db.Notifier.Sent);
    }

    [Fact]
    public async Task Booked_slot_is_removed_from_availability_for_any_service_at_the_branch()
    {
        using var ctx = _db.CreateContext();
        var svc = _db.CreateService(ctx);
        await svc.BookAppointmentAsync(TestDb.Dto(TestDb.Tomorrow.AddHours(10), TestDb.HourServiceId));

        var slots = await svc.GetAvailableSlotsAsync(TestDb.BranchId, TestDb.HalfHourServiceId, TestDb.Tomorrow);

        Assert.DoesNotContain(new TimeSpan(10, 0, 0), slots);
        Assert.DoesNotContain(new TimeSpan(10, 30, 0), slots);
        Assert.Contains(new TimeSpan(11, 0, 0), slots);
        Assert.Contains(new TimeSpan(9, 30, 0), slots);
    }

    [Fact]
    public async Task Cancelling_frees_the_slot_notifies_and_is_idempotent()
    {
        using var ctx = _db.CreateContext();
        var svc = _db.CreateService(ctx);
        var booked = await svc.BookAppointmentAsync(TestDb.Dto(TestDb.Tomorrow.AddHours(10)));

        var email = TestDb.Dto(TestDb.Tomorrow).CustomerEmail;
        var cancelled = await svc.CancelAppointmentAsync(booked.ConfirmationCode, email);
        await svc.CancelAppointmentAsync(booked.ConfirmationCode, email);

        Assert.Equal("Cancelled", cancelled.Status);
        Assert.Single(_db.Notifier.Cancelled);
        var slots = await svc.GetAvailableSlotsAsync(TestDb.BranchId, TestDb.HalfHourServiceId, TestDb.Tomorrow);
        Assert.Contains(new TimeSpan(10, 0, 0), slots);
    }

    [Fact]
    public async Task Lookup_finds_appointment_by_code_and_email_ignoring_case()
    {
        using var ctx = _db.CreateContext();
        var svc = _db.CreateService(ctx);
        var booked = await svc.BookAppointmentAsync(TestDb.Dto(TestDb.Tomorrow.AddHours(10)));

        var found = await svc.FindAppointmentAsync(booked.ConfirmationCode.ToLowerInvariant(), " JANE@example.com ");

        Assert.Equal(booked.Id, found.Id);
    }

    [Fact]
    public async Task Lookup_with_wrong_email_is_not_found()
    {
        using var ctx = _db.CreateContext();
        var svc = _db.CreateService(ctx);
        var booked = await svc.BookAppointmentAsync(TestDb.Dto(TestDb.Tomorrow.AddHours(10)));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            svc.FindAppointmentAsync(booked.ConfirmationCode, "someone.else@example.com"));
    }

    [Fact]
    public async Task Cancelling_with_wrong_email_is_not_found_and_keeps_the_booking()
    {
        using var ctx = _db.CreateContext();
        var svc = _db.CreateService(ctx);
        var booked = await svc.BookAppointmentAsync(TestDb.Dto(TestDb.Tomorrow.AddHours(10)));

        await Assert.ThrowsAsync<NotFoundException>(() => svc.CancelAppointmentAsync(booked.ConfirmationCode, "someone.else@example.com"));

        var found = await svc.FindAppointmentAsync(booked.ConfirmationCode, TestDb.Dto(TestDb.Tomorrow).CustomerEmail);
        Assert.Equal("Confirmed", found.Status);
    }

    [Fact]
    public async Task Parallel_bookings_for_the_same_slot_only_let_one_through()
    {
        var attempts = Enumerable.Range(0, 8).Select(async _ =>
        {
            using var ctx = _db.CreateContext();
            try
            {
                await _db.CreateService(ctx).BookAppointmentAsync(TestDb.Dto(TestDb.Tomorrow.AddHours(10)));
                return true;
            }
            catch (SlotUnavailableException)
            {
                return false;
            }
        });

        var results = await Task.WhenAll(attempts);

        Assert.Equal(1, results.Count(r => r));
    }

    [Fact]
    public async Task Double_booking_is_rejected_with_conflict()
    {
        using var ctx = _db.CreateContext();
        var svc = _db.CreateService(ctx);
        await svc.BookAppointmentAsync(TestDb.Dto(TestDb.Tomorrow.AddHours(10)));

        await Assert.ThrowsAsync<SlotUnavailableException>(() =>
            svc.BookAppointmentAsync(TestDb.Dto(TestDb.Tomorrow.AddHours(10))));
    }

    [Fact]
    public async Task Same_time_at_a_different_branch_is_allowed()
    {
        using var ctx = _db.CreateContext();
        var svc = _db.CreateService(ctx);
        await svc.BookAppointmentAsync(TestDb.Dto(TestDb.Tomorrow.AddHours(10)));

        var result = await svc.BookAppointmentAsync(TestDb.Dto(TestDb.Tomorrow.AddHours(10), branchId: TestDb.OtherBranchId));
        Assert.NotEqual(Guid.Empty, result.Id);
    }

    [Fact]
    public async Task Start_before_opening_is_rejected()
    {
        using var ctx = _db.CreateContext();
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _db.CreateService(ctx).BookAppointmentAsync(TestDb.Dto(TestDb.Tomorrow.AddHours(8))));
    }

    [Fact]
    public async Task Appointment_running_past_closing_is_rejected_but_last_slot_is_allowed()
    {
        using var ctx = _db.CreateContext();
        var svc = _db.CreateService(ctx);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            svc.BookAppointmentAsync(TestDb.Dto(TestDb.Tomorrow.AddHours(16.5), TestDb.HourServiceId)));

        var result = await svc.BookAppointmentAsync(TestDb.Dto(TestDb.Tomorrow.AddHours(16.5)));
        Assert.Equal(TestDb.Tomorrow.AddHours(17), result.EndTime);
    }

    [Fact]
    public async Task Past_start_time_is_rejected()
    {
        using var ctx = _db.CreateContext();
        var past = TestDb.Now.UtcDateTime.AddDays(-1).Date.AddHours(10);
        await Assert.ThrowsAsync<BusinessRuleException>(() => _db.CreateService(ctx).BookAppointmentAsync(TestDb.Dto(past)));
    }

    [Fact]
    public async Task Off_grid_start_time_is_rejected()
    {
        using var ctx = _db.CreateContext();
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _db.CreateService(ctx).BookAppointmentAsync(TestDb.Dto(TestDb.Tomorrow.AddHours(10).AddMinutes(10))));
    }

    [Fact]
    public async Task Service_not_offered_by_branch_is_rejected()
    {
        using var ctx = _db.CreateContext();
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _db.CreateService(ctx).BookAppointmentAsync(TestDb.Dto(TestDb.Tomorrow.AddHours(10), TestDb.UnofferedServiceId)));
    }

    [Fact]
    public async Task Unknown_branch_or_service_is_not_found()
    {
        using var ctx = _db.CreateContext();
        var svc = _db.CreateService(ctx);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            svc.BookAppointmentAsync(TestDb.Dto(TestDb.Tomorrow.AddHours(10), branchId: Guid.NewGuid())));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            svc.BookAppointmentAsync(TestDb.Dto(TestDb.Tomorrow.AddHours(10), serviceId: Guid.NewGuid())));
    }

    [Fact]
    public async Task Opening_hours_are_applied_in_south_african_time()
    {
        using var ctx = _db.CreateContext();
        var svc = _db.CreateService(ctx, "Africa/Johannesburg");

        var slots = await svc.GetAvailableSlotsAsync(TestDb.BranchId, TestDb.HalfHourServiceId, TestDb.Tomorrow);
        Assert.Equal(new TimeSpan(9, 0, 0), slots.First());

        var result = await svc.BookAppointmentAsync(TestDb.Dto(TestDb.Tomorrow.AddHours(7)));
        Assert.Equal(TestDb.Tomorrow.AddHours(7), result.StartTime);
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            svc.BookAppointmentAsync(TestDb.Dto(TestDb.Tomorrow.AddHours(5))));

        var after = await svc.GetAvailableSlotsAsync(TestDb.BranchId, TestDb.HalfHourServiceId, TestDb.Tomorrow);
        Assert.DoesNotContain(new TimeSpan(9, 0, 0), after);
    }

    [Fact]
    public async Task Slots_in_the_past_are_not_offered()
    {
        using var ctx = _db.CreateContext();
        var slots = await _db.CreateService(ctx)
            .GetAvailableSlotsAsync(TestDb.BranchId, TestDb.HalfHourServiceId, TestDb.Now.UtcDateTime.Date);
        Assert.Equal(new TimeSpan(9, 0, 0), slots.First());

        var later = new Appointment.Server.Services.AppointmentService(
            ctx, _db.Notifier,
            Microsoft.Extensions.Options.Options.Create(new Appointment.Server.Models.BookingOptions { TimeZoneId = "UTC" }),
            new FixedTimeProvider(new DateTimeOffset(2030, 1, 1, 12, 15, 0, TimeSpan.Zero)),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<Appointment.Server.Services.AppointmentService>.Instance);
        var laterSlots = await later.GetAvailableSlotsAsync(TestDb.BranchId, TestDb.HalfHourServiceId, TestDb.Now.UtcDateTime.Date);
        Assert.Equal(new TimeSpan(12, 30, 0), laterSlots.First());
    }
}
