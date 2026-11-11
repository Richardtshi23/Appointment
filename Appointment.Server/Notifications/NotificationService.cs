using Appointment.Server.Models;

namespace Appointment.Server.Notifications
{
    public interface INotificationService
    {
        Task SendBookingConfirmationAsync(Models.Appointment appointment, string serviceName, CancellationToken ct = default);
                Task SendCancellationAsync(Models.Appointment appointment, string serviceName, CancellationToken ct = default);
    }

    public class SimulatedNotificationService : INotificationService
    {
        private readonly ILogger<SimulatedNotificationService> _logger;

        public SimulatedNotificationService(ILogger<SimulatedNotificationService> logger) => _logger = logger;

        public Task SendBookingConfirmationAsync(Models.Appointment appointment, string serviceName, CancellationToken ct = default)
        {
            _logger.LogInformation(
                "[SIMULATED EMAIL] To: {Email} | Subject: Appointment {Code} confirmed | {Service} on {Start:u}",
                appointment.CustomerEmail, appointment.ConfirmationCode, serviceName, appointment.StartTime);

            _logger.LogInformation(
                "[SIMULATED SMS] To: {Phone} | Your appointment {Code} is confirmed for {Start:u}",
                appointment.CustomerPhone, appointment.ConfirmationCode, appointment.StartTime);

            return Task.CompletedTask;
        }

        public Task SendCancellationAsync(Models.Appointment appointment, string serviceName, CancellationToken ct = default)
        {
            _logger.LogInformation(
                "[SIMULATED EMAIL] To: {Email} | Subject: Appointment {Code} cancelled | {Service} on {Start:u}",
                appointment.CustomerEmail, appointment.ConfirmationCode, serviceName, appointment.StartTime);

            _logger.LogInformation(
                "[SIMULATED SMS] To: {Phone} | Your appointment {Code} has been cancelled",
                appointment.CustomerPhone, appointment.ConfirmationCode);

            return Task.CompletedTask;
        }
    }
}
