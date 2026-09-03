using BCLMSApi.Data;

namespace BCLMSApi.Services;

public class SmsService(Datalayer datalayer, IConfiguration configuration) : ISmsService
{
    public async Task SendSmsAsync(string mobileNumber, string message)
    {
        var departmentNumber = configuration.GetValue<int?>("Sms:DepartmentNo") ?? 1;
        await using var command = datalayer.CreateStoredProcedureCommand("SMS_Manager.dbo.spx_SendSMS");
        command.Parameters.AddWithValue("@DEPARTMENT_NO", departmentNumber);
        command.Parameters.AddWithValue("@CELL_NUMBER", mobileNumber);
        command.Parameters.AddWithValue("@MESSAGE", message);
        await command.Connection!.OpenAsync();
        await command.ExecuteNonQueryAsync();
    }
}
