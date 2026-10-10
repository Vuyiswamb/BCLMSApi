using System.Text.RegularExpressions;

namespace BCLMSApi.Services;

// Retain the existing helper name for callers; contact numbers are international.
public static class SouthAfricanTelephone
{
    public static string Normalize(string? value)
    {
        var number = Regex.Replace(value ?? "", @"[\s()\-]", "");
        if (number.StartsWith("00")) number = "+" + number[2..];
        if (Regex.IsMatch(number, @"^\+[1-9][0-9]{7,14}$")) return number;
        // Existing national-format contacts remain editable.
        if (Regex.IsMatch(number, @"^0[1-8][0-9]{8}$")) return number;
        throw new ArgumentException("Enter a valid telephone number with its country code (e.g. +260971234567 or +263771234567).");
    }
}
