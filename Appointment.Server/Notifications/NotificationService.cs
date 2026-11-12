using Appointment.Server.Models;

namespace Appointment.Server.Notifications
{
    public interface INotificationService
    {
        Task SendBookingConfirmationAsync(Models.Appointment appointment, string serviceName, CancellationToken ct = default);
        Task SendCancellationAsync(Models.Appointment appointment, string serviceName, CancellationToken ct = default);
    }

    internal static class Mask
    {
        public static string Email(string email)
        {
            var at = email.IndexOf('@');
            return at <= 1 ? "***" : email[0] + "***" + email[at..];
        }

        public static string Phone(string phone) =>
            phone.Length <= 4 ? "***" : new string('*', phone.Length - 4) + phone[^4..];
    }

    public class SimulatedNotificationService : INotificationService
    {
        private readonly ILogger<SimulatedNotificationService> _logger;

        public SimulatedNotificationService(ILogger<SimulatedNotificationService> logger) => _logger = logger;

        public Task SendBookingConfirmationAsync(Models.Appointment appointment, string serviceName, CancellationToken ct = default)
        {
            _logger.LogInformation(
                "[SIMULATED EMAIL] To: {Email} | Subject: Appointment {Code} confirmed | {Service} on {Start:u} | Manage or cancel: /manage?code={Code}",
                Mask.Email(appointment.CustomerEmail), appointment.ConfirmationCode, serviceName, appointment.StartTime);

            _logger.LogInformation(
                "[SIMULATED SMS] To: {Phone} | Your appointment {Code} is confirmed for {Start:u}",
                Mask.Phone(appointment.CustomerPhone), appointment.ConfirmationCode, appointment.StartTime);

            return Task.CompletedTask;
        }

        public Task SendCancellationAsync(Models.Appointment appointment, string serviceName, CancellationToken ct = default)
        {
            _logger.LogInformation(
                "[SIMULATED EMAIL] To: {Email} | Subject: Appointment {Code} cancelled | {Service} on {Start:u}",
                Mask.Email(appointment.CustomerEmail), appointment.ConfirmationCode, serviceName, appointment.StartTime);

            _logger.LogInformation(
                "[SIMULATED SMS] To: {Phone} | Your appointment {Code} has been cancelled",
                Mask.Phone(appointment.CustomerPhone), appointment.ConfirmationCode);

            return Task.CompletedTask;
        }
    }
}
