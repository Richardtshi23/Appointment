namespace Appointment.Server.Models
{
    public class Branch : BaseEntity
    {
        public string Name { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public ICollection<BranchService> BranchServices { get; set; } = new List<BranchService>();
        public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
    }
}

