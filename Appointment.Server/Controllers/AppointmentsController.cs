using Appointment.Server.Exceptions;
using Appointment.Server.Interfaces;
using Appointment.Server.Models;
using Microsoft.AspNetCore.Mvc;

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
        [ProducesResponseType(typeof(AppointmentResultDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> BookAppointment([FromBody] CreateAppointmentDto dto, CancellationToken ct)
        {
            var result = await _appointments.BookAppointmentAsync(dto, ct);
            return Created($"/api/appointments/{result.Id}", result);
        }

        [HttpPost("{id:guid}/cancel")]
        [ProducesResponseType(typeof(AppointmentResultDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> CancelAppointment(Guid id, CancellationToken ct)
        {
            var result = await _appointments.CancelAppointmentAsync(id, ct);
            return Ok(result);
        }

        [HttpGet("lookup")]
        [ProducesResponseType(typeof(AppointmentResultDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> FindAppointment([FromQuery] string code, [FromQuery] string email, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(email))
                throw new BusinessRuleException("Confirmation code and email are required.");

            return Ok(await _appointments.FindAppointmentAsync(code, email, ct));
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
