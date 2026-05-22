using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;

namespace RailwayAgent;

public class RailwayTools
{
    private readonly RailwayClient _client;
    private readonly ILogger<RailwayTools> _logger;

    // Zapamiętany postęp aktywacji per trasa: route -> (phase, steps)
    private readonly Dictionary<string, ActivationState> _activationState = new();

    public RailwayTools(RailwayClient client, ILogger<RailwayTools> logger)
    {
        _client = client;
        _logger = logger;
    }

    // --- Definicje narzędzi dla OpenAI ---

    public static IEnumerable<ChatTool> GetDefinitions() =>
    [
        ChatTool.CreateFunctionTool(
            "help",
            "Zwraca listę dostępnych akcji i parametrów API railway.",
            BinaryData.FromString("""{"type":"object","properties":{},"required":[]}""")
        ),
        ChatTool.CreateFunctionTool(
            "getstatus",
            "Pobiera aktualny status danej trasy.",
            BinaryData.FromString("""
            {
                "type": "object",
                "properties": {
                    "route": {
                        "type": "string",
                        "description": "Identyfikator trasy, np. a-1, b-12. Format: [a-z]-[0-9]{1,2}"
                    }
                },
                "required": ["route"]
            }
            """)
        ),
        ChatTool.CreateFunctionTool(
            "reconfigure",
            "Włącza tryb rekonfiguracji dla danej trasy (wymagany przed zmianą statusu).",
            BinaryData.FromString("""
            {
                "type": "object",
                "properties": {
                    "route": { "type": "string", "description": "Identyfikator trasy, np. a-1" }
                },
                "required": ["route"]
            }
            """)
        ),
        ChatTool.CreateFunctionTool(
            "setstatus",
            "Ustawia status trasy (RTOPEN = otwarta, RTCLOSE = zamknięta). Wymaga wcześniej reconfigure.",
            BinaryData.FromString("""
            {
                "type": "object",
                "properties": {
                    "route": { "type": "string", "description": "Identyfikator trasy, np. a-1" },
                    "value": { "type": "string", "enum": ["RTOPEN","RTCLOSE"], "description": "Nowy status trasy." }
                },
                "required": ["route","value"]
            }
            """)
        ),
        ChatTool.CreateFunctionTool(
            "save",
            "Zapisuje zmiany i wychodzi z trybu rekonfiguracji dla danej trasy.",
            BinaryData.FromString("""
            {
                "type": "object",
                "properties": {
                    "route": { "type": "string", "description": "Identyfikator trasy, np. a-1" }
                },
                "required": ["route"]
            }
            """)
        ),
        ChatTool.CreateFunctionTool(
            "activate_route",
            "Automatycznie aktywuje trasę wykonując sekwencję: getstatus → reconfigure → setstatus(RTOPEN) → save. Użyj gdy użytkownik chce aktywować lub otworzyć trasę.",
            BinaryData.FromString("""
            {
                "type": "object",
                "properties": {
                    "route": {
                        "type": "string",
                        "description": "Identyfikator trasy, np. a-1, b-12. Format: [a-z]-[0-9]{1,2}"
                    }
                },
                "required": ["route"]
            }
            """)
        ),
    ];

    // --- Dispatcher ---

    public async Task<string> DispatchAsync(string toolName, string argsJson)
    {
        _logger.LogInformation("Narzędzie: {Name}\n{Args}", toolName,
            JsonSerializer.Serialize(JsonSerializer.Deserialize<JsonElement>(argsJson),
                new JsonSerializerOptions { WriteIndented = true }));

        var args = JsonNode.Parse(argsJson)?.AsObject() ?? [];

        var result = toolName switch
        {
            "help"           => await HandleHelpAsync(),
            "getstatus"      => await HandleGetStatusAsync(args),
            "reconfigure"    => await HandleReconfigureAsync(args),
            "setstatus"      => await HandleSetStatusAsync(args),
            "save"           => await HandleSaveAsync(args),
            "activate_route" => await HandleActivateRouteAsync(args),
            _ => JsonNode.Parse($"{{\"error\":\"Nieznane narzędzie: {toolName}\"}}")!,
        };

        return result.ToJsonString();
    }

    // --- Handlery ---

    private async Task<JsonNode> HandleHelpAsync()
        => await CallApiAsync(new { action = "help" });

