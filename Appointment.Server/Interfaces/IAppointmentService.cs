using Appointment.Server.Models;

namespace Appointment.Server.Interfaces
{
    public interface IAppointmentService
    {
        Task<AppointmentResultDto> BookAppointmentAsync(CreateAppointmentDto dto, CancellationToken ct = default);
        Task<AppointmentResultDto> CancelAppointmentAsync(string confirmationCode, string email, CancellationToken ct = default);
        Task<AppointmentResultDto> FindAppointmentAsync(string confirmationCode, string email, CancellationToken ct = default);
        Task<IReadOnlyList<TimeSpan>> GetAvailableSlotsAsync(Guid branchId, Guid serviceId, DateTime date, CancellationToken ct = default);
    }
}
