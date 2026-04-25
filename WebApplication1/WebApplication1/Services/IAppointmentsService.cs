using WebApp.DTOs;
namespace WebApp.Services;
public interface IAppointmentsService
{
    Task<IEnumerable<AppointmentListDto>> GetAllAppointmentsAsync(string status, string patientLastName);
    Task<IEnumerable<AppointmentDetailsDto>> GetAppointmentsAsync(int id);
}