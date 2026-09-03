using BCLMSApi.Models;

namespace BCLMSApi.Services;

public interface IComplaintService
{
    Task<ComplaintResponse> CreateComplaintAsync(UserTokenPayload user, ComplaintCreateRequest request);

    Task<List<ComplaintResponse>> GetCustomerComplaintsAsync(UserTokenPayload user);

    Task<List<ComplaintResponse>> GetInternalComplaintsAsync(UserTokenPayload user);

    Task<ComplaintResponse?> UpdateComplaintStatusAsync(UserTokenPayload user, int complaintId, ComplaintStatusUpdateRequest request);

    Task<ComplaintResponse?> CancelComplaintAsync(UserTokenPayload user, int complaintId);
}
