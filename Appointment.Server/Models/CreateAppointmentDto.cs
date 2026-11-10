using System.ComponentModel.DataAnnotations;

namespace Appointment.Server.Models
{
    public class CreateAppointmentDto
    {
        [Required]
        public Guid BranchId { get; set; }

        [Required]
        public Guid ServiceId { get; set; }

        [Required]
        [StringLength(100, ErrorMessage = "Customer name cannot exceed 100 characters.")]
        public string CustomerName { get; set; } = string.Empty;

        [Required]
        [EmailAddress(ErrorMessage = "Invalid email format.")]
        public string CustomerEmail { get; set; } = string.Empty;

        [Required]
        [Phone(ErrorMessage = "Invalid phone number format.")]
        public string CustomerPhone { get; set; } = string.Empty;

        [Required]
        public DateTime StartTime { get; set; }
    }
}
