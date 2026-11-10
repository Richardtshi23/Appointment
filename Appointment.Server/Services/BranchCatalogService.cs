using Appointment.Server.Exceptions;
using Appointment.Server.Interfaces;
using Appointment.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace Appointment.Server.Services
{
    public class BranchCatalogService : IBranchService
    {
        private readonly AppDBContext _context;
        private readonly ILogger<BranchCatalogService> _logger;

        public BranchCatalogService(AppDBContext context, ILogger<BranchCatalogService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<IReadOnlyList<BranchDto>> GetBranchesAsync(CancellationToken ct = default)
        {
            return await _context.Branches.AsNoTracking()
                .OrderBy(b => b.Name)
                .Select(b => new BranchDto(b.Id, b.Name, b.Address, b.PhoneNumber))
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<ServiceDto>> GetServicesForBranchAsync(Guid branchId, CancellationToken ct = default)
        {
            if (!await _context.Branches.AnyAsync(b => b.Id == branchId, ct))
            {
                _logger.LogWarning("Services requested for unknown branch {BranchId}", branchId);
                throw new NotFoundException("Branch not found.");
            }

            return await _context.BranchServices.AsNoTracking()
                .Where(bs => bs.BranchId == branchId)
                .Select(bs => bs.Service)
                .OrderBy(s => s.Name)
                .Select(s => new ServiceDto(s.Id, s.Name, s.Description, s.DurationMinutes))
                .ToListAsync(ct);
        }
    }
}
