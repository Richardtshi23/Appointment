using Appointment.Server.Exceptions;
using Appointment.Server.Interfaces;
using Appointment.Server.Models;
using Appointment.Server.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Appointment.Server.Services
{
    public class AppointmentService : IAppointmentService
    {
        // Serialises check-then-insert within this process so two requests cannot book the same slot.
        // A multi-instance deployment needs a database-level guarantee instead (see README).
        private static readonly SemaphoreSlim BookingLock = new(1, 1);

        private readonly AppDBContext _context;
        private readonly INotificationService _notifications;
        private readonly BookingOptions _options;
        private readonly TimeProvider _clock;
        private readonly ILogger<AppointmentService> _logger;

        public AppointmentService(
            AppDBContext context,
            INotificationService notifications,
            IOptions<BookingOptions> options,
            TimeProvider clock,
            ILogger<AppointmentService> logger)
        {
            _context = context;
            _notifications = notifications;
            _options = options.Value;
            _clock = clock;
            _logger = logger;
        }

        public async Task<AppointmentResultDto> BookAppointmentAsync(CreateAppointmentDto dto, CancellationToken ct = default)
        {
            var start = NormaliseToUtc(dto.StartTime);
            _logger.LogInformation("Booking requested for branch {BranchId}, service {ServiceId} at {Start:u}", dto.BranchId, dto.ServiceId, start);

            var branchExists = await _context.Branches.AnyAsync(b => b.Id == dto.BranchId, ct);
            if (!branchExists)
                throw new NotFoundException("Selected branch not found.");

            var service = await _context.Services.FirstOrDefaultAsync(s => s.Id == dto.ServiceId, ct)
                ?? throw new NotFoundException("Selected service not found.");

            var branchOffersService = await _context.BranchServices
                .AnyAsync(bs => bs.BranchId == dto.BranchId && bs.ServiceId == dto.ServiceId, ct);
            if (!branchOffersService)
                throw new BusinessRuleException("The selected branch does not offer this service.");

            var end = start.AddMinutes(service.DurationMinutes);
            ValidateTime(start, end);

            await BookingLock.WaitAsync(ct);
            try
            {
                var hasConflict = await _context.Appointments.AnyAsync(a =>
                    a.BranchId == dto.BranchId
                    && a.Status != AppointmentStatus.Cancelled
                    && a.StartTime < end
                    && a.EndTime > start, ct);

                if (hasConflict)
                {
                    _logger.LogWarning("Slot conflict for branch {BranchId} at {Start:u}", dto.BranchId, start);
                    throw new SlotUnavailableException("This time slot is already booked. Please choose another time.");
                }

                var appointment = new Models.Appointment
                {
                    BranchId = dto.BranchId,
                    ServiceId = dto.ServiceId,
                    CustomerName = dto.CustomerName.Trim(),
                    CustomerEmail = dto.CustomerEmail.Trim(),
                    CustomerPhone = dto.CustomerPhone.Trim(),
                    StartTime = start,
                    EndTime = end,
                    Status = AppointmentStatus.Confirmed,
                    ConfirmationCode = GenerateConfirmationCode()
                };

                _context.Appointments.Add(appointment);
                await _context.SaveChangesAsync(ct);

                _logger.LogInformation("Appointment {AppointmentId} booked with code {Code}", appointment.Id, appointment.ConfirmationCode);

                await NotifyAsync(appointment, service.Name, ct);

                return new AppointmentResultDto
                {
                    Id = appointment.Id,
                    ConfirmationCode = appointment.ConfirmationCode,
                    StartTime = appointment.StartTime,
                    EndTime = appointment.EndTime,
                    Status = appointment.Status.ToString()
                };
            }
            finally
            {
                BookingLock.Release();
            }
        }

        public async Task<IReadOnlyList<TimeSpan>> GetAvailableSlotsAsync(Guid branchId, Guid serviceId, DateTime date, CancellationToken ct = default)
        {
            var service = await _context.Services.AsNoTracking().FirstOrDefaultAsync(s => s.Id == serviceId, ct)
                ?? throw new NotFoundException("Selected service not found.");

            var branchOffersService = await _context.BranchServices
                .AnyAsync(bs => bs.BranchId == branchId && bs.ServiceId == serviceId, ct);
            if (!branchOffersService)
                throw new BusinessRuleException("The selected branch does not offer this service.");

            var startOfDay = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);
            var endOfDay = startOfDay.AddDays(1);
            var now = _clock.GetUtcNow().UtcDateTime;

            // A branch is a single resource: any booking at the branch blocks the slot, whatever the service.
            var booked = await _context.Appointments.AsNoTracking()
                .Where(a => a.BranchId == branchId
                            && a.Status != AppointmentStatus.Cancelled
                            && a.StartTime < endOfDay
                            && a.EndTime > startOfDay)
                .Select(a => new { a.StartTime, a.EndTime })
                .ToListAsync(ct);

            var duration = TimeSpan.FromMinutes(service.DurationMinutes);
            var interval = TimeSpan.FromMinutes(_options.SlotIntervalMinutes);
            var slots = new List<TimeSpan>();

            for (var t = _options.OpeningTime; t + duration <= _options.ClosingTime; t += interval)
            {
                var slotStart = startOfDay + t;
                var slotEnd = slotStart + duration;

                if (slotStart <= now) continue;
                if (booked.Any(b => b.StartTime < slotEnd && b.EndTime > slotStart)) continue;

                slots.Add(t);
            }

            return slots;
        }

        private void ValidateTime(DateTime start, DateTime end)
        {
            if (start <= _clock.GetUtcNow().UtcDateTime)
                throw new BusinessRuleException("Appointments must be booked in the future.");

            if (end.Date != start.Date
                || start.TimeOfDay < _options.OpeningTime
                || end.TimeOfDay > _options.ClosingTime)
            {
                throw new BusinessRuleException(
                    $"Appointments must be booked between {_options.OpeningTime:hh\\:mm} and {_options.ClosingTime:hh\\:mm}.");
            }

            var offset = start.TimeOfDay - _options.OpeningTime;
            if (offset.TotalMinutes % _options.SlotIntervalMinutes != 0)
                throw new BusinessRuleException($"Appointments must start on a {_options.SlotIntervalMinutes}-minute boundary.");
        }

        private async Task NotifyAsync(Models.Appointment appointment, string serviceName, CancellationToken ct)
        {
            // The booking is already committed; a notification failure must not fail the request.
            try
            {
                await _notifications.SendBookingConfirmationAsync(appointment, serviceName, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send confirmation for appointment {AppointmentId}", appointment.Id);
            }
        }

        private static DateTime NormaliseToUtc(DateTime value) => value.Kind switch
        {
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };

        private static string GenerateConfirmationCode() =>
            "APT-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
    }
}
