using System.Collections.Concurrent;
using System.Data;
using Appointment.Server.Exceptions;
using Appointment.Server.Interfaces;
using Appointment.Server.Models;
using Appointment.Server.Notifications;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Appointment.Server.Services
{
    public class AppointmentService : IAppointmentService
    {
        private const string LookupFailedMessage = "No appointment found for that confirmation code and email.";

        private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> BranchLocks = new();

        private readonly AppDBContext _context;
        private readonly INotificationService _notifications;
        private readonly BookingOptions _options;
        private readonly TimeProvider _clock;
        private readonly TimeZoneInfo _zone;
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
            _zone = TimeZoneInfo.FindSystemTimeZoneById(_options.TimeZoneId);
            _logger = logger;
        }

        public async Task<AppointmentResultDto> BookAppointmentAsync(CreateAppointmentDto dto, CancellationToken ct = default)
        {
            var start = NormaliseToUtc(dto.StartTime);
            _logger.LogInformation("Booking requested for branch {BranchId}, service {ServiceId} at {Start:u}", dto.BranchId, dto.ServiceId, start);

            var capacity = await _context.Branches.Where(b => b.Id == dto.BranchId)
                .Select(b => (int?)b.Capacity).FirstOrDefaultAsync(ct)
                ?? throw new NotFoundException("Selected branch not found.");

            var service = await _context.Services.FirstOrDefaultAsync(s => s.Id == dto.ServiceId, ct)
                ?? throw new NotFoundException("Selected service not found.");

            var branchOffersService = await _context.BranchServices
                .AnyAsync(bs => bs.BranchId == dto.BranchId && bs.ServiceId == dto.ServiceId, ct);
            if (!branchOffersService)
                throw new BusinessRuleException("The selected branch does not offer this service.");

            var end = start.AddMinutes(service.DurationMinutes);
            ValidateTime(start, end);

            var branchLock = BranchLocks.GetOrAdd(dto.BranchId, _ => new SemaphoreSlim(1, 1));
            await branchLock.WaitAsync(ct);

            Models.Appointment appointment;
            try
            {
                await using var transaction = await _context.Database.BeginTransactionAsync(ct);
                await LockBranchInDatabaseAsync(dto.BranchId, ct);

                var overlapping = await _context.Appointments.AsNoTracking()
                    .Where(a => a.BranchId == dto.BranchId
                                && a.Status != AppointmentStatus.Cancelled
                                && a.StartTime < end
                                && a.EndTime > start)
                    .Select(a => new TimeRange(a.StartTime, a.EndTime))
                    .ToListAsync(ct);

                if (IsFull(overlapping, start, end, capacity))
                {
                    _logger.LogWarning("Slot conflict for branch {BranchId} at {Start:u}", dto.BranchId, start);
                    throw new SlotUnavailableException("This time slot is already booked. Please choose another time.");
                }

                appointment = new Models.Appointment
                {
                    BranchId = dto.BranchId,
                    ServiceId = dto.ServiceId,
                    CustomerName = dto.CustomerName.Trim(),
                    CustomerEmail = dto.CustomerEmail.Trim(),
                    CustomerPhone = dto.CustomerPhone.Trim(),
                    StartTime = start,
                    EndTime = end,
                    Status = AppointmentStatus.Confirmed,
                    ConfirmationCode = await GenerateUniqueCodeAsync(ct)
                };

                _context.Appointments.Add(appointment);
                await _context.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
            }
            finally
            {
                branchLock.Release();
            }

            _logger.LogInformation("Appointment {AppointmentId} booked with code {Code}", appointment.Id, appointment.ConfirmationCode);
            await NotifyAsync(appointment, service.Name, ct);

            return ToResult(appointment);
        }

        public async Task<AppointmentResultDto> CancelAppointmentAsync(string confirmationCode, string email, CancellationToken ct = default)
        {
            var code = confirmationCode.Trim().ToUpperInvariant();
            var customerEmail = email.Trim().ToLowerInvariant();

            var appointment = await _context.Appointments
                .Include(a => a.Service)
                .FirstOrDefaultAsync(a => a.ConfirmationCode == code && a.CustomerEmail.ToLower() == customerEmail, ct)
                ?? throw new NotFoundException(LookupFailedMessage);

            if (appointment.Status != AppointmentStatus.Cancelled)
            {
                if (appointment.StartTime <= _clock.GetUtcNow().UtcDateTime)
                    throw new BusinessRuleException("Appointments that have already started cannot be cancelled.");

                appointment.Status = AppointmentStatus.Cancelled;
                appointment.UpdatedAt = _clock.GetUtcNow().UtcDateTime;
                await _context.SaveChangesAsync(ct);
                _logger.LogInformation("Appointment {AppointmentId} cancelled", appointment.Id);

                try
                {
                    await _notifications.SendCancellationAsync(appointment, appointment.Service.Name, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send cancellation notice for appointment {AppointmentId}", appointment.Id);
                }
            }

            return ToResult(appointment);
        }
        public async Task<AppointmentResultDto> FindAppointmentAsync(string confirmationCode, string email, CancellationToken ct = default)
        {
            var code = confirmationCode.Trim().ToUpperInvariant();
            var customerEmail = email.Trim().ToLowerInvariant();

            var appointment = await _context.Appointments.AsNoTracking()
                .FirstOrDefaultAsync(a => a.ConfirmationCode == code && a.CustomerEmail.ToLower() == customerEmail, ct);

            if (appointment == null)
            {
                _logger.LogWarning("Appointment lookup failed for code {Code}", code);
                throw new NotFoundException(LookupFailedMessage);
            }

            return ToResult(appointment);
        }

        public async Task<IReadOnlyList<TimeSpan>> GetAvailableSlotsAsync(Guid branchId, Guid serviceId, DateTime date, CancellationToken ct = default)
        {
            var service = await _context.Services.AsNoTracking().FirstOrDefaultAsync(s => s.Id == serviceId, ct)
                ?? throw new NotFoundException("Selected service not found.");

            var branchOffersService = await _context.BranchServices
                .AnyAsync(bs => bs.BranchId == branchId && bs.ServiceId == serviceId, ct);
            if (!branchOffersService)
                throw new BusinessRuleException("The selected branch does not offer this service.");

            var localDay = DateTime.SpecifyKind(date.Date, DateTimeKind.Unspecified);
            var startOfDay = TimeZoneInfo.ConvertTimeToUtc(localDay, _zone);
            var endOfDay = TimeZoneInfo.ConvertTimeToUtc(localDay.AddDays(1), _zone);
            var now = _clock.GetUtcNow().UtcDateTime;

            var capacity = await _context.Branches.Where(b => b.Id == branchId)
                .Select(b => b.Capacity).FirstAsync(ct);

            var booked = await _context.Appointments.AsNoTracking()
                .Where(a => a.BranchId == branchId
                            && a.Status != AppointmentStatus.Cancelled
                            && a.StartTime < endOfDay
                            && a.EndTime > startOfDay)
                .Select(a => new TimeRange(a.StartTime, a.EndTime))
                .ToListAsync(ct);

            var duration = TimeSpan.FromMinutes(service.DurationMinutes);
            var interval = TimeSpan.FromMinutes(_options.SlotIntervalMinutes);
            var slots = new List<TimeSpan>();

            for (var t = _options.OpeningTime; t + duration <= _options.ClosingTime; t += interval)
            {
                var slotStart = TimeZoneInfo.ConvertTimeToUtc(localDay + t, _zone);
                var slotEnd = slotStart + duration;

                if (slotStart <= now) continue;
                if (IsFull(booked, slotStart, slotEnd, capacity)) continue;

                slots.Add(t);
            }

            return slots;
        }

        private record TimeRange(DateTime Start, DateTime End);

        // Appointments only add to the load at their own start time, so it is enough to count
        // how many are running at the new start and at each later start inside the new window
        private static bool IsFull(IReadOnlyCollection<TimeRange> existing, DateTime start, DateTime end, int capacity)
        {
            var overlapping = existing.Where(a => a.Start < end && a.End > start).ToList();
            if (overlapping.Count < capacity) return false;

            var checkPoints = overlapping.Select(a => a.Start).Where(s => s > start).Append(start);
            return checkPoints.Any(p => overlapping.Count(a => a.Start <= p && a.End > p) >= capacity);
        }

        private void ValidateTime(DateTime start, DateTime end)
        {
            if (start <= _clock.GetUtcNow().UtcDateTime)
                throw new BusinessRuleException("Appointments must be booked in the future.");

            var localStart = TimeZoneInfo.ConvertTimeFromUtc(start, _zone);
            var localEnd = TimeZoneInfo.ConvertTimeFromUtc(end, _zone);

            if (localEnd.Date != localStart.Date
                || localStart.TimeOfDay < _options.OpeningTime
                || localEnd.TimeOfDay > _options.ClosingTime)
            {
                throw new BusinessRuleException(
                    $"Appointments must be booked between {_options.OpeningTime:hh\\:mm} and {_options.ClosingTime:hh\\:mm}.");
            }

            var offset = localStart.TimeOfDay - _options.OpeningTime;
            if (offset.TotalMinutes % _options.SlotIntervalMinutes != 0)
                throw new BusinessRuleException($"Appointments must start on a {_options.SlotIntervalMinutes}-minute boundary.");
        }

        private async Task NotifyAsync(Models.Appointment appointment, string serviceName, CancellationToken ct)
        {
            try
            {
                await _notifications.SendBookingConfirmationAsync(appointment, serviceName, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send confirmation for appointment {AppointmentId}", appointment.Id);
            }
        }

        private static AppointmentResultDto ToResult(Models.Appointment a) => new()
        {
            Id = a.Id,
            ConfirmationCode = a.ConfirmationCode,
            StartTime = a.StartTime,
            EndTime = a.EndTime,
            Status = a.Status.ToString()
        };

        private static DateTime NormaliseToUtc(DateTime value) => value.Kind switch
        {
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };

        private async Task<string> GenerateUniqueCodeAsync(CancellationToken ct)
        {
            // The unique index is the real guard; this just avoids a 500 on a rare collision
            for (var attempt = 0; attempt < 5; attempt++)
            {
                var code = "APT-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
                if (!await _context.Appointments.AnyAsync(a => a.ConfirmationCode == code, ct))
                    return code;
            }

            throw new InvalidOperationException("Could not generate a unique confirmation code.");
        }

        private async Task LockBranchInDatabaseAsync(Guid branchId, CancellationToken ct)
        {
            if (!_context.Database.IsSqlServer()) return;

            var resource = new SqlParameter("@resource", $"booking:{branchId}");
            var result = new SqlParameter("@result", SqlDbType.Int) { Direction = ParameterDirection.Output };
            await _context.Database.ExecuteSqlRawAsync(
                "EXEC @result = sp_getapplock @Resource = @resource, @LockMode = N'Exclusive', @LockOwner = N'Transaction', @LockTimeout = 10000",
                new object[] { result, resource }, ct);

            // Negative means the lock timed out, was cancelled or hit a deadlock
            if ((int)result.Value < 0)
            {
                _logger.LogWarning("Could not get booking lock for branch {BranchId}, code {Code}", branchId, result.Value);
                throw new SlotUnavailableException("The system is busy right now. Please try again.");
            }
        }
    }
}
