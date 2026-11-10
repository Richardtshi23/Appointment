namespace Appointment.Server.Models
{
    public class Service:BaseEntity
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int DurationMinutes { get; set; } // e.g., 30 mins

        // Navigation property
        public ICollection<BranchService> BranchServices { get; set; } = new List<BranchService>();
        public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
    }
}
