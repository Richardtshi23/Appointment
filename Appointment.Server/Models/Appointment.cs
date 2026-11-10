namespace Appointment.Server.Models
{
    public class Appointment: BaseEntity
    {
        public Guid BranchId { get; set; }
        public Branch Branch { get; set; } = null!;

        public Guid ServiceId { get; set; }
        public Service Service { get; set; } = null!;

        public string CustomerName { get; set; } = string.Empty;
        public string CustomerEmail { get; set; } = string.Empty;
        public string CustomerPhone { get; set; } = string.Empty;

        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }

        public AppointmentStatus Status { get; set; } = AppointmentStatus.Confirmed;
        public string ConfirmationCode { get; set; } = string.Empty;
    }

    public enum AppointmentStatus
    {
        Pending,
        Confirmed,
        Cancelled
    }
}
