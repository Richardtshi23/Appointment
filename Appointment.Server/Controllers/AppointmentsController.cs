using Appointment.Server.Interfaces;
using Appointment.Server.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Appointment.Server.Controllers
{
    [ApiController]
    [Route("api/appointments")]
    [Produces("application/json")]
    public class AppointmentsController : ControllerBase
    {
        private readonly IAppointmentService _appointments;

        public AppointmentsController(IAppointmentService appointments) => _appointments = appointments;

        [HttpPost]
        [EnableRateLimiting("booking")]
        [ProducesResponseType(typeof(AppointmentResultDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> BookAppointment([FromBody] CreateAppointmentDto dto, CancellationToken ct)
        {
            var result = await _appointments.BookAppointmentAsync(dto, ct);
            return Created($"/api/appointments/{result.Id}", result);
        }

        [HttpPost("cancel")]
        [EnableRateLimiting("lookup")]
        [ProducesResponseType(typeof(AppointmentResultDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> CancelAppointment([FromBody] AppointmentLookupDto dto, CancellationToken ct)
        {
            var result = await _appointments.CancelAppointmentAsync(dto.ConfirmationCode, dto.Email, ct);
            return Ok(result);
        }

        [HttpPost("lookup")]
        [EnableRateLimiting("lookup")]
        [ProducesResponseType(typeof(AppointmentResultDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> FindAppointment([FromBody] AppointmentLookupDto dto, CancellationToken ct)
        {
            return Ok(await _appointments.FindAppointmentAsync(dto.ConfirmationCode, dto.Email, ct));
        }

        /// <summary>Returns the start times (HH:mm) still free on the given date.</summary>
        [HttpGet("available-slots")]
        [ProducesResponseType(typeof(IEnumerable<string>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetAvailableSlots(
            [FromQuery] Guid branchId, [FromQuery] Guid serviceId, [FromQuery] DateTime date, CancellationToken ct)
        {
            var slots = await _appointments.GetAvailableSlotsAsync(branchId, serviceId, date, ct);
            return Ok(slots.Select(s => s.ToString(@"hh\:mm")));
        }
    }
}
