namespace BCLMSApi.Models;

public class GroupResponse
{
    public int GroupId { get; set; }

    public string GroupName { get; set; } = string.Empty;

    public string? Description { get; set; }
}

public class ManagedUserResponse
{
    public int UserId { get; set; }

    public string Username { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string? EmailAddress { get; set; }

    public bool IsActive { get; set; }

    public List<string> Groups { get; set; } = [];

    public bool HasAllRegions { get; set; }

    public List<string> Regions { get; set; } = [];
}

public class PasswordResetByAdminResponse
{
    public string Message { get; set; } = string.Empty;
}

public class SetUserPasswordByAdminRequest
{
    public string NewPassword { get; set; } = string.Empty;
}

public class CreateOfficialUserRequest
{
    public string DisplayName { get; set; } = string.Empty;

    public string EmailAddress { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public List<int> GroupIds { get; set; } = [];

    public bool HasAllRegions { get; set; } = true;

    public List<string> Regions { get; set; } = [];
}

public class UpdateOfficialUserRequest
{
    public string DisplayName { get; set; } = string.Empty;

    public string EmailAddress { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public List<int> GroupIds { get; set; } = [];

    public bool HasAllRegions { get; set; } = true;

    public List<string> Regions { get; set; } = [];
}

public class RegisterCustomerRequest
{
    public string FullName { get; set; } = string.Empty;

    public string IdNumber { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;
}
