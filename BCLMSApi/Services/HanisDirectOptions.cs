namespace BCLMSApi.Services;

public sealed class HanisDirectOptions
{
    public const string PrimaryEndpoint = "https://hanisrvs2.hanis.gov.za/PersonData_4/HANISNPRRequest.asmx";
    public string Endpoint { get; set; } = PrimaryEndpoint;
    public string SiteId { get; set; } = "";
    public string WorkstationId { get; set; } = "";

    public static HanisDirectOptions Read(IConfiguration configuration, bool isDevelopment = false)
    {
        var options = configuration.GetSection("Hanis:Direct").Get<HanisDirectOptions>() ?? new();
        if (!Uri.TryCreate(options.Endpoint, UriKind.Absolute, out var uri) || uri.Scheme != "https"
            || uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0
            || !ValidId(options.SiteId, isDevelopment) || !ValidId(options.WorkstationId, isDevelopment))
            throw new ArgumentException("Direct Home Affairs access is not configured. Configure the HTTPS endpoint and DHA-issued Site ID and Workstation ID.");
        return options;
    }

    // Development may send missing registration values so DHA returns its own access error.
    // Never fabricate institution IDs or treat that response as a successful verification.
    private static bool ValidId(string value, bool isDevelopment) =>
        (isDevelopment && string.IsNullOrWhiteSpace(value))
        || (!string.IsNullOrWhiteSpace(value) && value.Length <= 20
            && value == value.Trim() && value == value.ToUpperInvariant() && !value.Any(char.IsControl));
}
