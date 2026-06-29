using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;

namespace Zadanie_04._02;

public sealed record WindPowerToolCall(string Name, string ArgumentsJson);

public sealed class WindPowerAgentTools
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    private readonly WindPowerClient _client;
    private readonly ILogger<WindPowerAgentTools> _logger;

    public WindPowerAgentTools(WindPowerClient client, ILogger<WindPowerAgentTools> logger)
    {
        _client = client;
        _logger = logger;
    }

    public Task<string> HelpAsync(CancellationToken cancellationToken = default)
        => CallAsync(new { action = "help" }, cancellationToken);

    public Task<string> StartAsync(CancellationToken cancellationToken = default)
        => CallAsync(new { action = "start" }, cancellationToken);

    public Task<string> GetAsync(string param, CancellationToken cancellationToken = default)
        => CallAsync(new { action = "get", param }, cancellationToken);

    public Task<string> GetResultAsync(CancellationToken cancellationToken = default)
        => CallAsync(new { action = "getResult" }, cancellationToken);

    public Task<string> ConfigAsync(
        string startDate,
        string startHour,
        int pitchAngle,
        string turbineMode,
        string unlockCode,
        CancellationToken cancellationToken = default)
        => CallAsync(new
        {
            action = "config",
            startDate,
            startHour,
            pitchAngle,
            turbineMode,
            unlockCode,
        }, cancellationToken);

    public Task<string> ConfigBatchAsync(
        IReadOnlyDictionary<string, WindPowerConfigEntry> configs,
        CancellationToken cancellationToken = default)
        => CallAsync(new { action = "config", configs }, cancellationToken);

    public Task<string> UnlockCodeGeneratorAsync(
        string startDate,
        string startHour,
        double windMs,
        int pitchAngle,
        CancellationToken cancellationToken = default)
        => CallAsync(new
        {
            action = "unlockCodeGenerator",
            startDate,
            startHour,
            windMs,
            pitchAngle,
        }, cancellationToken);

    public Task<string> DoneAsync(CancellationToken cancellationToken = default)
        => CallAsync(new { action = "done" }, cancellationToken);

    public async Task<string> CollectQueuedResultsAsync(
        int maxItems = 32,
        int pollDelayMs = 100,
        CancellationToken cancellationToken = default)
    {
        var collected = new List<JsonNode>();

        for (var i = 0; i < maxItems; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var raw = await GetResultAsync(cancellationToken);
            var node = JsonNode.Parse(raw);
            if (node is null)
            {
                break;
            }

            var response = node["response"]?.AsObject();
            if (IsQueueNotReady(response))
            {
                if (collected.Count == 0 && pollDelayMs > 0)
                {
                    await Task.Delay(pollDelayMs, cancellationToken);
                    continue;
                }

                break;
            }

            collected.Add(node);
        }

        return JsonSerializer.Serialize(new
        {
            ok = true,
            count = collected.Count,
            results = collected,
        }, JsonOpts);
    }

    public async Task<string> DispatchAsync(
        string toolName,
        string argsJson,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Narzędzie: {Name}", toolName);
        _logger.LogDebug("Args: {Args}", argsJson);

        var args = JsonNode.Parse(argsJson)?.AsObject() ?? [];

        try
        {
            return toolName switch
            {
                "windpower_help" => await HelpAsync(cancellationToken),
                "windpower_start" => await StartAsync(cancellationToken),
                "windpower_get" => await GetAsync(Require(args, "param"), cancellationToken),
                "windpower_get_result" => await GetResultAsync(cancellationToken),
                "windpower_collect_results" => await CollectQueuedResultsAsync(
                    args["max_items"]?.GetValue<int>() ?? 32,
                    args["poll_delay_ms"]?.GetValue<int>() ?? 100,
                    cancellationToken),
                "windpower_config" => await DispatchConfigAsync(args, cancellationToken),
                "windpower_unlock_code_generator" => await UnlockCodeGeneratorAsync(
                    Require(args, "startDate"),
                    Require(args, "startHour"),
                    args["windMs"]!.GetValue<double>(),
                    args["pitchAngle"]!.GetValue<int>(),
                    cancellationToken),
                "windpower_done" => await DoneAsync(cancellationToken),
                _ => JsonSerializer.Serialize(new { ok = false, error = $"Nieznane narzędzie: {toolName}" }),
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd narzędzia {Name}", toolName);
            return JsonSerializer.Serialize(new { ok = false, error = ex.Message });
        }
    }

    public async Task<string> DispatchParallelAsync(
        IReadOnlyList<WindPowerToolCall> calls,
        CancellationToken cancellationToken = default)
    {
        if (calls.Count == 0)
        {
            return JsonSerializer.Serialize(new { ok = false, error = "Brak wywołań do wykonania." });
        }

        _logger.LogDebug("Równoległe wywołanie {Count} narzędzi", calls.Count);

        var tasks = calls.Select(call =>
            DispatchAsync(call.Name, call.ArgumentsJson, cancellationToken));

        var results = await Task.WhenAll(tasks);

        return JsonSerializer.Serialize(new
        {
            ok = true,
            count = results.Length,
            results = results.Select((result, index) => new
            {
                tool = calls[index].Name,
                result = JsonNode.Parse(result),
            }),
        }, JsonOpts);
    }

    public static IEnumerable<ChatTool> GetDefinitions() =>
        GetToolDefinitions().Select(tool => ChatTool.CreateFunctionTool(
            tool.Name,
            tool.Description,
            BinaryData.FromString(tool.ParametersJson)));

    public static IReadOnlyList<ToolDefinition> GetToolDefinitions() =>
    [
        new(
            "windpower_help",
            "Zwraca dokumentację API windpower (dostępne akcje i reguły).",
            """{"type":"object","properties":{}}"""),
        new(
            "windpower_start",
            "Rozpoczyna nowe okno serwisowe. Wywołaj przed innymi akcjami.",
            """{"type":"object","properties":{}}"""),
        new(
            "windpower_get",
            "Żąda danych zadania. Dla weather, turbinecheck i powerplantcheck wynik trafia do kolejki — odbierz go przez windpower_get_result lub windpower_collect_results. Documentation zwraca się od razu.",
            """
            {
              "type": "object",
              "properties": {
                "param": {
                  "type": "string",
                  "enum": ["weather", "turbinecheck", "powerplantcheck", "documentation"],
                  "description": "Rodzaj danych do pobrania."
                }
              },
              "required": ["param"]
            }
            """),
        new(
            "windpower_get_result",
            "Pobiera jedną ukończoną odpowiedź z kolejki asynchronicznej (pole sourceFunction).",
            """{"type":"object","properties":{}}"""),
        new(
            "windpower_collect_results",
            "Pobiera wszystkie dostępne odpowiedzi z kolejki, wywołując getResult w pętli.",
            """
            {
              "type": "object",
              "properties": {
                "max_items": {
                  "type": "integer",
                  "description": "Maksymalna liczba elementów do pobrania z kolejki.",
                  "default": 32
                },
                "poll_delay_ms": {
                  "type": "integer",
                  "description": "Opóźnienie między próbami, gdy kolejka jeszcze nie ma wyników.",
                  "default": 100
                }
              }
            }
            """),
        new(
            "windpower_config",
            "Zapisuje konfigurację turbiny. Podaj pojedynczy punkt albo mapę configs z kluczami YYYY-MM-DD HH:mm:ss.",
            """
            {
              "type": "object",
              "properties": {
                "startDate": { "type": "string", "description": "Data w formacie YYYY-MM-DD." },
                "startHour": { "type": "string", "description": "Godzina w formacie HH:mm:ss." },
                "pitchAngle": { "type": "integer", "description": "Kąt łopat turbiny (0, 45 lub 90)." },
                "turbineMode": {
                  "type": "string",
                  "enum": ["production", "idle"],
                  "description": "production = generacja, idle = turbina wyłączona."
                },
                "unlockCode": { "type": "string", "description": "Kod odblokowujący wymagany dla każdego punktu." },
                "configs": {
                  "type": "object",
                  "description": "Wiele punktów konfiguracyjnych naraz.",
                  "additionalProperties": {
                    "type": "object",
                    "properties": {
                      "pitchAngle": { "type": "integer" },
                      "turbineMode": { "type": "string", "enum": ["production", "idle"] },
                      "unlockCode": { "type": "string" }
                    },
                    "required": ["pitchAngle", "turbineMode", "unlockCode"]
                  }
                }
              }
            }
            """),
        new(
            "windpower_unlock_code_generator",
            "Generuje unlockCode dla podanej konfiguracji. Wynik jest asynchroniczny — odbierz go przez windpower_get_result.",
            """
            {
              "type": "object",
              "properties": {
                "startDate": { "type": "string", "description": "Data w formacie YYYY-MM-DD." },
                "startHour": { "type": "string", "description": "Godzina w formacie HH:mm:ss." },
                "windMs": { "type": "number", "description": "Prędkość wiatru w m/s." },
                "pitchAngle": { "type": "integer", "description": "Kąt łopat turbiny." }
              },
              "required": ["startDate", "startHour", "windMs", "pitchAngle"]
            }
            """),
        new(
            "windpower_done",
            "Waliduje końcową konfigurację i zwraca flagę po sukcesie. Wymaga wcześniejszego turbinecheck.",
            """{"type":"object","properties":{}}"""),
    ];

    private async Task<string> DispatchConfigAsync(
        JsonObject args,
        CancellationToken cancellationToken)
    {
        if (args["configs"] is JsonObject configsNode)
        {
            var configs = configsNode.ToDictionary(
                pair => pair.Key,
                pair => new WindPowerConfigEntry(
                    pair.Value!["pitchAngle"]!.GetValue<int>(),
                    pair.Value!["turbineMode"]!.GetValue<string>()!,
                    pair.Value!["unlockCode"]!.GetValue<string>()!));

            return await ConfigBatchAsync(configs, cancellationToken);
        }

        return await ConfigAsync(
            Require(args, "startDate"),
            Require(args, "startHour"),
            args["pitchAngle"]!.GetValue<int>(),
            Require(args, "turbineMode"),
            Require(args, "unlockCode"),
            cancellationToken);
    }

    private async Task<string> CallAsync(object answer, CancellationToken cancellationToken)
    {
        var result = await _client.TrySendActionAsync(answer, cancellationToken);
        if (!result.Success)
        {
            return JsonSerializer.Serialize(new
            {
                ok = false,
                statusCode = result.StatusCode,
                message = result.GetMessage(),
                body = result.Body,
            }, JsonOpts);
        }

        return JsonSerializer.Serialize(new
        {
            ok = true,
            response = result.Document is null
                ? JsonValue.Create(result.Body)
                : JsonNode.Parse(result.Document.RootElement.GetRawText()),
        }, JsonOpts);
    }

    private static bool IsQueueNotReady(JsonObject? response)
    {
        if (response is null)
        {
            return false;
        }

        if (response.TryGetPropertyValue("code", out var codeNode) &&
            codeNode is JsonValue codeValue &&
            codeValue.TryGetValue<int>(out var code) &&
            code == 11)
        {
            return true;
        }

        var message = response["message"]?.GetValue<string>();
        return !string.IsNullOrWhiteSpace(message) &&
               message.Contains("No completed queued response", StringComparison.OrdinalIgnoreCase);
    }

    private static string Require(JsonObject args, string key)
        => args[key]?.GetValue<string>()
           ?? throw new ArgumentException($"Brak wymaganego parametru: {key}");
}

public sealed record WindPowerConfigEntry(
    int PitchAngle,
    string TurbineMode,
    string UnlockCode);

public record ToolDefinition(string Name, string Description, string ParametersJson);
