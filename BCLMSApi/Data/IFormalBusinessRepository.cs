using BCLMSApi.Models;

namespace BCLMSApi.Data;

public interface IFormalBusinessRepository
{
    Task<List<AttachmentTypeResponse>> GetAttachmentTypesAsync();

    Task<List<TownshipResponse>> GetTownshipsAsync();

    Task<List<CustomerBusinessResponse>> GetCustomerBusinessesAsync(int userId);

    Task<CustomerBusinessResponse> CreateCustomerBusinessAsync(int userId, CustomerBusinessSaveRequest request);

    Task<CustomerBusinessResponse?> UpdateCustomerBusinessAsync(int userId, int businessId, CustomerBusinessSaveRequest request);

    Task<CustomerBusinessResponse?> GetCustomerBusinessAsync(int userId, int businessId);

    Task<bool> DeactivateCustomerBusinessAsync(int userId, int businessId);

    Task<TrackingApplicationResponse?> TrackApplicationAsync(string trackingNumber);

    Task<List<InternalApplicationSummaryResponse>> GetInternalApplicationsAsync(int? customerUserId = null, bool hasAllRegions = true, IReadOnlyCollection<string>? regionNames = null);

    Task<InternalApplicationDetailResponse?> GetInternalApplicationDetailAsync(int applicationId, int? customerUserId = null, bool hasAllRegions = true, IReadOnlyCollection<string>? regionNames = null);

    Task<ApplicationDocumentFileResponse?> GetApplicationDocumentFileAsync(int applicationId, int applicationDocumentId, int? customerUserId = null);

    Task<bool> ArchiveApplicationAsync(int applicationId, int archiveUserId);

    Task<bool> CancelCustomerApplicationAsync(int applicationId, int userId);

    Task<bool> ApplicationExistsAsync(int applicationId, int? customerUserId = null);

    Task<AttachmentTypeRecord?> GetAttachmentTypeAsync(int attachmentTypeId);

    Task<int> InsertPdfDocumentAsync(int applicationId, AttachmentTypeRecord attachmentType, string originalFileName, string storedFileName, long fileSizeBytes, byte[] fileContent, string fileHash, string? remarks);

    Task<ApplicationSubmitResponse> SubmitApplicationAsync(ApplicationSubmitRequest request, string licenceType, int userId);

    Task<ApplicationSubmitResponse?> ResubmitRejectedApplicationAsync(int applicationId, ApplicationSubmitRequest request, string licenceType, int userId);

    Task<InternalApplicationDetailResponse?> ProcessWorkflowStepAsync(int applicationId, WorkflowStepActionRequest request, UserTokenPayload user);

    Task<bool> BusinessExistsForApplicantAsync(string idNumber, string businessName);

    Task<bool> BusinessHasApplicationAsync(int userId, int businessId);

    Task<TariffResponse?> GetTariffAsync(string licenceType, string applicationKind);

    Task<List<TariffResponse>> GetTariffsAsync();

    Task<TariffResponse> SaveTariffAsync(TariffSaveRequest request);

    Task<TariffResponse?> UpdateTariffAsync(int tariffId, TariffSaveRequest request);

    Task<bool> DisableTariffAsync(int tariffId);

    Task<bool> DeleteTariffAsync(int tariffId);

    Task<bool> NewApplicationExistsForApplicantAsync(string idNumber);
}
