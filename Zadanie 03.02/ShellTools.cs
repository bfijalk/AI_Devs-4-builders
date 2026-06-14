using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;

namespace Zadanie_03._02;

public class ShellTools
{
    private static readonly string[] ForbiddenPrefixes = ["/etc", "/root", "/proc"];
    private const string CoolerBinPath = "/opt/firmware/cooler/cooler.bin";

    private readonly ShellClient _client;
    private readonly GitignoreGuard _gitignoreGuard;
    private readonly ILogger<ShellTools> _logger;

    public ShellTools(ShellClient client, ILogger<ShellTools> logger)
    {
        _client = client;
        _gitignoreGuard = new GitignoreGuard();
        _logger = logger;
    }

    public Task<string> HelpAsync() => ExecuteAsync("help");

    public Task<string> LsAsync(string? path = null) =>
        ExecuteAsync(path is null ? "ls" : $"ls {path}");

    public Task<string> CatAsync(string path) =>
        ExecuteAsync($"cat {path}");

    public Task<string> CdAsync(string? path = null) =>
        ExecuteAsync(path is null ? "cd" : $"cd {path}");

    public Task<string> PwdAsync() => ExecuteAsync("pwd");

    public Task<string> RmAsync(string file) =>
        ExecuteAsync($"rm {file}");

    public Task<string> EditLineAsync(string file, int lineNumber, string content) =>
        ExecuteAsync($"editline {file} {lineNumber} {content}");

    public Task<string> RebootAsync() => ExecuteAsync("reboot");

    public Task<string> DateAsync() => ExecuteAsync("date");

    public Task<string> UptimeAsync() => ExecuteAsync("uptime");

    public Task<string> FindAsync(string pattern) =>
        ExecuteAsync($"find {pattern}");

    public Task<string> HistoryAsync() => ExecuteAsync("history");

    public Task<string> WhoamiAsync() => ExecuteAsync("whoami");

    public Task<string> RunAsync(string command) =>
        ExecuteAsync(command);

    public async Task<string> DispatchAsync(string toolName, string argsJson)
    {
        _logger.LogInformation("Narzędzie: {Name}\n{Args}", toolName,
            JsonSerializer.Serialize(JsonSerializer.Deserialize<JsonElement>(argsJson),
                new JsonSerializerOptions { WriteIndented = true }));

        var args = JsonNode.Parse(argsJson)?.AsObject() ?? [];

        return toolName switch
        {
            "shell_help"    => await HelpAsync(),
            "shell_ls"      => await LsAsync(args["path"]?.GetValue<string>()),
            "shell_cat"     => await CatAsync(Require(args, "path")),
            "shell_cd"      => await CdAsync(args["path"]?.GetValue<string>()),
            "shell_pwd"     => await PwdAsync(),
            "shell_rm"      => await RmAsync(Require(args, "file")),
            "shell_editline" => await EditLineAsync(
                Require(args, "file"),
                args["line_number"]!.GetValue<int>(),
                Require(args, "content")),
            "shell_reboot"  => await RebootAsync(),
            "shell_date"    => await DateAsync(),
            "shell_uptime"  => await UptimeAsync(),
            "shell_find"    => await FindAsync(Require(args, "pattern")),
            "shell_history" => await HistoryAsync(),
            "shell_whoami"  => await WhoamiAsync(),
            "shell_run"     => await RunAsync(Require(args, "command")),
            _ => JsonSerializer.Serialize(new { error = $"Nieznane narzędzie: {toolName}" }),
        };
    }

    public static IEnumerable<ChatTool> GetToolDefinitions() =>
        GetDefinitions().Select(t => ChatTool.CreateFunctionTool(
            t.Name,
            t.Description,
            BinaryData.FromString(t.ParametersJson)));

