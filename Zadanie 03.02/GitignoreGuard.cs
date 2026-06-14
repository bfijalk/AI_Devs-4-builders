namespace Zadanie_03._02;

public class GitignoreGuard
{
    private readonly HashSet<string> _blockedPaths = new(StringComparer.Ordinal);

    public void RegisterFromGitignore(string directory, string gitignoreContent)
    {
        var dir = directory.TrimEnd('/');

        foreach (var rawLine in gitignoreContent.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;

            var entry = line.TrimEnd('/');
            var fullPath = entry.StartsWith('/')
                ? entry
                : $"{dir}/{entry}";

            _blockedPaths.Add(fullPath);
            _blockedPaths.Add($"{fullPath}/");
        }
    }

    public bool IsBlocked(string pathOrCommand)
    {
        foreach (var blocked in _blockedPaths)
        {
            if (pathOrCommand.Contains(blocked, StringComparison.Ordinal))
                return true;
        }

        return false;
    }
}
