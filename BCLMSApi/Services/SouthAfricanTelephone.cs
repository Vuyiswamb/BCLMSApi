using System.Text.RegularExpressions;

namespace BCLMSApi.Services;

public static class SouthAfricanTelephone
{
    public static string Normalize(string? value)
    {
        var number = Regex.Replace(value?.Trim() ?? "", "[ ()-]", "");
        if (number.StartsWith("+27", StringComparison.Ordinal)) number = "0" + number[3..];
        if (!Regex.IsMatch(number, @"\A0[1-8][0-9]{8}\z"))
            throw new ArgumentException("A valid South African telephone number is required (e.g. 0123456789 or +27123456789).");
        return number;
    }
}
