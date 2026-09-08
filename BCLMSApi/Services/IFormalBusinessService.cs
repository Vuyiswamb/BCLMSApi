using BCLMSApi.Models;

namespace BCLMSApi.Services;

public interface IFormalBusinessService
{
    FormalBusinessApplicationTemplate GetNewApplicationTemplate();

    Task<List<AttachmentTypeResponse>> GetAttachmentTypesAsync();

    Task<List<TownshipResponse>> GetTownshipsAsync();

    Task<List<CustomerBusinessResponse>> GetCustomerBusinessesAsync(UserTokenPayload user);

    Task<CustomerBusinessResponse> CreateCustomerBusinessAsync(UserTokenPayload user, CustomerBusinessSaveRequest request);

    Task<CustomerBusinessResponse?> UpdateCustomerBusinessAsync(UserTokenPayload user, int businessId, CustomerBusinessSaveRequest request);

    Task<CustomerBusinessResponse?> GetCustomerBusinessAsync(UserTokenPayload user, int businessId);

    Task<bool> CustomerBusinessHasApplicationAsync(UserTokenPayload user, int businessId);

    Task<bool> DeactivateCustomerBusinessAsync(UserTokenPayload user, int businessId);

    Task<TrackingApplicationResponse?> TrackApplicationAsync(string trackingNumber);

    Task<List<InternalApplicationSummaryResponse>> GetMyApplicationsAsync(UserTokenPayload user);

    Task<List<InternalApplicationSummaryResponse>> GetInternalApplicationsAsync(UserTokenPayload user);

    Task<InternalApplicationDetailResponse?> GetInternalApplicationDetailAsync(int applicationId, UserTokenPayload user);

    Task<ApplicationDocumentFileResponse?> GetApplicationDocumentFileAsync(int applicationId, int applicationDocumentId, UserTokenPayload user);

    Task<bool> ArchiveApplicationAsync(int applicationId, UserTokenPayload user);

    Task<bool> CancelCustomerApplicationAsync(int applicationId, UserTokenPayload user);

    Task<AttachmentUploadResponse> UploadAttachmentAsync(int applicationId, AttachmentUploadRequest request, UserTokenPayload user);

    FormalBusinessDraftResponse SaveDraft(FormalBusinessDraftRequest request);

    Task<ApplicationSubmitResponse> SubmitApplicationAsync(ApplicationSubmitRequest request, int userId);

    Task<ApplicationSubmitResponse?> ResubmitRejectedApplicationAsync(int applicationId, ApplicationSubmitRequest request, int userId);

    Task<InternalApplicationDetailResponse?> ProcessWorkflowStepAsync(int applicationId, WorkflowStepActionRequest request, UserTokenPayload user);

    Task<TariffResponse?> GetTariffAsync(string licenceType, string applicationKind);

    Task<PermitRentalFeeResponse?> GetPermitRentalFeeAsync(string businessType, string? tradingLocation);
    Task<List<PermitRentalFeeResponse>> GetPermitRentalFeesAsync(UserTokenPayload user);
    Task<PermitRentalFeeResponse> SavePermitRentalFeeAsync(UserTokenPayload user, PermitRentalFeeSaveRequest request);
    Task<PermitRentalFeeResponse?> UpdatePermitRentalFeeAsync(UserTokenPayload user, int permitRentalFeeId, PermitRentalFeeSaveRequest request);
    Task<bool> DisablePermitRentalFeeAsync(UserTokenPayload user, int permitRentalFeeId);
    Task<bool> DeletePermitRentalFeeAsync(UserTokenPayload user, int permitRentalFeeId);

    Task<List<TariffResponse>> GetTariffsAsync(UserTokenPayload user);

    Task<TariffResponse> SaveTariffAsync(UserTokenPayload user, TariffSaveRequest request);

    Task<TariffResponse?> UpdateTariffAsync(UserTokenPayload user, int tariffId, TariffSaveRequest request);

    Task<bool> DisableTariffAsync(UserTokenPayload user, int tariffId);

    Task<bool> DeleteTariffAsync(UserTokenPayload user, int tariffId);
}
