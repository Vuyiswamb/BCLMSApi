namespace BCLMSApi.Models;

public class StallResponse
{
    public int StallId { get; set; }
    public string StallNumber { get; set; } = string.Empty;
    public string StallName { get; set; } = string.Empty;
    public string StallType { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string? AreaName { get; set; }
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public bool IsRestrictedArea { get; set; }
    public string? RestrictedStreetName { get; set; }
    public string? RestrictedAreaCode { get; set; }
    public bool IsOccupied { get; set; }
    public string? AllocatedTrackingNumber { get; set; }
    public string? AllocatedBusinessName { get; set; }
}

public class StallSaveRequest
{
    public string? StallName { get; set; }
    public string StallType { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string? AreaName { get; set; }
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
}

public class StallAllocationRequest
{
    public int ApplicationId { get; set; }
    public int StallId { get; set; }
}

public class StallAllocationResponse
{
    public int ApplicationStallAllocationId { get; set; }
    public int ApplicationId { get; set; }
    public int StallId { get; set; }
    public string StallNumber { get; set; } = string.Empty;
    public string StallName { get; set; } = string.Empty;
    public bool IsRestrictedArea { get; set; }
    public string InspectionGroupName { get; set; } = string.Empty;
}

public class StallApplicationOptionResponse
{
    public int ApplicationId { get; set; }
    public string TrackingNumber { get; set; } = string.Empty;
    public string BusinessName { get; set; } = string.Empty;
    public string ApplicantName { get; set; } = string.Empty;
    public string LicenceType { get; set; } = string.Empty;
    public string CurrentStage { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public class RestrictedTradingAreaResponse
{
    public int RestrictedAreaId { get; set; }
    public string StreetName { get; set; } = string.Empty;
    public string AreaCode { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string? DisplayName { get; set; }
    public decimal? CenterLatitude { get; set; }
    public decimal? CenterLongitude { get; set; }
    public int? MapZoom { get; set; }
    public string? GeometryType { get; set; }
    public string? GeometryJson { get; set; }
    public bool IsActive { get; set; }
}

public class RestrictedTradingAreaSaveRequest
{
    public string StreetName { get; set; } = string.Empty;
    public string AreaCode { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string? DisplayName { get; set; }
    public decimal? CenterLatitude { get; set; }
    public decimal? CenterLongitude { get; set; }
    public int? MapZoom { get; set; }
    public string? GeometryType { get; set; }
    public string? GeometryJson { get; set; }
}
