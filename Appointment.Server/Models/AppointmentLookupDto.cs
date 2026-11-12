using System.ComponentModel.DataAnnotations;

namespace Appointment.Server.Models
{
    public class AppointmentLookupDto
    {
        [Required]
        [StringLength(20)]
        public string ConfirmationCode { get; set; } = string.Empty;

        [Required]
        [EmailAddress(ErrorMessage = "Invalid email format.")]
        [StringLength(150)]
        public string Email { get; set; } = string.Empty;
    }
}
