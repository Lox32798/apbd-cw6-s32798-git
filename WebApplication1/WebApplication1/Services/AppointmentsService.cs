using Microsoft.Data.SqlClient;
using System.Data;
using WebApp.DTOs;
namespace WebApp.Services;
public class AppointmentsService : IAppointmentsService
{
    private readonly string _connectionString;

    public AppointmentsService(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("Default");
    }
    
    public async Task<IEnumerable<AppointmentListDto>> GetAllAppointmentsAsync(string status, string patientLastName)
    {
        var query = """
        SELECT
        a.IdAppointment,
        a.Status
            FROM dbo.Appointments a
        JOIN dbo.Patients p ON p.IdPatient = a.IdPatient
        WHERE (@Status IS NULL OR a.Status = @Status)
        AND (@PatientLastName IS NULL OR p.LastName = @PatientLastName)
        ORDER BY a.AppointmentDate;
        """;
        
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand();
        command.Connection = connection;
        command.CommandText = query;
        
        command.Parameters.Add("@Status", SqlDbType.NVarChar, 50).Value =
            (object?)status ?? DBNull.Value;
        command.Parameters.Add("@PatientLastName", SqlDbType.NVarChar, 100).Value =
            (object?)patientLastName ?? DBNull.Value;
        
        var reader = await command.ExecuteReaderAsync();
        
        var appointments = new List<AppointmentListDto>();
        while (await reader.ReadAsync())
        {
            var appointment = new AppointmentListDto()
            {
                IdAppointment = reader.GetInt32(0),
                Status = reader.GetString(1),
            };
            appointments.Add(appointment);
        }
        
        return appointments;
    }
    public async Task<IEnumerable<AppointmentDetailsDto>> GetAppointmentsAsync(int id){
        var query = """
                    SELECT
                    a.IdAppointment,
                    a.AppointmentDate,
                    a.Status,
                    a.Reason
                        FROM dbo.Appointments a
                    JOIN dbo.Patients p ON p.IdPatient = a.IdPatient
                    WHERE ( a.IdAppointment = @id)
                    """;
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand();
        command.Connection = connection;
        command.CommandText = query;
        command.Parameters.Add("@id", SqlDbType.Int).Value = id;
        
        var reader = await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
            return Enumerable.Empty<AppointmentDetailsDto>();

        var appointment = new AppointmentDetailsDto
        {
            IdAppointment = reader.GetInt32(0),
            AppointmentDate = reader.GetDateTime(1),
            Status = reader.GetString(2),
            Reason = reader.GetString(3)
        };
        return new List<AppointmentDetailsDto> { appointment };
    }

