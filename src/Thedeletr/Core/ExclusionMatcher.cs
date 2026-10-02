using System.Text.RegularExpressions;

namespace Thedeletr.Core;

/// <summary>
/// Matches folders and files against exclusion patterns (case-insensitive).
/// Pattern without a path separator: matched against the name only (supports * wildcards, e.g. *tmp, tmp*, *tmp*).
/// Pattern with a path separator: fully qualified paths are matched against the full path,
/// relative paths are resolved against the root folder. * wildcards are supported as well.
/// </summary>
public sealed class ExclusionMatcher
{
    private readonly List<Regex> _namePatterns = [];
    private readonly List<Regex> _pathPatterns = [];

    public ExclusionMatcher(IEnumerable<string> patterns, string rootFolder)
    {
        var root = NormalizePath(Path.GetFullPath(rootFolder));

        foreach (var raw in patterns)
        {
            var pattern = raw.Trim().Trim('"');
            if (pattern.Length == 0)
            {
                continue;
            }

            if (pattern.IndexOfAny(['\\', '/']) < 0)
            {
                _namePatterns.Add(ToRegex(pattern));
                continue;
            }

            var normalized = NormalizePath(pattern);
            var fullPattern = Path.IsPathFullyQualified(normalized)
                ? normalized
                : NormalizePath(Path.Combine(root, normalized.TrimStart(Path.DirectorySeparatorChar)));
            _pathPatterns.Add(ToRegex(fullPattern));
        }
    }

    public bool IsEmpty => _namePatterns.Count == 0 && _pathPatterns.Count == 0;

    public bool IsExcluded(string fullPath)
    {
        if (IsEmpty)
        {
            return false;
        }

        var normalized = NormalizePath(fullPath);
        var name = Path.GetFileName(normalized);

        return _namePatterns.Any(r => r.IsMatch(name)) || _pathPatterns.Any(r => r.IsMatch(normalized));
    }

    private static string NormalizePath(string path) =>
        path.Replace('/', Path.DirectorySeparatorChar).TrimEnd(Path.DirectorySeparatorChar);

    private static Regex ToRegex(string pattern) =>
        new("^" + Regex.Escape(pattern).Replace(@"\*", ".*") + "$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
}
