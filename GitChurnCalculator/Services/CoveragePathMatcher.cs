namespace GitChurnCalculator.Services;

/// <summary>
/// Shared utilities for matching coverage file paths to git-tracked file paths.
/// </summary>
public static class CoveragePathMatcher
{
    /// <summary>
    /// Attempts to match coverage file paths to git-tracked file paths using
    /// exact match, suffix match, and filename-only match (in that priority order).
    /// </summary>
    public static Dictionary<string, double> MapToGitFiles(
        Dictionary<string, double> coverageByPath,
        IReadOnlyList<string> gitFiles)
    {
        var index = GitPathMatchIndex.Create(gitFiles);
        var result = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        foreach (var (rawCovPath, percent) in coverageByPath)
        {
            var covPath = NormalizePath(rawCovPath);

            if (index.TryMatch(covPath, out var gitMatch))
                result[gitMatch] = percent;
        }

        return result;
    }

    public static string NormalizePath(string path)
    {
        return path.Replace('\\', '/').TrimEnd('/');
    }

    private static string GetFileName(string path)
    {
        var lastSlash = path.LastIndexOf('/');
        return lastSlash < 0 ? path : path[(lastSlash + 1)..];
    }

    /// <summary>
    /// Precomputed indexes so each coverage path is matched in O(path length)
    /// instead of scanning every git-tracked file.
    /// </summary>
    private sealed class GitPathMatchIndex
    {
        private readonly Dictionary<string, string> _exactLookup;
        private readonly Dictionary<string, string> _gitPathEndsWithLookup;
        private readonly Dictionary<string, string> _fileNameLookup;
        private readonly Dictionary<string, int> _gitOrder;

        private GitPathMatchIndex(
            Dictionary<string, string> exactLookup,
            Dictionary<string, string> gitPathEndsWithLookup,
            Dictionary<string, string> fileNameLookup,
            Dictionary<string, int> gitOrder)
        {
            _exactLookup = exactLookup;
            _gitPathEndsWithLookup = gitPathEndsWithLookup;
            _fileNameLookup = fileNameLookup;
            _gitOrder = gitOrder;
        }

        public static GitPathMatchIndex Create(IReadOnlyList<string> gitFiles)
        {
            var exactLookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var gitPathEndsWithLookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var fileNameLookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var gitOrder = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < gitFiles.Count; i++)
            {
                var original = gitFiles[i];
                var normalized = NormalizePath(original);
                gitOrder[original] = i;
                exactLookup[normalized] = original;

                RegisterGitPathSuffixes(normalized, original, gitPathEndsWithLookup);

                var fileName = GetFileName(normalized);
                if (!fileNameLookup.ContainsKey(fileName))
                    fileNameLookup[fileName] = original;
            }

            return new GitPathMatchIndex(exactLookup, gitPathEndsWithLookup, fileNameLookup, gitOrder);
        }

        public bool TryMatch(string covPath, out string originalGit)
        {
            if (_exactLookup.TryGetValue(covPath, out originalGit))
                return true;

            if (TryGetSuffixGitMatch(covPath, out originalGit))
                return true;

            var fileName = GetFileName(covPath);
            if (_fileNameLookup.TryGetValue(fileName, out originalGit))
                return true;

            originalGit = null!;
            return false;
        }

        private bool TryGetSuffixGitMatch(string covPath, out string originalGit)
        {
            string? bestMatch = null;
            var bestOrder = int.MaxValue;

            if (_gitPathEndsWithLookup.TryGetValue(covPath, out var rule1Git))
            {
                bestMatch = rule1Git;
                bestOrder = _gitOrder[rule1Git];
            }

            var rule2Git = FindEarliestGitPathAsCoverageSuffix(covPath);
            if (rule2Git is not null && _gitOrder[rule2Git] < bestOrder)
            {
                bestMatch = rule2Git;
                bestOrder = _gitOrder[rule2Git];
            }

            if (bestMatch is null)
            {
                originalGit = null!;
                return false;
            }

            originalGit = bestMatch;
            return true;
        }

        private string? FindEarliestGitPathAsCoverageSuffix(string covPath)
        {
            string? bestMatch = null;
            var bestOrder = int.MaxValue;

            for (var start = 0; ; )
            {
                var suffix = start == 0 ? covPath : covPath[start..];
                if (_exactLookup.TryGetValue(suffix, out var git))
                {
                    var order = _gitOrder[git];
                    if (order < bestOrder)
                    {
                        bestOrder = order;
                        bestMatch = git;
                    }
                }

                var nextSlash = covPath.IndexOf('/', start);
                if (nextSlash < 0)
                    break;

                start = nextSlash + 1;
            }

            return bestMatch;
        }

        private static void RegisterGitPathSuffixes(
            string normalizedGit,
            string originalGit,
            Dictionary<string, string> gitPathEndsWithLookup)
        {
            for (var slash = normalizedGit.IndexOf('/'); slash >= 0; slash = normalizedGit.IndexOf('/', slash + 1))
            {
                var suffix = normalizedGit[(slash + 1)..];
                if (!gitPathEndsWithLookup.ContainsKey(suffix))
                    gitPathEndsWithLookup[suffix] = originalGit;
            }
        }
    }
}
