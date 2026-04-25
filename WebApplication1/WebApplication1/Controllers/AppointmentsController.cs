using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using WebApp.DTOs;
using WebApp.Services;
namespace WebApp.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AppointmentsController : ControllerBase
    {
        private readonly IAppointmentsService _appointmentsService;

        public AppointmentsController(IAppointmentsService appointmentsService)
        {
            _appointmentsService = appointmentsService;
        }

        [HttpGet]
        public async Task<IActionResult> Get([FromQuery]string? staus, [FromQuery] string? patientLastName)
        {
            var appointments = await _appointmentsService.GetAllAppointmentsAsync(staus, patientLastName);
            return Ok(appointments);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(int id)
        {
            var appointment = await _appointmentsService.GetAppointmentsAsync(id);
            
            if (!appointment.Any())
                return NotFound(new ErrorResponseDto( "Appointment not found" ));
            
            return Ok(appointment);
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] CreateAppointmentRequestDto appointment)
        {
            var (newId, error) = await _appointmentsService.CreateAppointmentAsync(appointment);

            if (error != null)
            {
                return Conflict(new ErrorResponseDto (error));
            }

            return Created($"api/appointments/{newId}", new { id = newId });
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Put(int id, [FromBody] UpdateAppointmentRequestDto appointment)
        {
            var (success, error) = await _appointmentsService.UpdateAppointmentAsync(id, appointment);

            if (!success)
            {
                if (error == "NotFound")
                    return NotFound(new ErrorResponseDto ("Appointment not found" ));

                return Conflict(new ErrorResponseDto (error ));
            }

            return Ok();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var (success, error) = await _appointmentsService.DeleteAppointmentAsync(id);

            if (!success)
            {
                if (error == "NotFound")
                    return NotFound(new ErrorResponseDto ("Appointment not found" ));

                return Conflict(new ErrorResponseDto ( error ));
            }

            return NoContent();
        }
}
}
