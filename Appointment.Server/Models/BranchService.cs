namespace Appointment.Server.Models
{
    public class BranchService
    {
        public Guid BranchId { get; set; }
        public Branch Branch { get; set; } = null!;
        public Guid ServiceId { get; set; }
        public Service Service { get; set; } = null!;
    }
}
