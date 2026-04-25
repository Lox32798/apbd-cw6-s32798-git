using WebApp.DTOs;
namespace WebApp.Services;
public interface IAppointmentsService
{
    Task<IEnumerable<AppointmentListDto>> GetAllAppointmentsAsync(string status, string patientLastName);
    Task<IEnumerable<AppointmentDetailsDto>> GetAppointmentsAsync(int id);
    Task<(int new_id, string error)> CreateAppointmentAsync(CreateAppointmentRequestDto dto);
    Task<(bool success, string error)> UpdateAppointmentAsync(int id, UpdateAppointmentRequestDto dto);
    Task<(bool success, string error)> DeleteAppointmentAsync(int id);
}