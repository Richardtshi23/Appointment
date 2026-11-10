namespace Appointment.Server.Models
{
    public record BranchDto(Guid Id, string Name, string Address, string PhoneNumber);

    public record ServiceDto(Guid Id, string Name, string Description, int DurationMinutes);
}