    public static IReadOnlyList<ToolDefinition> GetDefinitions() =>
    [
        new("shell_help", "Wyświetla dostępne komendy shell i ich opisy.", "{}"),
        new("shell_ls", "Listuje pliki i katalogi.", """{"type":"object","properties":{"path":{"type":"string","description":"Opcjonalna ścieżka do listowania."}}}"""),
        new("shell_cat", "Wyświetla zawartość pliku (lub listuje katalog).", """{"type":"object","properties":{"path":{"type":"string","description":"Ścieżka do pliku lub katalogu."}},"required":["path"]}"""),
        new("shell_cd", "Zmienia bieżący katalog roboczy.", """{"type":"object","properties":{"path":{"type":"string","description":"Opcjonalna ścieżka docelowa."}}}"""),
        new("shell_pwd", "Wyświetla bieżący katalog roboczy.", "{}"),
        new("shell_rm", "Usuwa plik z wirtualnego systemu plików.", """{"type":"object","properties":{"file":{"type":"string","description":"Ścieżka do pliku do usunięcia."}},"required":["file"]}"""),
        new("shell_editline", "Zastępuje jedną linię w pliku tekstowym.", """{"type":"object","properties":{"file":{"type":"string","description":"Ścieżka do pliku."},"line_number":{"type":"integer","description":"Numer linii do podmiany (1-based)."},"content":{"type":"string","description":"Nowa treść linii."}},"required":["file","line_number","content"]}"""),
        new("shell_reboot", "Odtwarza stan wirtualnego systemu plików z dysku.", "{}"),
        new("shell_date", "Wyświetla aktualną datę i czas serwera.", "{}"),
        new("shell_uptime", "Wyświetla czas pracy maszyny wirtualnej.", "{}"),
        new("shell_find", "Wyszukuje pliki po nazwie (obsługuje wildcards).", """{"type":"object","properties":{"pattern":{"type":"string","description":"Wzorzec nazwy pliku, np. *.txt lub pass.txt."}},"required":["pattern"]}"""),
        new("shell_history", "Wyświetla historię wykonanych komend.", "{}"),
        new("shell_whoami", "Wyświetla nazwę bieżącego użytkownika.", "{}"),
        new("shell_run", "Uruchamia dowolną dozwoloną komendę shell, np. plik binarny z hasłem.", """{"type":"object","properties":{"command":{"type":"string","description":"Pełna komenda do wykonania, np. /opt/firmware/cooler/cooler.bin haslo123."}},"required":["command"]}"""),
    ];

    private async Task<string> ExecuteAsync(string cmd)
    {
        ValidateCommand(cmd);
        _logger.LogInformation("Shell cmd: {Cmd}", cmd);
        var doc = await _client.SendCommandAsync(cmd);
        var result = doc.RootElement.GetRawText();

        if (cmd.StartsWith("cat ", StringComparison.Ordinal) && cmd.Contains(".gitignore", StringComparison.Ordinal))
            TryRegisterGitignore(cmd, doc.RootElement);

        return result;
    }

    private void TryRegisterGitignore(string cmd, JsonElement response)
    {
        if (!response.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.String)
            return;

        var path = cmd["cat ".Length..].Trim();
        var directory = path[..path.LastIndexOf('/')];
        _gitignoreGuard.RegisterFromGitignore(directory, data.GetString() ?? string.Empty);
        _logger.LogInformation("Zarejestrowano wpisy .gitignore z {Dir}", directory);
    }

    private void ValidateCommand(string cmd)
    {
        foreach (var prefix in ForbiddenPrefixes)
        {
            if (cmd.Contains(prefix, StringComparison.Ordinal))
                throw new InvalidOperationException($"Zabroniony dostęp do {prefix}");
        }

        if (_gitignoreGuard.IsBlocked(cmd))
            throw new InvalidOperationException(
                "Zabroniony dostęp do pliku/katalogu wymienionego w .gitignore.");

        if (cmd.StartsWith("cat ", StringComparison.Ordinal)
            && cmd.TrimEnd().Equals($"cat {CoolerBinPath}", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "cooler.bin jest zaszyfrowany — nie używaj shell_cat. " +
                "Uruchom cooler.bin przez shell_run i odczytaj odszyfrowaną treść z pola data odpowiedzi.");
        }
    }

    private static string Require(JsonObject args, string key) =>
        args[key]?.GetValue<string>()
        ?? throw new ArgumentException($"Brak wymaganego parametru: {key}");
}

public record ToolDefinition(string Name, string Description, string ParametersJson);
