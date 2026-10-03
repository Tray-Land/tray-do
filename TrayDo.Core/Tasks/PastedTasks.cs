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

        List<string> lines =
        [
            .. text.Split(["\r\n", "\n", "\r"], StringSplitOptions.None)
                .Select(l => StripInvisible(l).Trim())
                .Where(l => l.Length > 0),
        ];

        // Look at the whole paste before each line on its own, so an artifact every line shares
        // (a quote mark, a table bar, an arrow, an odd bullet) is stripped even if it isn't a known marker.
        int shared = lines.Count > 1 ? SharedPrefixLength(lines) : 0;
        return
        [
            .. lines
                .Select(l => CleanLine(l[shared..]))
                .Where(l => l.Length > 0),
        ];
    }

    /// <summary>
    /// Length of the leading run of symbols and spaces that every line starts with, cut back to end
    /// at a space so "(maybe) a" and "(later) b" keep their brackets, as do "-5 C" and "-3 C".
    /// </summary>
    private static int SharedPrefixLength(List<string> lines)
    {
        string first = lines[0];
        int length = 0;
        while (length < first.Length
            && !char.IsLetterOrDigit(first[length])
            && lines.All(l => length < l.Length && l[length] == first[length]))
        {
            length++;
        }

        while (length > 0 && !char.IsWhiteSpace(first[length - 1]))
        {
            length--;
        }

        return length;
    }

    /// <summary>Drops byte order marks and zero-width characters that ride along with copied text.</summary>
    private static string StripInvisible(string line) =>
        line.Contains('﻿') || line.Contains('​') || line.Contains('‌') || line.Contains('‍')
            ? line.Replace("﻿", "").Replace("​", "").Replace("‌", "").Replace("‍", "")
            : line;

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
