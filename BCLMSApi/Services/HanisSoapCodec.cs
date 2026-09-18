using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using ImageMagick;

namespace BCLMSApi.Services;

internal static class HanisSoapCodec
{
    private static readonly XNamespace Soap = "http://schemas.xmlsoap.org/soap/envelope/";
    private static readonly XNamespace Hanis = "http://HANIS.org/";

    internal static string Request(string id, HanisDirectOptions options, DateTimeOffset? requestUtc = null) => new XDocument(
        new XElement(Soap + "Envelope", new XAttribute(XNamespace.Xmlns + "soap", Soap),
            new XElement(Soap + "Body", new XElement(Hanis + "GetData",
                new XElement(Hanis + "IDN", id),
                new XElement(Hanis + "TransactionDate", (requestUtc ?? DateTimeOffset.UtcNow).ToOffset(TimeSpan.FromHours(2)).ToString("ddMMyyyy HHmmss", CultureInfo.InvariantCulture)),
                new XElement(Hanis + "SiteID", options.SiteId),
                new XElement(Hanis + "WrkStnID", options.WorkstationId))))).ToString(SaveOptions.DisableFormatting);

    internal static HanisVerificationService.HanisResult Parse(string xml)
    {
        using var input = new StringReader(xml);
        using var reader = XmlReader.Create(input, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 10 * 1024 * 1024
        });
        var doc = XDocument.Load(reader);
        var body = doc.Root?.Name == Soap + "Envelope" ? doc.Root.Element(Soap + "Body") : null;
        if (body?.Element(Soap + "Fault") is not null)
            throw new ArgumentException("Home Affairs returned a service fault. This step remains pending.");
        var data = body?.Element(Hanis + "GetDataResponse")?.Element(Hanis + "GetDataResult")
            ?? throw new XmlException("Missing HANIS result.");
        string? Value(string name) => data.Element(Hanis + name)?.Value.Trim();
        bool? Flag(string name) => Value(name) switch { "true" or "1" => true, "false" or "0" => false, _ => null };
        if (!int.TryParse(Value("Error"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var code))
            throw new XmlException("Missing HANIS error code.");
        if (code == 1056)
        {
            var error = new ArgumentException("Home Affairs rejected the transaction date (1056).");
            error.Data["HanisErrorCode"] = code;
            throw error;
        }
        if (code != 0)
            throw new ArgumentException(code switch
            {
                500 or 600 or 800 or 10004 => "Home Affairs did not confirm a valid population-register ID. This step remains pending.",
                10012 or 10014 or 10015 or 10016 or 10070 or 10071 => "Home Affairs access registration is not configured correctly. Contact the system administrator.",
                _ => "Home Affairs demographic verification is unavailable. Try again later; this step remains pending."
            });
        var photo = Value("Photo");
        return new()
        {
            Success = true, ErrorCode = code, IdNumber = Value("IDN"),
            HanisTransactionId = Value("TranNo"), Forenames = Value("Name"), Surname = Value("Surname"),
            IsDeceased = Flag("DeadIndicator"), IsIdBlocked = Flag("IDNBlocked"),
            IsOnNpr = Flag("OnNPR"), IsOnHanis = Flag("OnHANIS"), SmartCardIssued = Flag("SmartCardIssued"),
            IdIssueDate = Value("IDIssueDate"), DateOfDeath = Value("DateOfDeath"),
            MaritalStatus = Value("MaritalStatus"), BirthCountryCode = Value("BirthPlaceCountryCode"),
            HasPhoto = !string.IsNullOrEmpty(photo), PhotoJpegBase64 = ConvertPhoto(photo)
        };
    }

    private static string? ConvertPhoto(string? photo)
    {
        if (string.IsNullOrWhiteSpace(photo)) return null;
        if (photo.Length > 7 * 1024 * 1024) throw new FormatException("Photo too large.");
        var bytes = Convert.FromBase64String(photo);
        if (bytes.Length > 5 * 1024 * 1024) throw new FormatException("Photo too large.");
        var jpeg = bytes.Length >= 3 && bytes[0] == 255 && bytes[1] == 216 && bytes[2] == 255;
        var j2k = bytes.Length >= 4 && bytes[0] == 255 && bytes[1] == 79 && bytes[2] == 255 && bytes[3] == 81;
        var jp2 = bytes.AsSpan().StartsWith(new byte[] { 0, 0, 0, 12, 106, 80, 32, 32, 13, 10, 135, 10 });
        if (!jpeg && !j2k && !jp2) throw new FormatException("Unsupported photo format.");
        var settings = new MagickReadSettings { Format = jpeg ? MagickFormat.Jpeg : j2k ? MagickFormat.J2k : MagickFormat.Jp2 };
        using var image = new MagickImage();
        image.Ping(bytes, settings);
        if (image.Width == 0 || image.Height == 0 || (ulong)image.Width * image.Height > 16_000_000)
            throw new FormatException("Photo dimensions exceed limit.");
        image.Read(bytes, settings);
        image.Strip();
        image.Quality = 85;
        var output = image.ToByteArray(MagickFormat.Jpeg);
        if (output.Length > 5 * 1024 * 1024) throw new FormatException("Converted photo too large.");
        return Convert.ToBase64String(output);
    }
}
