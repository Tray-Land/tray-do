using System.Text.RegularExpressions;

namespace TrayDo.Tasks;

/// <summary>
/// Turns text pasted from another app (a note, an email, a Markdown checklist) into one task per
/// line, without the bullets, numbers and checkboxes that list came with.
/// </summary>
public static partial class PastedTasks
{
    /// <summary>True when the text holds more than one line, so a paste should become several tasks.</summary>
    public static bool IsMultiLine(string? text) => Split(text).Count > 1;

    public static IReadOnlyList<string> Split(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return [];
        }

        List<string> lines = [];
        foreach (string line in text.Split(["\r\n", "\n", "\r"], StringSplitOptions.None))
        {
            string cleaned = CleanLine(line);
            if (cleaned.Length > 0)
            {
                lines.Add(cleaned);
            }
        }

        return lines;
    }

    /// <summary>Strips list markers ("- ", "* ", "• ", "1. ", "2) ", "[ ] ", "- [x] ") and surrounding space.</summary>
    public static string CleanLine(string line)
    {
        string trimmed = line.Trim();

        // Markers can stack, as in "- [ ] task".
        for (int i = 0; i < 3; i++)
        {
            Match match = ListMarker().Match(trimmed);
            if (!match.Success || match.Length == trimmed.Length)
            {
                break;
            }

            trimmed = trimmed[match.Length..].TrimStart();
        }

        return trimmed;
    }

    [GeneratedRegex(@"^(?:[-*+•·◦▪▫‣⁃–—○●■□☐☑☒✓✔]|\d{1,3}[.)]|\[[ xX✓]?\])\s+")]
    private static partial Regex ListMarker();
}
