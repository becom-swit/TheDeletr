namespace Thedeletr.Core;

public static class ExclusionParser
{
    private static readonly char[] Separators = [',', ';', '\r', '\n'];

    public static IReadOnlyList<string> Parse(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(Separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(p => p.Length > 0)
                .ToList();

    public static IReadOnlyList<string> Parse(IEnumerable<string>? values, FileInfo? file)
    {
        var result = new List<string>();
        foreach (var value in values ?? [])
        {
            result.AddRange(Parse(value));
        }

        if (file is not null)
        {
            result.AddRange(Parse(File.ReadAllText(file.FullName)));
        }

        return result.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }
}
