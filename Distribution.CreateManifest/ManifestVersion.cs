using System.Globalization;
using System.Text.RegularExpressions;

namespace Distribution.CreateManifest;

/// <summary>
///     The format of both of the manifest's versions: version 5, then a two-digit year, month, and day, which together must make a real date.
/// </summary>
internal static partial class ManifestVersion
{
    internal static string FromDate(DateTime utcNow)
        => string.Create(CultureInfo.InvariantCulture, $"5.{utcNow:yy}.{utcNow:MM}.{utcNow:dd}");

    internal static bool IsValid(string version)
    {
        Match match = VersionPattern().Match(version);

        if (match.Success is false)
            return false;

        string date = $"20{match.Groups["year"].Value}{match.Groups["month"].Value}{match.Groups["day"].Value}";

        return DateTime.TryParseExact(date, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
    }

    // "[0-9]" Rather Than "\d", Which Also Matches Other Scripts' Digits, And "\z" Rather Than "$", Which Also Accepts A Trailing Line Break
    [GeneratedRegex(@"^5\.(?<year>[0-9]{2})\.(?<month>[0-9]{2})\.(?<day>[0-9]{2})\z")]
    private static partial Regex VersionPattern();
}
