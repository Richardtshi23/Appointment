namespace Appointment.Server.Models
{
    public class AppointmentResultDto
    {
        public Guid Id { get; set; }
        public string ConfirmationCode { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}