    private async Task<JsonNode> HandleGetStatusAsync(JsonObject args)
        => await CallApiAsync(new { action = "getstatus", route = args["route"]!.GetValue<string>() });

    private async Task<JsonNode> HandleReconfigureAsync(JsonObject args)
        => await CallApiAsync(new { action = "reconfigure", route = args["route"]!.GetValue<string>() });

    private async Task<JsonNode> HandleSetStatusAsync(JsonObject args)
        => await CallApiAsync(new
        {
            action = "setstatus",
            route = args["route"]!.GetValue<string>(),
            value = args["value"]!.GetValue<string>(),
        });

    private async Task<JsonNode> HandleSaveAsync(JsonObject args)
        => await CallApiAsync(new { action = "save", route = args["route"]!.GetValue<string>() });

    private async Task<JsonNode> HandleActivateRouteAsync(JsonObject args)
    {
        var route = args["route"]!.GetValue<string>();

        if (!_activationState.TryGetValue(route, out var state))
            state = new ActivationState("getstatus");

        _logger.LogInformation("Trasa {Route} — wznawianie od fazy: {Phase}", route, state.Phase);

        var steps = state.Steps;
        var phase = state.Phase;

        if (phase == "getstatus")
        {
            var result = await CallApiAsync(new { action = "getstatus", route });
            steps.Add(new JsonObject { ["step"] = "getstatus", ["result"] = result.DeepClone() });
            if (IsError(result))
            {
                _activationState[route] = state with { Phase = "getstatus" };
                return BuildError("getstatus failed", result, steps);
            }
            var current = result["status"]?.GetValue<string>() ?? result["value"]?.GetValue<string>();
            if (current == "RTOPEN")
            {
                _activationState.Remove(route);
                _logger.LogInformation("Trasa {Route} jest już otwarta — pomijam aktywację.", route);
                return new JsonObject
                {
                    ["ok"] = true,
                    ["message"] = $"Trasa {route} jest już aktywna (RTOPEN).",
                    ["steps"] = StepsToJson(steps),
                };
            }
            phase = "reconfigure";
        }

        if (phase == "reconfigure")
        {
            var result = await CallApiAsync(new { action = "reconfigure", route });
            steps.Add(new JsonObject { ["step"] = "reconfigure", ["result"] = result.DeepClone() });
            if (IsError(result))
            {
                _activationState[route] = state with { Phase = "reconfigure" };
                return BuildError("reconfigure failed", result, steps);
            }
            phase = "setstatus";
        }

        if (phase == "setstatus")
        {
            var result = await CallApiAsync(new { action = "setstatus", route, value = "RTOPEN" });
            steps.Add(new JsonObject { ["step"] = "setstatus", ["result"] = result.DeepClone() });
            if (IsError(result))
            {
                _activationState[route] = state with { Phase = "setstatus" };
                return BuildError("setstatus failed", result, steps);
            }
            phase = "save";
        }

        if (phase == "save")
        {
            var result = await CallApiAsync(new { action = "save", route });
            steps.Add(new JsonObject { ["step"] = "save", ["result"] = result.DeepClone() });
            if (IsError(result))
            {
                _activationState[route] = state with { Phase = "save" };
                return BuildError("save failed", result, steps);
            }
        }

        _activationState.Remove(route);
        return new JsonObject
        {
            ["ok"] = true,
            ["message"] = $"Trasa {route} została pomyślnie aktywowana (RTOPEN).",
            ["steps"] = StepsToJson(steps),
        };
    }

    // --- Helpers ---

    private async Task<JsonNode> CallApiAsync(object answer)
    {
        var doc = await _client.VerifyAsync(answer);
        return JsonNode.Parse(doc.RootElement.GetRawText())!;
    }

    private static bool IsError(JsonNode node)
    {
        if (node["error"] != null) return true;
        if (node["code"]?.GetValue<int>() is int code && code < 0) return true;
        return false;
    }

    private static JsonNode BuildError(string error, JsonNode details, List<JsonObject> steps) =>
        new JsonObject
        {
            ["error"] = error,
            ["details"] = details.DeepClone(),
            ["steps"] = StepsToJson(steps),
        };

    private static JsonArray StepsToJson(List<JsonObject> steps)
    {
        var arr = new JsonArray();
        foreach (var s in steps) arr.Add(s.DeepClone());
        return arr;
    }

    private record ActivationState(string Phase)
    {
        public List<JsonObject> Steps { get; init; } = [];
    }
}