    public async Task<(int new_id, string error)> CreateAppointmentAsync(CreateAppointmentRequestDto dto)
    {
        var query = """
                        INSERT INTO dbo.Appointments (IdPatient, IdDoctor, AppointmentDate, Status, Reason)
                        OUTPUT INSERTED.IdAppointment
                        VALUES (@IdPatient, @IdDoctor, @AppointmentDate, N'Scheduled', @Reason)
                    """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(query, connection);

        command.Parameters.Add("@IdPatient", SqlDbType.Int).Value = dto.IdPatient;
        command.Parameters.Add("@IdDoctor", SqlDbType.Int).Value = dto.IdDoctor;
        command.Parameters.Add("@AppointmentDate", SqlDbType.DateTime2).Value = dto.AppointmentDate;
        command.Parameters.Add("@Reason", SqlDbType.NVarChar, 250).Value = dto.Reason;

        var newId = (int)await command.ExecuteScalarAsync();

        return (newId, null);
    }
    public async Task<(bool success, string error)> UpdateAppointmentAsync(int id, UpdateAppointmentRequestDto dto)
{
    await using var connection = new SqlConnection(_connectionString);
    await connection.OpenAsync();

    var checkQuery = """
        SELECT Status, AppointmentDate
        FROM dbo.Appointments
        WHERE IdAppointment = @Id
    """;

    await using var checkCmd = new SqlCommand(checkQuery, connection);
    checkCmd.Parameters.Add("@Id", SqlDbType.Int).Value = id;

    await using var reader = await checkCmd.ExecuteReaderAsync();

    if (!await reader.ReadAsync())
        return (false, "NotFound");

    var currentStatus = reader.GetString(0);
    var currentDate = reader.GetDateTime(1);

    await reader.CloseAsync();
    
    var allowedStatuses = new[] { "Scheduled", "Completed", "Cancelled" };
    if (!allowedStatuses.Contains(dto.Status))
        return (false, "Invalid status");
    
    if (currentStatus == "Completed" && dto.AppointmentDate != currentDate)
        return (false, "Cannot change date of completed appointment");
    
    var patientCmd = new SqlCommand(
        "SELECT COUNT(1) FROM dbo.Patients WHERE IdPatient = @Id AND IsActive = 1",
        connection);
    patientCmd.Parameters.Add("@Id", SqlDbType.Int).Value = dto.IdPatient;

    if ((int)await patientCmd.ExecuteScalarAsync() == 0)
        return (false, "Patient not found or inactive");
    
    var doctorCmd = new SqlCommand(
        "SELECT COUNT(1) FROM dbo.Doctors WHERE IdDoctor = @Id AND IsActive = 1",
        connection);
    doctorCmd.Parameters.Add("@Id", SqlDbType.Int).Value = dto.IdDoctor;

    if ((int)await doctorCmd.ExecuteScalarAsync() == 0)
        return (false, "Doctor not found or inactive");

    var conflictQuery = """
        SELECT COUNT(1)
        FROM dbo.Appointments
        WHERE IdDoctor = @IdDoctor
          AND AppointmentDate = @Date
          AND IdAppointment <> @Id
          AND Status = 'Scheduled'
    """;

    var conflictCmd = new SqlCommand(conflictQuery, connection);
    conflictCmd.Parameters.Add("@IdDoctor", SqlDbType.Int).Value = dto.IdDoctor;
    conflictCmd.Parameters.Add("@Date", SqlDbType.DateTime2).Value = dto.AppointmentDate;
    conflictCmd.Parameters.Add("@Id", SqlDbType.Int).Value = id;

    if ((int)await conflictCmd.ExecuteScalarAsync() > 0)
        return (false, "Doctor already has an appointment at this time");
    
    var updateQuery = """
        UPDATE dbo.Appointments
        SET IdPatient = @IdPatient,
            IdDoctor = @IdDoctor,
            AppointmentDate = @Date,
            Status = @Status,
            Reason = @Reason
        WHERE IdAppointment = @Id
    """;

    var updateCmd = new SqlCommand(updateQuery, connection);
    updateCmd.Parameters.Add("@IdPatient", SqlDbType.Int).Value = dto.IdPatient;
    updateCmd.Parameters.Add("@IdDoctor", SqlDbType.Int).Value = dto.IdDoctor;
    updateCmd.Parameters.Add("@Date", SqlDbType.DateTime2).Value = dto.AppointmentDate;
    updateCmd.Parameters.Add("@Status", SqlDbType.NVarChar, 50).Value = dto.Status;
    updateCmd.Parameters.Add("@Reason", SqlDbType.NVarChar, 250).Value = dto.Reason;
    updateCmd.Parameters.Add("@Id", SqlDbType.Int).Value = id;

    await updateCmd.ExecuteNonQueryAsync();

    return (true, null);
}
    public async Task<(bool success, string error)> DeleteAppointmentAsync(int id)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        
        var checkQuery = """
                             SELECT Status
                             FROM dbo.Appointments
                             WHERE IdAppointment = @Id
                         """;

        await using var checkCmd = new SqlCommand(checkQuery, connection);
        checkCmd.Parameters.Add("@Id", SqlDbType.Int).Value = id;

        var statusObj = await checkCmd.ExecuteScalarAsync();

        if (statusObj == null)
            return (false, "NotFound");

        var status = (string)statusObj;

        if (status == "Completed")
            return (false, "Cannot delete completed appointment");
        
        var deleteCmd = new SqlCommand(
            "DELETE FROM dbo.Appointments WHERE IdAppointment = @Id",
            connection);

        deleteCmd.Parameters.Add("@Id", SqlDbType.Int).Value = id;

        await deleteCmd.ExecuteNonQueryAsync();

        return (true, null);
    }
}