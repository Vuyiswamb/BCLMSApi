using System.Net;
using System.Xml.Linq;

namespace BclmsOverdueReminderService;

public sealed class EmailTemplateRenderer
{
    public RenderedEmail Render(string templateName, IReadOnlyDictionary<string, string?> values)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Templates", templateName);
        var template = XDocument.Load(path).Root ?? throw new InvalidOperationException($"Email template '{templateName}' is invalid.");

        return new RenderedEmail(
            Replace(template.Element("Subject")?.Value ?? string.Empty, values, false),
            Replace(template.Element("HtmlBody")?.Value ?? string.Empty, values, true));
    }

    private static string Replace(string content, IReadOnlyDictionary<string, string?> values, bool encodeHtml)
    {
        foreach (var (key, value) in values)
            content = content.Replace($"{{{{{key}}}}}", encodeHtml ? WebUtility.HtmlEncode(value ?? string.Empty) : value ?? string.Empty, StringComparison.Ordinal);

        return content;
    }
}

public sealed record RenderedEmail(string Subject, string HtmlBody);
