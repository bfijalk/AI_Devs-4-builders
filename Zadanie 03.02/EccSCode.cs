using System.Text.RegularExpressions;

namespace Zadanie_03._02;

public static partial class EccSCode
{
    [GeneratedRegex(@"ECCS-[0-9a-f]{40}", RegexOptions.Compiled)]
    private static partial Regex Pattern();

    public static string? Extract(string text) => Pattern().Match(text) is { Success: true } m ? m.Value : null;
}
