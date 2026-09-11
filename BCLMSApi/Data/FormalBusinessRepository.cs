using System.Data;
using System.Security.Cryptography;
using BCLMSApi.Models;
using BCLMSApi.Services;
using Microsoft.Data.SqlClient;

namespace BCLMSApi.Data;

public class FormalBusinessRepository(Datalayer datalayer) : IFormalBusinessRepository
{
    public async Task<List<AttachmentTypeResponse>> GetAttachmentTypesAsync()
    {
        try
        {
            var results = new List<AttachmentTypeResponse>();
            await using var command = datalayer.CreateStoredProcedureCommand("dbo.usp_AttachmentTypes_GetActive");

            await command.Connection!.OpenAsync();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(new AttachmentTypeResponse
                {
                    AttachmentTypeId = reader.GetInt32(reader.GetOrdinal("AttachmentTypeId")),
                    TypeName = reader.GetString(reader.GetOrdinal("TypeName")),
                    Description = reader.IsDBNull(reader.GetOrdinal("Description")) ? null : reader.GetString(reader.GetOrdinal("Description")),
                    IsRequired = reader.GetBoolean(reader.GetOrdinal("IsRequired")),
                    MaxFileSizeBytes = reader.GetInt64(reader.GetOrdinal("MaxFileSizeBytes")),
                    AllowedExtension = reader.GetString(reader.GetOrdinal("AllowedExtension")),
                    AllowedContentType = reader.GetString(reader.GetOrdinal("AllowedContentType"))
                });
            }

            return results;
        }
        catch (SqlException ex)
        {
            throw CreateDatabaseException("load attachment types", ex);
        }
    }

    public async Task<List<TownshipResponse>> GetTownshipsAsync()
    {
        try
        {
            var results = new List<TownshipResponse>();
            await using var command = datalayer.CreateStoredProcedureCommand("dbo.usp_Townships_GetActive");

            await command.Connection!.OpenAsync();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(new TownshipResponse
                {
                    TownshipId = reader.GetInt32(reader.GetOrdinal("TownshipId")),
                    TownshipName = reader.GetString(reader.GetOrdinal("TownshipName")),
                    WardNumber = reader.GetString(reader.GetOrdinal("WardNumber")),
                    RegionName = HasColumn(reader, "RegionName") && !reader.IsDBNull(reader.GetOrdinal("RegionName")) ? reader.GetString(reader.GetOrdinal("RegionName")) : null,
                    AreaCategory = HasColumn(reader, "AreaCategory") && !reader.IsDBNull(reader.GetOrdinal("AreaCategory")) ? reader.GetString(reader.GetOrdinal("AreaCategory")) : null
                });
            }

            return results;
        }
        catch (SqlException ex)
        {
            throw CreateDatabaseException("load townships", ex);
        }
    }

    public async Task<List<CustomerBusinessResponse>> GetCustomerBusinessesAsync(int userId)
    {
        try
        {
            var businesses = new List<CustomerBusinessResponse>();
            await using var command = datalayer.CreateStoredProcedureCommand("dbo.usp_CustomerBusinesses_GetForUser");
            command.Parameters.AddWithValue("@UserId", userId);

            await command.Connection!.OpenAsync();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                businesses.Add(MapCustomerBusiness(reader));
            }

            return businesses;
        }
        catch (SqlException ex)
        {
            throw CreateDatabaseException("load customer businesses", ex);
        }
    }

    public async Task<CustomerBusinessResponse> CreateCustomerBusinessAsync(int userId, CustomerBusinessSaveRequest request)
    {
        try
        {
            await using var command = datalayer.CreateStoredProcedureCommand("dbo.usp_CustomerBusinesses_Create");
            AddCustomerBusinessParameters(command, userId, request);

            await command.Connection!.OpenAsync();
            await using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                throw new InvalidOperationException("The business could not be saved.");
            }

            return MapCustomerBusiness(reader);
        }
        catch (SqlException ex)
        {
            throw CreateDatabaseException("save customer business", ex);
        }
    }

    public async Task<CustomerBusinessResponse?> UpdateCustomerBusinessAsync(int userId, int businessId, CustomerBusinessSaveRequest request)
    {
        try
        {
            await using var command = datalayer.CreateStoredProcedureCommand("dbo.usp_CustomerBusinesses_Update");
            command.Parameters.AddWithValue("@BusinessId", businessId);
            AddCustomerBusinessParameters(command, userId, request);

            await command.Connection!.OpenAsync();
            await using var reader = await command.ExecuteReaderAsync();
            return await reader.ReadAsync() ? MapCustomerBusiness(reader) : null;
        }
        catch (SqlException ex)
        {
            throw CreateDatabaseException("update customer business", ex);
        }
    }

    public async Task<CustomerBusinessResponse?> GetCustomerBusinessAsync(int userId, int businessId)
    {
        try
        {
            await using var command = datalayer.CreateStoredProcedureCommand("dbo.usp_CustomerBusinesses_GetById");
            command.Parameters.AddWithValue("@UserId", userId);
            command.Parameters.AddWithValue("@BusinessId", businessId);

            await command.Connection!.OpenAsync();
            await using var reader = await command.ExecuteReaderAsync();
            return await reader.ReadAsync() ? MapCustomerBusiness(reader) : null;
        }
        catch (SqlException ex)
        {
            throw CreateDatabaseException("load customer business", ex);
        }
    }

    public async Task<bool> DeactivateCustomerBusinessAsync(int userId, int businessId)
    {
        try
        {
            await using var command = datalayer.CreateStoredProcedureCommand("dbo.usp_CustomerBusinesses_Deactivate");
            command.Parameters.AddWithValue("@UserId", userId);
            command.Parameters.AddWithValue("@BusinessId", businessId);

            await command.Connection!.OpenAsync();
            return Convert.ToInt32(await command.ExecuteScalarAsync()) > 0;
        }
        catch (SqlException ex)
        {
            throw CreateDatabaseException("deactivate customer business", ex);
        }
    }

    public async Task<TrackingApplicationResponse?> TrackApplicationAsync(string trackingNumber)
    {
        try
        {
            await using var command = datalayer.CreateStoredProcedureCommand("dbo.usp_Applications_TrackByTrackingNumber");
            command.Parameters.AddWithValue("@TrackingNumber", trackingNumber.Trim());

            await command.Connection!.OpenAsync();
            await using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                return null;
            }

            var application = new TrackingApplicationResponse
            {
                ApplicationId = reader.GetInt32(reader.GetOrdinal("ApplicationId")),
                TrackingNumber = reader.GetString(reader.GetOrdinal("TrackingNumber")),
                BusinessName = reader.GetString(reader.GetOrdinal("BusinessName")),
                LicenceType = reader.GetString(reader.GetOrdinal("LicenceType")),
                SubmittedDate = reader.GetDateTime(reader.GetOrdinal("SubmittedDate")).ToString("yyyy-MM-dd"),
                CurrentStage = reader.GetString(reader.GetOrdinal("CurrentStage")),
                Status = reader.GetString(reader.GetOrdinal("Status")),
                ProofOfPaymentUploaded = HasColumn(reader, "ProofOfPaymentUploaded") && reader.GetBoolean(reader.GetOrdinal("ProofOfPaymentUploaded"))
            };

            if (await reader.NextResultAsync())
            {
                while (await reader.ReadAsync())
                {
                    application.Steps.Add(new TrackingWorkflowStepResponse
                    {
                        Name = reader.GetString(reader.GetOrdinal("StepName")),
                        Group = reader.GetString(reader.GetOrdinal("GroupName")),
                        Status = reader.GetString(reader.GetOrdinal("Status"))
                    });
                }
            }

            return application;
        }
        catch (SqlException ex)
        {
            throw CreateDatabaseException("track application", ex);
        }
    }

    public async Task<List<InternalApplicationSummaryResponse>> GetInternalApplicationsAsync(int? customerUserId = null, bool hasAllRegions = true, IReadOnlyCollection<string>? regionNames = null)
    {
        try
        {
            var applications = new List<InternalApplicationSummaryResponse>();
            await using var command = datalayer.CreateStoredProcedureCommand("dbo.usp_Applications_GetInternal");
            command.Parameters.AddWithValue("@CustomerUserId", customerUserId.HasValue ? customerUserId.Value : DBNull.Value);
            command.Parameters.AddWithValue("@HasAllRegions", hasAllRegions);
            command.Parameters.AddWithValue("@RegionNames", string.Join(",", regionNames ?? []));

            await command.Connection!.OpenAsync();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                applications.Add(new InternalApplicationSummaryResponse
                {
                    ApplicationId = reader.GetInt32(reader.GetOrdinal("ApplicationId")),
                    TrackingNumber = reader.GetString(reader.GetOrdinal("TrackingNumber")),
                    ApplicantName = reader.GetString(reader.GetOrdinal("ApplicantName")),
                    BusinessName = reader.GetString(reader.GetOrdinal("BusinessName")),
                    LicenceType = reader.GetString(reader.GetOrdinal("LicenceType")),
                    EventStartDate = HasColumn(reader, "EventStartDate") && !reader.IsDBNull(reader.GetOrdinal("EventStartDate")) ? reader.GetDateTime(reader.GetOrdinal("EventStartDate")) : null,
                    EventEndDate = HasColumn(reader, "EventEndDate") && !reader.IsDBNull(reader.GetOrdinal("EventEndDate")) ? reader.GetDateTime(reader.GetOrdinal("EventEndDate")) : null,
                    FoodVendingDetails = HasColumn(reader, "FoodVendingDetailsJson") && !reader.IsDBNull(reader.GetOrdinal("FoodVendingDetailsJson")) ? System.Text.Json.JsonSerializer.Deserialize<FoodVendingDetails>(reader.GetString(reader.GetOrdinal("FoodVendingDetailsJson"))) : null,
                    TradeStandBusinessType = HasColumn(reader, "TradeStandBusinessType") && !reader.IsDBNull(reader.GetOrdinal("TradeStandBusinessType")) ? reader.GetString(reader.GetOrdinal("TradeStandBusinessType")) : null,
                    CurrentStage = reader.GetString(reader.GetOrdinal("CurrentStage")),
                    Status = reader.GetString(reader.GetOrdinal("Status")),
                    SubmittedDate = reader.GetDateTime(reader.GetOrdinal("SubmittedDate")),
                    RegionName = HasColumn(reader, "RegionName") && !reader.IsDBNull(reader.GetOrdinal("RegionName")) ? reader.GetString(reader.GetOrdinal("RegionName")) : null,
                    AreaOrSuburb = HasColumn(reader, "AreaOrSuburb") && !reader.IsDBNull(reader.GetOrdinal("AreaOrSuburb")) ? reader.GetString(reader.GetOrdinal("AreaOrSuburb")) : null,
                    AreaCategory = HasColumn(reader, "AreaCategory") && !reader.IsDBNull(reader.GetOrdinal("AreaCategory")) ? reader.GetString(reader.GetOrdinal("AreaCategory")) : null,
                    AttachmentCount = reader.GetInt32(reader.GetOrdinal("AttachmentCount")),
                    ProofOfPaymentUploaded = HasColumn(reader, "ProofOfPaymentUploaded") && reader.GetBoolean(reader.GetOrdinal("ProofOfPaymentUploaded"))
                });
            }

            return applications;
        }
        catch (SqlException ex)
        {
            throw CreateDatabaseException("load applications", ex);
        }
    }

    public async Task<InternalApplicationDetailResponse?> GetInternalApplicationDetailAsync(int applicationId, int? customerUserId = null, bool hasAllRegions = true, IReadOnlyCollection<string>? regionNames = null)
    {
        try
        {
            await using var command = datalayer.CreateStoredProcedureCommand("dbo.usp_Applications_GetInternalDetail");
            command.Parameters.AddWithValue("@ApplicationId", applicationId);
            command.Parameters.AddWithValue("@CustomerUserId", customerUserId.HasValue ? customerUserId.Value : DBNull.Value);
            command.Parameters.AddWithValue("@HasAllRegions", hasAllRegions);
            command.Parameters.AddWithValue("@RegionNames", string.Join(",", regionNames ?? []));

            await command.Connection!.OpenAsync();
            await using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                return null;
            }

            var application = new InternalApplicationDetailResponse
            {
                ApplicationId = reader.GetInt32(reader.GetOrdinal("ApplicationId")),
                TrackingNumber = reader.GetString(reader.GetOrdinal("TrackingNumber")),
                BusinessId = HasColumn(reader, "BusinessId") && !reader.IsDBNull(reader.GetOrdinal("BusinessId")) ? reader.GetInt32(reader.GetOrdinal("BusinessId")) : null,
                ApplicantName = reader.GetString(reader.GetOrdinal("ApplicantName")),
                IdOrPassportNumber = reader.GetString(reader.GetOrdinal("IdOrPassportNumber")),
                EmailAddress = reader.GetString(reader.GetOrdinal("EmailAddress")),
                MobileNumber = reader.GetString(reader.GetOrdinal("MobileNumber")),
                BusinessName = reader.GetString(reader.GetOrdinal("BusinessName")),
                RegistrationNumber = reader.IsDBNull(reader.GetOrdinal("RegistrationNumber")) ? null : reader.GetString(reader.GetOrdinal("RegistrationNumber")),
                LicenceType = reader.GetString(reader.GetOrdinal("LicenceType")),
                EventStartDate = HasColumn(reader, "EventStartDate") && !reader.IsDBNull(reader.GetOrdinal("EventStartDate")) ? reader.GetDateTime(reader.GetOrdinal("EventStartDate")) : null,
                EventEndDate = HasColumn(reader, "EventEndDate") && !reader.IsDBNull(reader.GetOrdinal("EventEndDate")) ? reader.GetDateTime(reader.GetOrdinal("EventEndDate")) : null,
                FoodVendingDetails = HasColumn(reader, "FoodVendingDetailsJson") && !reader.IsDBNull(reader.GetOrdinal("FoodVendingDetailsJson")) ? System.Text.Json.JsonSerializer.Deserialize<FoodVendingDetails>(reader.GetString(reader.GetOrdinal("FoodVendingDetailsJson"))) : null,
                TradeStandBusinessType = HasColumn(reader, "TradeStandBusinessType") && !reader.IsDBNull(reader.GetOrdinal("TradeStandBusinessType")) ? reader.GetString(reader.GetOrdinal("TradeStandBusinessType")) : null,
                ApplicationFee = HasColumn(reader, "ApplicationFee") && !reader.IsDBNull(reader.GetOrdinal("ApplicationFee")) ? reader.GetDecimal(reader.GetOrdinal("ApplicationFee")) : null,
                WardNumber = reader.IsDBNull(reader.GetOrdinal("WardNumber")) ? null : reader.GetString(reader.GetOrdinal("WardNumber")),
                RegionName = HasColumn(reader, "RegionName") && !reader.IsDBNull(reader.GetOrdinal("RegionName")) ? reader.GetString(reader.GetOrdinal("RegionName")) : null,
                AreaOrSuburb = HasColumn(reader, "AreaOrSuburb") && !reader.IsDBNull(reader.GetOrdinal("AreaOrSuburb")) ? reader.GetString(reader.GetOrdinal("AreaOrSuburb")) : null,
                AreaCategory = HasColumn(reader, "AreaCategory") && !reader.IsDBNull(reader.GetOrdinal("AreaCategory")) ? reader.GetString(reader.GetOrdinal("AreaCategory")) : null,
                PhysicalAddress = reader.GetString(reader.GetOrdinal("PhysicalAddress")),
                PostalAddress = HasColumn(reader, "PostalAddress") && !reader.IsDBNull(reader.GetOrdinal("PostalAddress")) ? reader.GetString(reader.GetOrdinal("PostalAddress")) : null,
                Latitude = HasColumn(reader, "Latitude") && !reader.IsDBNull(reader.GetOrdinal("Latitude")) ? reader.GetDecimal(reader.GetOrdinal("Latitude")) : null,
                Longitude = HasColumn(reader, "Longitude") && !reader.IsDBNull(reader.GetOrdinal("Longitude")) ? reader.GetDecimal(reader.GetOrdinal("Longitude")) : null,
                Notes = reader.IsDBNull(reader.GetOrdinal("Notes")) ? null : reader.GetString(reader.GetOrdinal("Notes")),
                CurrentStage = reader.GetString(reader.GetOrdinal("CurrentStage")),
                Status = reader.GetString(reader.GetOrdinal("Status")),
                SubmittedDate = reader.GetDateTime(reader.GetOrdinal("SubmittedDate")),
                AttachmentCount = reader.GetInt32(reader.GetOrdinal("AttachmentCount"))
            };

            if (await reader.NextResultAsync())
            {
                while (await reader.ReadAsync())
                {
                    application.WorkflowSteps.Add(new InternalApplicationWorkflowStepResponse
                    {
                        StepName = reader.GetString(reader.GetOrdinal("StepName")),
                        GroupName = reader.GetString(reader.GetOrdinal("GroupName")),
                        SequenceNumber = reader.GetInt32(reader.GetOrdinal("SequenceNumber")),
                        Status = reader.GetString(reader.GetOrdinal("Status")),
                        StartedDate = reader.IsDBNull(reader.GetOrdinal("StartedDate")) ? null : reader.GetDateTime(reader.GetOrdinal("StartedDate")),
                        CompletedDate = reader.IsDBNull(reader.GetOrdinal("CompletedDate")) ? null : reader.GetDateTime(reader.GetOrdinal("CompletedDate")),
                        Remarks = reader.IsDBNull(reader.GetOrdinal("Remarks")) ? null : reader.GetString(reader.GetOrdinal("Remarks")),
                        ActionedByUserId = HasColumn(reader, "ActionedByUserId") && !reader.IsDBNull(reader.GetOrdinal("ActionedByUserId")) ? reader.GetInt32(reader.GetOrdinal("ActionedByUserId")) : null,
                        ActionedByDisplayName = HasColumn(reader, "ActionedByDisplayName") && !reader.IsDBNull(reader.GetOrdinal("ActionedByDisplayName")) ? reader.GetString(reader.GetOrdinal("ActionedByDisplayName")) : null,
                        DecisionDate = HasColumn(reader, "DecisionDate") && !reader.IsDBNull(reader.GetOrdinal("DecisionDate")) ? reader.GetDateTime(reader.GetOrdinal("DecisionDate")) : null
                    });
                }
            }

            if (await reader.NextResultAsync())
            {
                while (await reader.ReadAsync())
                {
                    application.Attachments.Add(new InternalApplicationAttachmentResponse
                    {
                        ApplicationDocumentId = reader.GetInt32(reader.GetOrdinal("ApplicationDocumentId")),
                        AttachmentTypeId = reader.IsDBNull(reader.GetOrdinal("AttachmentTypeId")) ? null : reader.GetInt32(reader.GetOrdinal("AttachmentTypeId")),
                        DocumentName = reader.GetString(reader.GetOrdinal("DocumentName")),
                        IsRequired = reader.GetBoolean(reader.GetOrdinal("IsRequired")),
                        OriginalFileName = reader.IsDBNull(reader.GetOrdinal("OriginalFileName")) ? null : reader.GetString(reader.GetOrdinal("OriginalFileName")),
                        ContentType = reader.IsDBNull(reader.GetOrdinal("ContentType")) ? null : reader.GetString(reader.GetOrdinal("ContentType")),
                        FileSizeBytes = reader.IsDBNull(reader.GetOrdinal("FileSizeBytes")) ? null : reader.GetInt64(reader.GetOrdinal("FileSizeBytes")),
                        SubmittedDate = reader.IsDBNull(reader.GetOrdinal("SubmittedDate")) ? null : reader.GetDateTime(reader.GetOrdinal("SubmittedDate")),
                        Status = reader.GetString(reader.GetOrdinal("Status")),
                        Remarks = reader.IsDBNull(reader.GetOrdinal("Remarks")) ? null : reader.GetString(reader.GetOrdinal("Remarks"))
                    });
                }
            }

            return application;
        }
        catch (SqlException ex)
        {
            throw CreateDatabaseException("load application detail", ex);
        }
    }

    public async Task<bool> ArchiveApplicationAsync(int applicationId, int archiveUserId)
    {
        try
        {
            await using var command = datalayer.CreateStoredProcedureCommand("dbo.usp_Applications_Archive");
            command.Parameters.AddWithValue("@ApplicationId", applicationId);
            command.Parameters.AddWithValue("@ArchiveUserId", archiveUserId);

            await command.Connection!.OpenAsync();
            return Convert.ToInt32(await command.ExecuteScalarAsync()) > 0;
        }
        catch (SqlException ex)
        {
            throw CreateDatabaseException("archive application", ex);
        }
    }

    public async Task<ApplicationDocumentFileResponse?> GetApplicationDocumentFileAsync(int applicationId, int applicationDocumentId, int? customerUserId = null)
    {
        try
        {
            await using var command = datalayer.CreateStoredProcedureCommand("dbo.usp_ApplicationDocuments_GetFile");
            command.Parameters.AddWithValue("@ApplicationId", applicationId);
            command.Parameters.AddWithValue("@ApplicationDocumentId", applicationDocumentId);
            command.Parameters.AddWithValue("@CustomerUserId", customerUserId.HasValue ? customerUserId.Value : DBNull.Value);

            await command.Connection!.OpenAsync();
            await using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                return null;
            }

            return new ApplicationDocumentFileResponse
            {
                ApplicationDocumentId = reader.GetInt32(reader.GetOrdinal("ApplicationDocumentId")),
                DocumentName = reader.GetString(reader.GetOrdinal("DocumentName")),
                OriginalFileName = reader.IsDBNull(reader.GetOrdinal("OriginalFileName")) ? null : reader.GetString(reader.GetOrdinal("OriginalFileName")),
                ContentType = reader.IsDBNull(reader.GetOrdinal("ContentType")) ? null : reader.GetString(reader.GetOrdinal("ContentType")),
                FileContent = reader.IsDBNull(reader.GetOrdinal("FileContent")) ? [] : (byte[])reader["FileContent"]
            };
        }
        catch (SqlException ex)
        {
            throw CreateDatabaseException("load application document", ex);
        }
    }

    public async Task<bool> CancelCustomerApplicationAsync(int applicationId, int userId)
    {
        try
        {
            await using var command = datalayer.CreateStoredProcedureCommand("dbo.usp_Applications_CancelSubmittedForCustomer");
            command.Parameters.AddWithValue("@ApplicationId", applicationId);
            command.Parameters.AddWithValue("@UserId", userId);

            await command.Connection!.OpenAsync();
            return Convert.ToInt32(await command.ExecuteScalarAsync()) > 0;
        }
        catch (SqlException ex)
        {
            throw CreateDatabaseException("cancel application", ex);
        }
    }

    public async Task<bool> ApplicationExistsAsync(int applicationId, int? customerUserId = null)
    {
        try
        {
            await using var command = datalayer.CreateStoredProcedureCommand("dbo.usp_Applications_Exists");
            command.Parameters.AddWithValue("@ApplicationId", applicationId);
            command.Parameters.AddWithValue("@CustomerUserId", customerUserId.HasValue ? customerUserId.Value : DBNull.Value);

            await command.Connection!.OpenAsync();
            return Convert.ToBoolean(await command.ExecuteScalarAsync());
        }
        catch (SqlException ex)
        {
            throw CreateDatabaseException("check application existence", ex);
        }
    }

    public async Task<AttachmentTypeRecord?> GetAttachmentTypeAsync(int attachmentTypeId)
    {
        try
        {
            await using var command = datalayer.CreateStoredProcedureCommand("dbo.usp_AttachmentTypes_GetById");
            command.Parameters.AddWithValue("@AttachmentTypeId", attachmentTypeId);

            await command.Connection!.OpenAsync();
            await using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                return null;
            }

            return new AttachmentTypeRecord
            {
                AttachmentTypeId = reader.GetInt32(reader.GetOrdinal("AttachmentTypeId")),
                TypeName = reader.GetString(reader.GetOrdinal("TypeName")),
                IsRequired = reader.GetBoolean(reader.GetOrdinal("IsRequired"))
            };
        }
        catch (SqlException ex)
        {
            throw CreateDatabaseException("load attachment type", ex);
        }
    }

    public async Task<int> InsertPdfDocumentAsync(int applicationId, AttachmentTypeRecord attachmentType, string originalFileName, string storedFileName, long fileSizeBytes, byte[] fileContent, string fileHash, string? remarks)
    {
        try
        {
            await using var command = datalayer.CreateStoredProcedureCommand("dbo.usp_ApplicationDocuments_InsertPdf");
            command.Parameters.AddWithValue("@ApplicationId", applicationId);
            command.Parameters.AddWithValue("@AttachmentTypeId", attachmentType.AttachmentTypeId);
            command.Parameters.AddWithValue("@DocumentName", attachmentType.TypeName);
            command.Parameters.AddWithValue("@IsRequired", attachmentType.IsRequired);
            command.Parameters.AddWithValue("@OriginalFileName", originalFileName);
            command.Parameters.AddWithValue("@StoredFileName", storedFileName);
            command.Parameters.AddWithValue("@ContentType", "application/pdf");
            command.Parameters.AddWithValue("@FileSizeBytes", fileSizeBytes);
            command.Parameters.Add("@FileContent", SqlDbType.VarBinary, -1).Value = fileContent;
            command.Parameters.AddWithValue("@FileSha256Hash", fileHash);
            command.Parameters.AddWithValue("@Remarks", string.IsNullOrWhiteSpace(remarks) ? DBNull.Value : remarks);

            await command.Connection!.OpenAsync();
            return Convert.ToInt32(await command.ExecuteScalarAsync());
        }
        catch (SqlException ex)
        {
            throw CreateDatabaseException("save application document", ex);
        }
    }

    public async Task<ApplicationSubmitResponse> SubmitApplicationAsync(ApplicationSubmitRequest request, string licenceType, int userId)
    {
        try
        {
            await using var command = datalayer.CreateStoredProcedureCommand("dbo.usp_Applications_Insert");
            command.Parameters.AddWithValue("@ApplicantName", request.ApplicantName.Trim());
            command.Parameters.AddWithValue("@UserId", userId);
            command.Parameters.AddWithValue("@BusinessId", request.BusinessId.HasValue ? request.BusinessId.Value : DBNull.Value);
            command.Parameters.AddWithValue("@IdOrPassportNumber", request.IdNumber.Trim());
            command.Parameters.AddWithValue("@EmailAddress", request.Email.Trim());
            command.Parameters.AddWithValue("@MobileNumber", request.Phone.Trim());
            command.Parameters.AddWithValue("@BusinessName", request.BusinessName.Trim());
            command.Parameters.AddWithValue("@RegistrationNumber", string.IsNullOrWhiteSpace(request.RegistrationNumber) ? DBNull.Value : request.RegistrationNumber.Trim());
            command.Parameters.AddWithValue("@LicenceType", licenceType);
            command.Parameters.AddWithValue("@EventStartDate", (object?)request.EventStartDate?.Date ?? DBNull.Value);
            command.Parameters.AddWithValue("@EventEndDate", (object?)request.EventEndDate?.Date ?? DBNull.Value);
            command.Parameters.AddWithValue("@FoodVendingDetailsJson", request.FoodVendingDetails is null ? DBNull.Value : System.Text.Json.JsonSerializer.Serialize(request.FoodVendingDetails));
            command.Parameters.AddWithValue("@TradeStandBusinessType", string.IsNullOrWhiteSpace(request.TradeStandBusinessType) ? DBNull.Value : request.TradeStandBusinessType.Trim());
            command.Parameters.AddWithValue("@ApplicationFee", request.ApplicationFee.HasValue ? request.ApplicationFee.Value : DBNull.Value);
            command.Parameters.AddWithValue("@WardNumber", string.IsNullOrWhiteSpace(request.WardNumber) ? DBNull.Value : request.WardNumber.Trim());
            command.Parameters.AddWithValue("@RegionName", string.IsNullOrWhiteSpace(request.RegionName) ? DBNull.Value : request.RegionName.Trim());
            command.Parameters.AddWithValue("@AreaCategory", string.IsNullOrWhiteSpace(request.AreaCategory) ? DBNull.Value : request.AreaCategory.Trim());
            command.Parameters.AddWithValue("@TownshipId", request.TownshipId.HasValue ? request.TownshipId.Value : DBNull.Value);
            command.Parameters.AddWithValue("@PhysicalAddress", request.Address.Trim());
            command.Parameters.AddWithValue("@PostalAddress", string.IsNullOrWhiteSpace(request.PostalAddress) ? DBNull.Value : request.PostalAddress.Trim());
            command.Parameters.AddWithValue("@Latitude", request.Latitude.HasValue ? request.Latitude.Value : DBNull.Value);
            command.Parameters.AddWithValue("@Longitude", request.Longitude.HasValue ? request.Longitude.Value : DBNull.Value);
            command.Parameters.AddWithValue("@Notes", string.IsNullOrWhiteSpace(request.Notes) ? DBNull.Value : request.Notes.Trim());

            await command.Connection!.OpenAsync();
            SqlDataReader reader;
            try
            {
                reader = await command.ExecuteReaderAsync();
            }
            catch (SqlException error) when (error.Number == 50000 && error.Message.Contains("New Application", StringComparison.OrdinalIgnoreCase))
            {
                throw new DuplicateApplicationException(error.Message);
            }

            ApplicationSubmitResponse response;
            await using (reader)
            {
                if (!await reader.ReadAsync())
                {
                    throw new InvalidOperationException("The application could not be saved.");
                }
                response = MapApplicationResponse(reader);
            }
            await SavePrePackedPerishableGoodsAsync(response.ApplicationId, request.PrePackedPerishableGoods);
            response.PrePackedPerishableGoods = request.PrePackedPerishableGoods;
            return response;
        }
        catch (DuplicateApplicationException)
        {
            throw;
        }
        catch (SqlException ex)
        {
            throw CreateDatabaseException("submit application", ex);
        }
    }

    private async Task SavePrePackedPerishableGoodsAsync(int applicationId, string? selections)
    {
        await using var command = datalayer.CreateTextCommand("UPDATE dbo.Applications SET PrePackedPerishableGoods = @Selections WHERE ApplicationId = @ApplicationId;");
        command.Parameters.AddWithValue("@ApplicationId", applicationId);
        command.Parameters.AddWithValue("@Selections", string.IsNullOrWhiteSpace(selections) ? DBNull.Value : selections.Trim());
        await command.Connection!.OpenAsync();
        await command.ExecuteNonQueryAsync();
    }

    public async Task<ApplicationSubmitResponse?> ResubmitRejectedApplicationAsync(int applicationId, ApplicationSubmitRequest request, string licenceType, int userId)
    {
        try
        {
            await using var command = datalayer.CreateStoredProcedureCommand("dbo.usp_Applications_ResubmitRejectedForCustomer");
            command.Parameters.AddWithValue("@AreaCategory", (object?)request.AreaCategory ?? DBNull.Value);
            command.Parameters.AddWithValue("@ApplicationId", applicationId);
            command.Parameters.AddWithValue("@UserId", userId);
            command.Parameters.AddWithValue("@ApplicantName", request.ApplicantName.Trim());
            command.Parameters.AddWithValue("@EmailAddress", request.Email.Trim());
            command.Parameters.AddWithValue("@MobileNumber", request.Phone.Trim());
            command.Parameters.AddWithValue("@PhysicalAddress", request.Address.Trim());
            command.Parameters.AddWithValue("@PostalAddress", string.IsNullOrWhiteSpace(request.PostalAddress) ? DBNull.Value : request.PostalAddress.Trim());
            command.Parameters.AddWithValue("@EventStartDate", (object?)request.EventStartDate?.Date ?? DBNull.Value);
            command.Parameters.AddWithValue("@EventEndDate", (object?)request.EventEndDate?.Date ?? DBNull.Value);
            command.Parameters.AddWithValue("@FoodVendingDetailsJson", request.FoodVendingDetails is null ? DBNull.Value : System.Text.Json.JsonSerializer.Serialize(request.FoodVendingDetails));
            command.Parameters.AddWithValue("@TradeStandBusinessType", string.IsNullOrWhiteSpace(request.TradeStandBusinessType) ? DBNull.Value : request.TradeStandBusinessType.Trim());
            command.Parameters.AddWithValue("@Latitude", request.Latitude.HasValue ? request.Latitude.Value : DBNull.Value);
            command.Parameters.AddWithValue("@Longitude", request.Longitude.HasValue ? request.Longitude.Value : DBNull.Value);
            command.Parameters.AddWithValue("@Notes", string.IsNullOrWhiteSpace(request.Notes) ? DBNull.Value : request.Notes.Trim());

            await command.Connection!.OpenAsync();
            await using var reader = await command.ExecuteReaderAsync();
            return await reader.ReadAsync() ? MapApplicationResponse(reader) : null;
        }
        catch (SqlException ex)
        {
            throw CreateDatabaseException("resubmit rejected application", ex);
        }
    }

    public async Task<InternalApplicationDetailResponse?> ProcessWorkflowStepAsync(int applicationId, WorkflowStepActionRequest request, UserTokenPayload user)
    {
        try
        {
            var documentBytes = ParseBase64File(request.FileBase64);
            var signatureBytes = ParseBase64File(request.SignatureBase64);
            await using var command = datalayer.CreateStoredProcedureCommand("dbo.usp_Applications_ProcessWorkflowStep");
            command.Parameters.AddWithValue("@ApplicationId", applicationId);
            command.Parameters.AddWithValue("@UserId", user.UserId);
            command.Parameters.AddWithValue("@DisplayName", string.IsNullOrWhiteSpace(user.DisplayName) ? user.Username : user.DisplayName);
            command.Parameters.AddWithValue("@Decision", request.Decision.Trim());
            command.Parameters.AddWithValue("@Comment", string.IsNullOrWhiteSpace(request.Comment) ? DBNull.Value : request.Comment.Trim());
            command.Parameters.AddWithValue("@RejectionReason", string.IsNullOrWhiteSpace(request.RejectionReason) ? DBNull.Value : request.RejectionReason.Trim());
            command.Parameters.AddWithValue("@AttachmentTypeId", request.AttachmentTypeId.HasValue ? request.AttachmentTypeId.Value : DBNull.Value);
            command.Parameters.AddWithValue("@DocumentName", string.IsNullOrWhiteSpace(request.DocumentName) ? DBNull.Value : request.DocumentName.Trim());
            command.Parameters.AddWithValue("@OriginalFileName", string.IsNullOrWhiteSpace(request.FileName) ? DBNull.Value : Path.GetFileName(request.FileName.Trim()));
            command.Parameters.AddWithValue("@ContentType", string.IsNullOrWhiteSpace(request.ContentType) ? DBNull.Value : request.ContentType.Trim());
            command.Parameters.AddWithValue("@FileSizeBytes", documentBytes is null ? DBNull.Value : documentBytes.LongLength);
            command.Parameters.Add("@FileContent", SqlDbType.VarBinary, -1).Value = documentBytes is null ? DBNull.Value : documentBytes;
            command.Parameters.AddWithValue("@FileSha256Hash", documentBytes is null ? DBNull.Value : Convert.ToHexString(SHA256.HashData(documentBytes)));
            command.Parameters.AddWithValue("@SignatureFileSizeBytes", signatureBytes is null ? DBNull.Value : signatureBytes.LongLength);
            command.Parameters.Add("@SignatureFileContent", SqlDbType.VarBinary, -1).Value = signatureBytes is null ? DBNull.Value : signatureBytes;
            command.Parameters.AddWithValue("@SignatureFileSha256Hash", signatureBytes is null ? DBNull.Value : Convert.ToHexString(SHA256.HashData(signatureBytes)));

            await command.Connection!.OpenAsync();
            await using var transaction = (SqlTransaction)await command.Connection.BeginTransactionAsync();
            command.Transaction = transaction;
            // Serialize workflow writes and reject stale identity/step snapshots after the external lookup.
            await using var guard = command.Connection.CreateCommand();
            guard.Transaction = transaction;
            guard.CommandText = """
                SELECT ApplicantName, IdOrPassportNumber
                FROM dbo.Applications WITH (UPDLOCK, HOLDLOCK)
                WHERE ApplicationId = @ApplicationId AND Archive_Date IS NULL
                  AND Status NOT IN ('Rejected', 'Cancelled', 'Completed', 'Archived');
                SELECT TOP (1) SequenceNumber, StepName
                FROM dbo.ApplicationWorkflowSteps WITH (UPDLOCK, HOLDLOCK)
                WHERE ApplicationId = @ApplicationId
                  AND Status NOT IN ('Approved', 'Completed', 'Complete')
                ORDER BY SequenceNumber;
                """;
            guard.Parameters.AddWithValue("@ApplicationId", applicationId);
            await using (var snapshot = await guard.ExecuteReaderAsync())
            {
                if (!await snapshot.ReadAsync()
                    || snapshot.GetString(0) != request.ExpectedApplicantName
                    || snapshot.GetString(1) != request.ExpectedIdentityNumber
                    || !await snapshot.NextResultAsync() || !await snapshot.ReadAsync()
                    || snapshot.GetInt32(0) != request.ExpectedStepSequence
                    || snapshot.GetString(1) != request.ExpectedStepName)
                    throw new ArgumentException("The application changed while processing. Reload it and try again.");
            }

            int updatedApplicationId;
            await using (var reader = await command.ExecuteReaderAsync())
            {
                if (!await reader.ReadAsync()) return null;
                updatedApplicationId = reader.GetInt32(reader.GetOrdinal("ApplicationId"));
            }
            await transaction.CommitAsync();
            return await GetInternalApplicationDetailAsync(updatedApplicationId);
        }
        catch (SqlException ex)
        {
            throw CreateDatabaseException("process workflow step", ex);
        }
    }

    public async Task<bool> BusinessExistsForApplicantAsync(string idNumber, string businessName)
    {
        try
        {
            await using var command = datalayer.CreateStoredProcedureCommand("dbo.usp_Applications_BusinessExistsForApplicant");
            command.Parameters.AddWithValue("@IdOrPassportNumber", idNumber);
            command.Parameters.AddWithValue("@BusinessName", businessName);

            await command.Connection!.OpenAsync();
            return Convert.ToBoolean(await command.ExecuteScalarAsync());
        }
        catch (SqlException ex)
        {
            throw CreateDatabaseException("check business application", ex);
        }
    }

    public async Task<bool> BusinessHasApplicationAsync(int userId, int businessId)
    {
        try
        {
            await using var command = datalayer.CreateStoredProcedureCommand("dbo.usp_Applications_BusinessHasApplication");
            command.Parameters.AddWithValue("@UserId", userId);
            command.Parameters.AddWithValue("@BusinessId", businessId);

            await command.Connection!.OpenAsync();
            return Convert.ToBoolean(await command.ExecuteScalarAsync());
        }
        catch (SqlException ex)
        {
            throw CreateDatabaseException("check business application", ex);
        }
    }

    public async Task<TariffResponse?> GetTariffAsync(string licenceType, string applicationKind)
    {
        try
        {
            await using var command = datalayer.CreateStoredProcedureCommand("dbo.usp_Tariffs_GetActive");
            command.Parameters.AddWithValue("@LicenceType", licenceType.Trim());
            command.Parameters.AddWithValue("@ApplicationKind", applicationKind.Trim());

            await command.Connection!.OpenAsync();
            await using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                return null;
            }

            return MapTariffResponse(reader);
        }
        catch (SqlException ex)
        {
            throw CreateDatabaseException("load tariff", ex);
        }
    }

    public async Task<PermitRentalFeeResponse?> GetPermitRentalFeeAsync(string businessType, string? tradingLocation)
    {
        try
        {
            await using var command = datalayer.CreateStoredProcedureCommand("dbo.usp_PermitRentalFees_Get");
            command.Parameters.AddWithValue("@BusinessType", businessType.Trim());
            command.Parameters.AddWithValue("@TradingLocation", string.IsNullOrWhiteSpace(tradingLocation) ? DBNull.Value : tradingLocation.Trim());
            await command.Connection!.OpenAsync();
            await using var reader = await command.ExecuteReaderAsync();
            return await reader.ReadAsync()
                ? new PermitRentalFeeResponse
                {
                    PermitRentalFeeId = reader.GetInt32(reader.GetOrdinal("PermitRentalFeeId")),
                    BusinessType = reader.GetString(reader.GetOrdinal("BusinessType")),
                    TradingLocation = reader.IsDBNull(reader.GetOrdinal("TradingLocation")) ? null : reader.GetString(reader.GetOrdinal("TradingLocation")),
                    MonthlyFee = reader.GetDecimal(reader.GetOrdinal("MonthlyFee")),
                    IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive")),
                }
                : null;
        }
        catch (SqlException ex)
        {
            throw CreateDatabaseException("load permit rental fee", ex);
        }
    }

    public async Task<List<PermitRentalFeeResponse>> GetPermitRentalFeesAsync()
    {
        var results = new List<PermitRentalFeeResponse>();
        await using var command = datalayer.CreateTextCommand("SELECT PermitRentalFeeId, BusinessType, TradingLocation, MonthlyFee, IsActive FROM dbo.PermitRentalFees ORDER BY BusinessType, TradingLocation;");
        await command.Connection!.OpenAsync();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) results.Add(MapPermitRentalFee(reader));
        return results;
    }

    public async Task<PermitRentalFeeResponse> SavePermitRentalFeeAsync(PermitRentalFeeSaveRequest request)
    {
        await using var command = datalayer.CreateTextCommand("""
            MERGE dbo.PermitRentalFees AS target USING (SELECT @BusinessType AS BusinessType, @TradingLocation AS TradingLocation) AS source
            ON target.BusinessType = source.BusinessType AND ISNULL(target.TradingLocation, '') = ISNULL(source.TradingLocation, '')
            WHEN MATCHED THEN UPDATE SET MonthlyFee = @MonthlyFee, IsActive = @IsActive, ModifiedDate = SYSUTCDATETIME()
            WHEN NOT MATCHED THEN INSERT (BusinessType, TradingLocation, MonthlyFee, IsActive) VALUES (@BusinessType, @TradingLocation, @MonthlyFee, @IsActive)
            OUTPUT inserted.PermitRentalFeeId, inserted.BusinessType, inserted.TradingLocation, inserted.MonthlyFee, inserted.IsActive;
            """);
        AddPermitRentalFeeParameters(command, request);
        await command.Connection!.OpenAsync();
        await using var reader = await command.ExecuteReaderAsync();
        await reader.ReadAsync();
        return MapPermitRentalFee(reader);
    }

    public async Task<PermitRentalFeeResponse?> UpdatePermitRentalFeeAsync(int permitRentalFeeId, PermitRentalFeeSaveRequest request)
    {
        await using var command = datalayer.CreateTextCommand("""
            UPDATE dbo.PermitRentalFees SET BusinessType = @BusinessType, TradingLocation = @TradingLocation, MonthlyFee = @MonthlyFee, IsActive = @IsActive, ModifiedDate = SYSUTCDATETIME()
            OUTPUT inserted.PermitRentalFeeId, inserted.BusinessType, inserted.TradingLocation, inserted.MonthlyFee, inserted.IsActive
            WHERE PermitRentalFeeId = @PermitRentalFeeId;
            """);
        AddPermitRentalFeeParameters(command, request);
        command.Parameters.AddWithValue("@PermitRentalFeeId", permitRentalFeeId);
        await command.Connection!.OpenAsync(); await using var reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync() ? MapPermitRentalFee(reader) : null;
    }

    public async Task<bool> DisablePermitRentalFeeAsync(int permitRentalFeeId) => await SetPermitRentalFeeActiveAsync(permitRentalFeeId, false);
    public async Task<bool> DeletePermitRentalFeeAsync(int permitRentalFeeId)
    {
        await using var command = datalayer.CreateTextCommand("DELETE FROM dbo.PermitRentalFees WHERE PermitRentalFeeId = @PermitRentalFeeId;");
        command.Parameters.AddWithValue("@PermitRentalFeeId", permitRentalFeeId); await command.Connection!.OpenAsync(); return await command.ExecuteNonQueryAsync() > 0;
    }

    private async Task<bool> SetPermitRentalFeeActiveAsync(int permitRentalFeeId, bool isActive)
    {
        await using var command = datalayer.CreateTextCommand("UPDATE dbo.PermitRentalFees SET IsActive = @IsActive, ModifiedDate = SYSUTCDATETIME() WHERE PermitRentalFeeId = @PermitRentalFeeId;");
        command.Parameters.AddWithValue("@PermitRentalFeeId", permitRentalFeeId); command.Parameters.AddWithValue("@IsActive", isActive); await command.Connection!.OpenAsync(); return await command.ExecuteNonQueryAsync() > 0;
    }

    private static void AddPermitRentalFeeParameters(SqlCommand command, PermitRentalFeeSaveRequest request)
    {
        command.Parameters.AddWithValue("@BusinessType", request.BusinessType.Trim());
        command.Parameters.AddWithValue("@TradingLocation", string.IsNullOrWhiteSpace(request.TradingLocation) ? DBNull.Value : request.TradingLocation.Trim());
        command.Parameters.AddWithValue("@MonthlyFee", request.MonthlyFee); command.Parameters.AddWithValue("@IsActive", request.IsActive);
    }

    private static PermitRentalFeeResponse MapPermitRentalFee(SqlDataReader reader) => new()
    {
        PermitRentalFeeId = reader.GetInt32(reader.GetOrdinal("PermitRentalFeeId")), BusinessType = reader.GetString(reader.GetOrdinal("BusinessType")),
        TradingLocation = reader.IsDBNull(reader.GetOrdinal("TradingLocation")) ? null : reader.GetString(reader.GetOrdinal("TradingLocation")),
        MonthlyFee = reader.GetDecimal(reader.GetOrdinal("MonthlyFee")), IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive")),
    };

    public async Task<List<TariffResponse>> GetTariffsAsync()
    {
        try
        {
            var results = new List<TariffResponse>();
            await using var command = datalayer.CreateStoredProcedureCommand("dbo.usp_Tariffs_GetAll");

            await command.Connection!.OpenAsync();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(MapTariffResponse(reader));
            }

            return results;
        }
        catch (SqlException ex)
        {
            throw CreateDatabaseException("load tariffs", ex);
        }
    }

    public async Task<TariffResponse> SaveTariffAsync(TariffSaveRequest request)
    {
        try
        {
            await using var command = datalayer.CreateStoredProcedureCommand("dbo.usp_Tariffs_Save");
            command.Parameters.AddWithValue("@LicenceType", request.LicenceType);
            command.Parameters.AddWithValue("@ApplicationKind", request.ApplicationKind);
            command.Parameters.AddWithValue("@FeeAmount", request.FeeAmount);
            command.Parameters.AddWithValue("@IsActive", request.IsActive);

            await command.Connection!.OpenAsync();
            await using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                throw new InvalidOperationException("The application price could not be saved.");
            }

            return MapTariffResponse(reader);
        }
        catch (SqlException ex)
        {
            throw CreateDatabaseException("save tariff", ex);
        }
    }

    public async Task<TariffResponse?> UpdateTariffAsync(int tariffId, TariffSaveRequest request)
    {
        try
        {
            await using var command = datalayer.CreateStoredProcedureCommand("dbo.usp_Tariffs_Update");
            command.Parameters.AddWithValue("@TariffId", tariffId);
            command.Parameters.AddWithValue("@LicenceType", request.LicenceType);
            command.Parameters.AddWithValue("@ApplicationKind", request.ApplicationKind);
            command.Parameters.AddWithValue("@FeeAmount", request.FeeAmount);
            command.Parameters.AddWithValue("@IsActive", request.IsActive);

            await command.Connection!.OpenAsync();
            await using var reader = await command.ExecuteReaderAsync();
            return await reader.ReadAsync() ? MapTariffResponse(reader) : null;
        }
        catch (SqlException ex)
        {
            throw CreateDatabaseException("update tariff", ex);
        }
    }

    public async Task<bool> DisableTariffAsync(int tariffId)
    {
        try
        {
            await using var command = datalayer.CreateStoredProcedureCommand("dbo.usp_Tariffs_Disable");
            command.Parameters.AddWithValue("@TariffId", tariffId);

            await command.Connection!.OpenAsync();
            return Convert.ToBoolean(await command.ExecuteScalarAsync());
        }
        catch (SqlException ex)
        {
            throw CreateDatabaseException("disable tariff", ex);
        }
    }

    public async Task<bool> DeleteTariffAsync(int tariffId)
    {
        try
        {
            await using var command = datalayer.CreateStoredProcedureCommand("dbo.usp_Tariffs_Delete");
            command.Parameters.AddWithValue("@TariffId", tariffId);

            await command.Connection!.OpenAsync();
            return Convert.ToBoolean(await command.ExecuteScalarAsync());
        }
        catch (SqlException ex)
        {
            throw CreateDatabaseException("delete tariff", ex);
        }
    }

    public async Task<bool> NewApplicationExistsForApplicantAsync(string idNumber)
    {
        try
        {
            await using var command = datalayer.CreateStoredProcedureCommand("dbo.usp_Applications_NewApplicationExistsForApplicant");
            command.Parameters.AddWithValue("@IdOrPassportNumber", idNumber.Trim());

            await command.Connection!.OpenAsync();
            return Convert.ToBoolean(await command.ExecuteScalarAsync());
        }
        catch (SqlException ex)
        {
            throw CreateDatabaseException("check new application", ex);
        }
    }

    private static InvalidOperationException CreateDatabaseException(string operation, SqlException innerException)
    {
        return new InvalidOperationException($"The database operation failed while trying to {operation}.", innerException);
    }

    private static TariffResponse MapTariffResponse(SqlDataReader reader)
    {
        return new TariffResponse
        {
            TariffId = reader.GetInt32(reader.GetOrdinal("TariffId")),
            LicenceType = reader.GetString(reader.GetOrdinal("LicenceType")),
            ApplicationKind = reader.GetString(reader.GetOrdinal("ApplicationKind")),
            FeeAmount = reader.GetDecimal(reader.GetOrdinal("FeeAmount")),
            IsActive = !HasColumn(reader, "IsActive") || reader.GetBoolean(reader.GetOrdinal("IsActive"))
        };
    }

    private static ApplicationSubmitResponse MapApplicationResponse(SqlDataReader reader)
    {
        return new ApplicationSubmitResponse
        {
            ApplicationId = reader.GetInt32(reader.GetOrdinal("ApplicationId")),
            TrackingNumber = reader.GetString(reader.GetOrdinal("TrackingNumber")),
            ApplicantName = reader.GetString(reader.GetOrdinal("ApplicantName")),
            IdNumber = reader.GetString(reader.GetOrdinal("IdOrPassportNumber")),
            BusinessName = reader.GetString(reader.GetOrdinal("BusinessName")),
            LicenceType = reader.GetString(reader.GetOrdinal("LicenceType")),
            EventStartDate = HasColumn(reader, "EventStartDate") && !reader.IsDBNull(reader.GetOrdinal("EventStartDate")) ? reader.GetDateTime(reader.GetOrdinal("EventStartDate")) : null,
            EventEndDate = HasColumn(reader, "EventEndDate") && !reader.IsDBNull(reader.GetOrdinal("EventEndDate")) ? reader.GetDateTime(reader.GetOrdinal("EventEndDate")) : null,
            FoodVendingDetails = HasColumn(reader, "FoodVendingDetailsJson") && !reader.IsDBNull(reader.GetOrdinal("FoodVendingDetailsJson")) ? System.Text.Json.JsonSerializer.Deserialize<FoodVendingDetails>(reader.GetString(reader.GetOrdinal("FoodVendingDetailsJson"))) : null,
            TradeStandBusinessType = HasColumn(reader, "TradeStandBusinessType") && !reader.IsDBNull(reader.GetOrdinal("TradeStandBusinessType")) ? reader.GetString(reader.GetOrdinal("TradeStandBusinessType")) : null,
            CurrentStage = reader.GetString(reader.GetOrdinal("CurrentStage")),
            Status = reader.GetString(reader.GetOrdinal("Status")),
            SubmittedDate = reader.GetDateTime(reader.GetOrdinal("SubmittedDate")),
            RegionName = HasColumn(reader, "RegionName") && !reader.IsDBNull(reader.GetOrdinal("RegionName")) ? reader.GetString(reader.GetOrdinal("RegionName")) : null,
            AreaOrSuburb = HasColumn(reader, "AreaOrSuburb") && !reader.IsDBNull(reader.GetOrdinal("AreaOrSuburb")) ? reader.GetString(reader.GetOrdinal("AreaOrSuburb")) : null,
            AreaCategory = HasColumn(reader, "AreaCategory") && !reader.IsDBNull(reader.GetOrdinal("AreaCategory")) ? reader.GetString(reader.GetOrdinal("AreaCategory")) : null
        };
    }

    private static void AddCustomerBusinessParameters(SqlCommand command, int userId, CustomerBusinessSaveRequest request)
    {
        var documentBytes = ParseBase64File(request.CipcDocumentBase64);
        command.Parameters.AddWithValue("@UserId", userId);
        command.Parameters.AddWithValue("@BusinessName", request.BusinessName.Trim());
        command.Parameters.AddWithValue("@RegistrationNumber", string.IsNullOrWhiteSpace(request.RegistrationNumber) ? DBNull.Value : request.RegistrationNumber.Trim());
        command.Parameters.AddWithValue("@TownshipId", request.TownshipId.HasValue ? request.TownshipId.Value : DBNull.Value);
        command.Parameters.AddWithValue("@WardNumber", string.IsNullOrWhiteSpace(request.WardNumber) ? DBNull.Value : request.WardNumber.Trim());
        command.Parameters.AddWithValue("@PhysicalAddress", request.PhysicalAddress.Trim());
        command.Parameters.AddWithValue("@Notes", string.IsNullOrWhiteSpace(request.Notes) ? DBNull.Value : request.Notes.Trim());
        var businessPhotoBytes = ParseBase64File(request.BusinessPhotoDataUrl);
        var businessPhotoContentType = GetDataUrlContentType(request.BusinessPhotoDataUrl);
        command.Parameters.AddWithValue("@BusinessPhotoContentType", string.IsNullOrWhiteSpace(businessPhotoContentType) ? DBNull.Value : businessPhotoContentType);
        command.Parameters.Add("@BusinessPhotoContent", SqlDbType.VarBinary, -1).Value = businessPhotoBytes is null ? DBNull.Value : businessPhotoBytes;
        command.Parameters.AddWithValue("@CipcDocumentFileName", string.IsNullOrWhiteSpace(request.CipcDocumentFileName) ? DBNull.Value : Path.GetFileName(request.CipcDocumentFileName.Trim()));
        command.Parameters.AddWithValue("@CipcDocumentContentType", string.IsNullOrWhiteSpace(request.CipcDocumentContentType) ? DBNull.Value : request.CipcDocumentContentType.Trim());
        command.Parameters.AddWithValue("@CipcDocumentFileSizeBytes", documentBytes is null ? DBNull.Value : documentBytes.LongLength);
        command.Parameters.Add("@CipcDocumentFileContent", SqlDbType.VarBinary, -1).Value = documentBytes is null ? DBNull.Value : documentBytes;
        command.Parameters.AddWithValue("@CipcDocumentFileSha256Hash", documentBytes is null ? DBNull.Value : Convert.ToHexString(SHA256.HashData(documentBytes)));
    }

    private static byte[]? ParseBase64File(string? base64Value)
    {
        if (string.IsNullOrWhiteSpace(base64Value))
        {
            return null;
        }

        var value = base64Value.Trim();
        var commaIndex = value.IndexOf(',');
        if (commaIndex >= 0)
        {
            value = value[(commaIndex + 1)..];
        }

        return Convert.FromBase64String(value);
    }

    private static string? GetDataUrlContentType(string? dataUrl)
    {
        if (string.IsNullOrWhiteSpace(dataUrl) || !dataUrl.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var separator = dataUrl.IndexOf(';');
        return separator > 5 ? dataUrl[5..separator] : null;
    }

    private static CustomerBusinessResponse MapCustomerBusiness(SqlDataReader reader)
    {
        return new CustomerBusinessResponse
        {
            BusinessId = reader.GetInt32(reader.GetOrdinal("BusinessId")),
            UserId = reader.GetInt32(reader.GetOrdinal("UserId")),
            BusinessName = reader.GetString(reader.GetOrdinal("BusinessName")),
            RegistrationNumber = reader.IsDBNull(reader.GetOrdinal("RegistrationNumber")) ? null : reader.GetString(reader.GetOrdinal("RegistrationNumber")),
            TownshipId = reader.IsDBNull(reader.GetOrdinal("TownshipId")) ? null : reader.GetInt32(reader.GetOrdinal("TownshipId")),
            TownshipName = reader.IsDBNull(reader.GetOrdinal("TownshipName")) ? null : reader.GetString(reader.GetOrdinal("TownshipName")),
            WardNumber = reader.IsDBNull(reader.GetOrdinal("WardNumber")) ? null : reader.GetString(reader.GetOrdinal("WardNumber")),
            PhysicalAddress = reader.GetString(reader.GetOrdinal("PhysicalAddress")),
            Notes = reader.IsDBNull(reader.GetOrdinal("Notes")) ? null : reader.GetString(reader.GetOrdinal("Notes")),
            BusinessPhotoDataUrl = BuildBusinessPhotoDataUrl(reader),
            CipcDocumentFileName = HasColumn(reader, "CipcDocumentFileName") && !reader.IsDBNull(reader.GetOrdinal("CipcDocumentFileName")) ? reader.GetString(reader.GetOrdinal("CipcDocumentFileName")) : null,
            HasCipcDocument = HasColumn(reader, "HasCipcDocument") && reader.GetBoolean(reader.GetOrdinal("HasCipcDocument")),
            WorkshopAttended = HasColumn(reader, "WorkshopAttended") && reader.GetBoolean(reader.GetOrdinal("WorkshopAttended")),
            WorkshopAttendedDate = HasColumn(reader, "WorkshopAttendedDate") && !reader.IsDBNull(reader.GetOrdinal("WorkshopAttendedDate")) ? reader.GetDateTime(reader.GetOrdinal("WorkshopAttendedDate")) : null,
            WorkshopRequestStatus = HasColumn(reader, "WorkshopRequestStatus") && !reader.IsDBNull(reader.GetOrdinal("WorkshopRequestStatus")) ? reader.GetString(reader.GetOrdinal("WorkshopRequestStatus")) : null,
            WorkshopRequestedDate = HasColumn(reader, "WorkshopRequestedDate") && !reader.IsDBNull(reader.GetOrdinal("WorkshopRequestedDate")) ? reader.GetDateTime(reader.GetOrdinal("WorkshopRequestedDate")) : null,
            WorkshopScheduledDate = HasColumn(reader, "WorkshopScheduledDate") && !reader.IsDBNull(reader.GetOrdinal("WorkshopScheduledDate")) ? reader.GetDateTime(reader.GetOrdinal("WorkshopScheduledDate")) : null,
            IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive")),
            CreatedDate = reader.GetDateTime(reader.GetOrdinal("CreatedDate"))
        };
    }

    private static string? BuildBusinessPhotoDataUrl(SqlDataReader reader)
    {
        if (!HasColumn(reader, "BusinessPhotoContent") || reader.IsDBNull(reader.GetOrdinal("BusinessPhotoContent")))
        {
            return null;
        }

        var photoBytes = reader.GetFieldValue<byte[]>(reader.GetOrdinal("BusinessPhotoContent"));
        var contentType = HasColumn(reader, "BusinessPhotoContentType") && !reader.IsDBNull(reader.GetOrdinal("BusinessPhotoContentType"))
            ? reader.GetString(reader.GetOrdinal("BusinessPhotoContentType"))
            : "image/jpeg";
        return $"data:{contentType};base64,{Convert.ToBase64String(photoBytes)}";
    }

    private static bool HasColumn(SqlDataReader reader, string columnName)
    {
        for (var index = 0; index < reader.FieldCount; index += 1)
        {
            if (string.Equals(reader.GetName(index), columnName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
