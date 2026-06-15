using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Zadanie_03._03;

public sealed class ReactorAgentResult
{
    public required IReadOnlyList<string> Commands { get; init; }
    public required ReactorState FinalState { get; init; }
    public JsonDocument? LastResponse { get; init; }
    public bool Aborted { get; init; }
    public string? Flag { get; init; }
    public bool Completed => !Aborted && (Flag is not null || FinalState.ReachedGoal || FinalState.PlayerCol == FinalState.GoalCol);
}

public class ReactorAgent
{
    private static readonly Regex FlagRegex = new(@"\{FLG:[A-Z0-9]+\}", RegexOptions.IgnoreCase);

    private readonly ReactorClient _client;
    private readonly ILogger<ReactorAgent> _logger;
    private readonly IReactorStepPrompter _prompter;
    private JsonDocument? _lastSetupResponse;

    public ReactorAgent(
        ReactorClient client,
        ILogger<ReactorAgent> logger,
        IReactorStepPrompter? prompter = null)
    {
        _client = client;
        _logger = logger;
        _prompter = prompter ?? new ConsoleStepPrompter();
    }

    public async Task<ReactorAgentResult> RunAsync(CancellationToken cancellationToken = default)
    {
        if (!await RunSetupStepAsync("reset", cancellationToken))
            return AbortedResult([]);

        if (!await RunSetupStepAsync("start", cancellationToken))
            return AbortedResult(["reset"]);

        var startResponse = _lastSetupResponse!;
        var state = ReactorState.FromDocument(startResponse);
        LogState("START", state);

        var executedCommands = new List<string> { "start" };
        JsonDocument? lastResponse = startResponse;

        var startFlag = ExtractFlag(state, lastResponse);
        if (startFlag is not null)
            return SuccessResult(executedCommands, state, lastResponse, startFlag, "start");

        _logger.LogInformation("Szukam bezpiecznej ścieżki (BFS)...");
        var plannedCommands = ReactorSolver.FindPath(state);
        _logger.LogInformation(
            "Znaleziono plan: {Steps} kroków → {Plan}",
            plannedCommands.Count,
            string.Join(" → ", plannedCommands));

        Console.WriteLine();
        Console.WriteLine("=== Plan agenta ===");
        for (var i = 0; i < plannedCommands.Count; i++)
            Console.WriteLine($"  {i + 1}. {plannedCommands[i]}");
        Console.WriteLine();

        for (var step = 0; step < plannedCommands.Count; step++)
        {
            var command = plannedCommands[step];
            var predicted = ReactorSimulator.Apply(state, command);

            if (!_prompter.ConfirmMoveStep(step + 1, plannedCommands.Count, command, state, predicted))
            {
                _logger.LogWarning("Gra przerwana przez użytkownika przed krokiem {Step}: {Command}", step + 1, command);
                return AbortedResult(executedCommands, state, lastResponse);
            }

            _logger.LogInformation(
                "Krok {Step}/{Total}: wysyłam '{Command}' (robot col={Col})",
                step + 1,
                plannedCommands.Count,
                command,
                state.PlayerCol);

            lastResponse = await _client.SendCommandAsync(command, cancellationToken);
            state = ReactorState.FromDocument(lastResponse);
            executedCommands.Add(command);
            LogState($"PO '{command.ToUpperInvariant()}'", state);

            if (state.IsCrushed || state.Code < 0)
            {
                _logger.LogError("Robot zgnieciony po komendzie '{Command}': {Message}", command, state.Message);
                throw new InvalidOperationException($"Robot zgnieciony po komendzie '{command}': {state.Message}");
            }

            var flag = ExtractFlag(state, lastResponse);
            if (flag is not null)
            {
                _logger.LogInformation("Otrzymano flagę po komendzie '{Command}': {Flag}", command, flag);
                return SuccessResult(executedCommands, state, lastResponse, flag, command);
            }

            if (state.ReachedGoal || state.PlayerCol == state.GoalCol)
            {
                _logger.LogInformation("Cel osiągnięty po komendzie '{Command}'", command);
                break;
            }
        }

        var finalFlag = ExtractFlag(state, lastResponse);
        if (finalFlag is not null)
        {
            _logger.LogInformation("Otrzymano flagę: {Flag}", finalFlag);
            return SuccessResult(executedCommands, state, lastResponse, finalFlag);
        }

        if (state.PlayerCol != state.GoalCol)
        {
            _logger.LogError(
                "Plan wykonany, ale robot nie dotarł do celu (col={Col}, goal={Goal})",
                state.PlayerCol,
                state.GoalCol);
            throw new InvalidOperationException("Robot dotarł na koniec ścieżki, ale cel nie został osiągnięty.");
        }

        return SuccessResult(executedCommands, state, lastResponse, finalFlag);
    }

