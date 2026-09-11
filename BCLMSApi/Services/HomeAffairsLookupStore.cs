using System.Data;
using System.Text.Json;
using System.Text.Json.Nodes;
using BCLMSApi.Data;
using BCLMSApi.Models;
using Microsoft.Data.SqlClient;

namespace BCLMSApi.Services;

public sealed class HomeAffairsLookupStore(Datalayer database, IHanisVerificationService hanis, ISystemSettingsService settings, IConfiguration configuration)
{
    public async Task<HanisVerificationService.HanisResult?> GetAsync(
        InternalApplicationDetailResponse application, UserTokenPayload user, bool refresh, bool savedOnly)
    {
        // A session lock prevents concurrent requests on different API instances from duplicating a paid lookup.
        await using var command = database.CreateTextCommand("""
            DECLARE @result INT;
            EXEC @result = sys.sp_getapplock @Resource=@LockName, @LockMode='Exclusive',
                @LockOwner='Session', @LockTimeout=10000;
            SELECT @result;
            """);
        command.Parameters.Add("@LockName", SqlDbType.NVarChar, 255).Value = $"BCLMS:HANIS:{application.ApplicationId}";
        await command.Connection!.OpenAsync();
        if (Convert.ToInt32(await command.ExecuteScalarAsync()) < 0)
            throw new ArgumentException("Another identity lookup is running for this application. Please try again shortly.");
        try
        {
            var source = (await settings.GetHanisSettingsAsync()).BaseUrl.TrimEnd('/');
            var now = DateTime.UtcNow;
            if (!refresh)
            {
                command.Parameters.Clear();
                command.CommandText = """
                    SELECT IdentityJson, PhotoJpeg, LookedUpAtUtc, ExpiresAtUtc
                    FROM dbo.HomeAffairsIdentityLookups
                    WHERE ApplicationId=@ApplicationId AND IdNumber=@IdNumber COLLATE Latin1_General_100_BIN2
                      AND ApplicantName=@ApplicantName COLLATE Latin1_General_100_BIN2
                      AND SourceEnvironment=@Source COLLATE Latin1_General_100_BIN2
                      AND ExpiresAtUtc>@Now;
                    """;
                BindIdentity(command, application, source);
                command.Parameters.Add("@Now", SqlDbType.DateTime2).Value = now;
                await using var reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    var saved = JsonSerializer.Deserialize<HanisVerificationService.HanisResult>(reader.GetString(0));
                    if (saved is not null)
                    {
                        saved.PhotoJpegBase64 = reader.IsDBNull(1) ? null : Convert.ToBase64String((byte[])reader[1]);
                        saved.LookedUpAtUtc = DateTime.SpecifyKind(reader.GetDateTime(2), DateTimeKind.Utc);
                        saved.ExpiresAtUtc = DateTime.SpecifyKind(reader.GetDateTime(3), DateTimeKind.Utc);
                        saved.IsCached = true;
                        // Re-evaluate the current rules rather than trusting a previously saved decision.
                        try
                        {
                            HanisVerificationService.ValidateResult(saved, application.IdOrPassportNumber.Trim(), application.ApplicantName);
                            saved.CanApprove = true;
                            saved.VerificationMessage = "The saved ID and full name match Home Affairs. Approval will perform a fresh check.";
                        }
                        catch (ArgumentException error)
                        {
                            saved.CanApprove = false;
                            saved.VerificationMessage = error.Message;
                        }
                        return saved;
                    }
                }
            }
            if (savedOnly) return null;

            var result = await hanis.LookupAsync(application.IdOrPassportNumber, application.ApplicantName, user.Username);
            result.LookedUpAtUtc = DateTime.UtcNow;
            result.ExpiresAtUtc = result.LookedUpAtUtc.Value.AddHours(Math.Clamp(configuration.GetValue("Hanis:CacheHours", 24), 1, 168));
            result.IsCached = false;
            var photo = DecodePhoto(result.PhotoJpegBase64);
            var json = JsonSerializer.SerializeToNode(result)!.AsObject();
            json.Remove(nameof(result.PhotoJpegBase64));
            command.Parameters.Clear();
            command.CommandText = """
                SET XACT_ABORT ON;
                BEGIN TRANSACTION;
                IF NOT EXISTS (SELECT 1 FROM dbo.Applications WITH (UPDLOCK, HOLDLOCK)
                    WHERE ApplicationId=@ApplicationId AND Archive_Date IS NULL
                      AND IdOrPassportNumber=@IdNumber COLLATE Latin1_General_100_BIN2
                      AND ApplicantName=@ApplicantName COLLATE Latin1_General_100_BIN2)
                BEGIN
                    ROLLBACK;
                    THROW 50010, 'Application identity changed during lookup.', 1;
                END;
                UPDATE dbo.HomeAffairsIdentityLookups
                SET IdNumber=@IdNumber, ApplicantName=@ApplicantName, SourceEnvironment=@Source,
                    IdentityJson=@Json, PhotoJpeg=@Photo, HanisTransactionId=@TransactionId,
                    LookedUpAtUtc=@LookedUp, ExpiresAtUtc=@Expires, LookedUpByUserId=@UserId
                WHERE ApplicationId=@ApplicationId;
                IF @@ROWCOUNT=0
                    INSERT dbo.HomeAffairsIdentityLookups
                    (ApplicationId,IdNumber,ApplicantName,SourceEnvironment,IdentityJson,PhotoJpeg,
                     HanisTransactionId,LookedUpAtUtc,ExpiresAtUtc,LookedUpByUserId)
                    VALUES (@ApplicationId,@IdNumber,@ApplicantName,@Source,@Json,@Photo,
                            @TransactionId,@LookedUp,@Expires,@UserId);
                COMMIT;
                """;
            BindIdentity(command, application, source);
            command.Parameters.Add("@Json", SqlDbType.NVarChar, -1).Value = json.ToJsonString();
            command.Parameters.Add("@Photo", SqlDbType.VarBinary, -1).Value = (object?)photo ?? DBNull.Value;
            command.Parameters.Add("@TransactionId", SqlDbType.NVarChar, 120).Value =
                result.HanisTransactionId is { Length: <= 120 } reference ? reference : DBNull.Value;
            command.Parameters.Add("@LookedUp", SqlDbType.DateTime2).Value = result.LookedUpAtUtc;
            command.Parameters.Add("@Expires", SqlDbType.DateTime2).Value = result.ExpiresAtUtc;
            command.Parameters.Add("@UserId", SqlDbType.Int).Value = user.UserId;
            await command.ExecuteNonQueryAsync();
            return result;
        }
        finally
        {
            command.Parameters.Clear();
            command.CommandText = "EXEC sys.sp_releaseapplock @Resource=@LockName, @LockOwner='Session';";
            command.Parameters.Add("@LockName", SqlDbType.NVarChar, 255).Value = $"BCLMS:HANIS:{application.ApplicationId}";
            await command.ExecuteNonQueryAsync();
        }
    }

    private static void BindIdentity(SqlCommand command, InternalApplicationDetailResponse application, string source)
    {
        command.Parameters.Add("@ApplicationId", SqlDbType.Int).Value = application.ApplicationId;
        command.Parameters.Add("@IdNumber", SqlDbType.NVarChar, 50).Value = application.IdOrPassportNumber;
        command.Parameters.Add("@ApplicantName", SqlDbType.NVarChar, 150).Value = application.ApplicantName;
        command.Parameters.Add("@Source", SqlDbType.NVarChar, 400).Value = source;
    }

    public static byte[]? DecodePhoto(string? encoded)
    {
        if (string.IsNullOrWhiteSpace(encoded)) return null;
        if (encoded.Length > 7 * 1024 * 1024) throw new ArgumentException("The Home Affairs photo exceeds the storage limit.");
        byte[] bytes;
        try { bytes = Convert.FromBase64String(encoded); }
        catch (FormatException) { throw new ArgumentException("Home Affairs returned an invalid photo. Please refresh the lookup."); }
        if (bytes.Length > 5 * 1024 * 1024 || bytes.Length < 3 || bytes[0] != 0xff || bytes[1] != 0xd8 || bytes[2] != 0xff)
            throw new ArgumentException("Home Affairs returned an invalid JPEG photo. Please refresh the lookup.");
        return bytes;
    }
}
