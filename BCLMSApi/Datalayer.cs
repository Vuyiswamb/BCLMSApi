using System.Data;
using Microsoft.Data.SqlClient;

namespace BCLMSApi.Data;

public class Datalayer
{
    private readonly string connectionString;

    public Datalayer(IConfiguration configuration)
    {
        connectionString = configuration.GetConnectionString("BclmsConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:BclmsConnection is missing from configuration.");
    }

    public SqlCommand CreateStoredProcedureCommand(string procedureName)
    {
        return new SqlCommand(procedureName, new SqlConnection(connectionString))
        {
            CommandType = CommandType.StoredProcedure
        };
    }

    public SqlCommand CreateTextCommand(string commandText)
    {
        return new SqlCommand(commandText, new SqlConnection(connectionString))
        {
            CommandType = CommandType.Text
        };
    }

    private static bool HasColumn(IDataRecord reader, string columnName)
    {
        for (var i = 0; i < reader.FieldCount; i++)
        {
            if (string.Equals(reader.GetName(i), columnName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string? GetStringValue(IDataRecord reader, string columnName)
    {
        return HasColumn(reader, columnName) && reader[columnName] != DBNull.Value
            ? reader[columnName].ToString()
            : null;
    }

    private static T? GetNullableValue<T>(IDataRecord reader, string columnName) where T : struct
    {
        return HasColumn(reader, columnName) && reader[columnName] != DBNull.Value
            ? (T)Convert.ChangeType(reader[columnName], typeof(T))
            : null;
    }
}