    private ReactorAgentResult SuccessResult(
        IReadOnlyList<string> commands,
        ReactorState state,
        JsonDocument? lastResponse,
        string? flag = null,
        string? completedAfterCommand = null)
    {
        if (flag is not null)
        {
            _logger.LogInformation(
                "Zadanie zaliczone. Flaga: {Flag}. Wykonano {Count} komend: {Commands}",
                flag,
                commands.Count,
                string.Join(" → ", commands));

            Console.WriteLine();
            Console.WriteLine("=== ZADANIE ZALICZONE ===");
            if (completedAfterCommand is not null)
                Console.WriteLine($"Flaga otrzymana po komendzie '{completedAfterCommand}': {flag}");
            else
                Console.WriteLine($"Flaga: {flag}");
        }
        else
        {
            _logger.LogInformation(
                "Sukces. Wykonano {Count} komend: {Commands}",
                commands.Count,
                string.Join(" → ", commands));
        }

        return new ReactorAgentResult
        {
            Commands = commands,
            FinalState = state,
            LastResponse = lastResponse,
            Aborted = false,
            Flag = flag,
        };
    }

    private async Task<bool> RunSetupStepAsync(string stepName, CancellationToken cancellationToken)
    {
        if (!_prompter.ConfirmSetupStep(stepName))
        {
            _logger.LogWarning("Gra przerwana przez użytkownika przed krokiem: {Step}", stepName);
            return false;
        }

        _logger.LogInformation("Wysyłam komendę: {Step}", stepName);
        _lastSetupResponse = await _client.SendCommandAsync(stepName, cancellationToken);
        return true;
    }

    private static ReactorAgentResult AbortedResult(
        IReadOnlyList<string> commands,
        ReactorState? state = null,
        JsonDocument? lastResponse = null)
    {
        return new ReactorAgentResult
        {
            Commands = commands,
            FinalState = state ?? new ReactorState(),
            LastResponse = lastResponse,
            Aborted = true,
        };
    }

    private void LogState(string label, ReactorState state)
    {
        _logger.LogInformation(
            "[{Label}] col={Col} row5={Row} blocks=[{Blocks}] reached={Reached} crushed={Crushed} msg={Message}",
            label,
            state.PlayerCol,
            state.FormatBottomRow(),
            state.FormatBlocks(),
            state.ReachedGoal,
            state.IsCrushed,
            state.Message ?? "-");
    }

    public static string? ExtractFlag(ReactorState state, JsonDocument? response)
    {
        if (!string.IsNullOrWhiteSpace(state.Flag))
            return state.Flag;

        if (!string.IsNullOrWhiteSpace(state.Message))
        {
            var match = FlagRegex.Match(state.Message);
            if (match.Success)
                return match.Value;
        }

        if (response is not null
            && response.RootElement.TryGetProperty("flag", out var flagEl)
            && flagEl.ValueKind == JsonValueKind.String)
        {
            return flagEl.GetString();
        }

        return null;
    }
}
