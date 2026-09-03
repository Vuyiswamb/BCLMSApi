using BCLMSApi.Models;

namespace BCLMSApi.Data;

public interface IComplaintRepository
{
    Task<ComplaintResponse> CreateComplaintAsync(int userId, ComplaintCreateRequest request);

    Task<List<ComplaintResponse>> GetCustomerComplaintsAsync(int userId);

    Task<List<ComplaintResponse>> GetInternalComplaintsAsync();

    Task<ComplaintResponse?> UpdateComplaintStatusAsync(int complaintId, int updatedByUserId, ComplaintStatusUpdateRequest request);

    Task<ComplaintResponse?> CancelComplaintAsync(int complaintId, int userId);
}
