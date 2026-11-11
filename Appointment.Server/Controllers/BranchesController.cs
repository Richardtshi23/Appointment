using Appointment.Server.Interfaces;
using Appointment.Server.Models;
using Microsoft.AspNetCore.Mvc;

namespace Appointment.Server.Controllers
{
    [ApiController]
    [Route("api/branches")]
    [Produces("application/json")]
    public class BranchesController : ControllerBase
    {
        private readonly IBranchService _branches;

        public BranchesController(IBranchService branches) => _branches = branches;

        [HttpGet]
        [ProducesResponseType(typeof(IReadOnlyList<BranchDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetBranches(CancellationToken ct) =>
            Ok(await _branches.GetBranchesAsync(ct));

        [HttpGet("{branchId:guid}/services")]
        [ProducesResponseType(typeof(IReadOnlyList<ServiceDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetServices(Guid branchId, CancellationToken ct) =>
            Ok(await _branches.GetServicesForBranchAsync(branchId, ct));
    }
}
