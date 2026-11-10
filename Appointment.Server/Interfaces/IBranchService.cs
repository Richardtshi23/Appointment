using Appointment.Server.Models;

namespace Appointment.Server.Interfaces
{
    public interface IBranchService
    {
        Task<IReadOnlyList<BranchDto>> GetBranchesAsync(CancellationToken ct = default);
        Task<IReadOnlyList<ServiceDto>> GetServicesForBranchAsync(Guid branchId, CancellationToken ct = default);
    }
}
