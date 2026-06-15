using System.Text.Json;

namespace Zadanie_03._03;

public sealed record ReactorBlock(int Col, int TopRow, int BottomRow, string Direction);

public sealed class ReactorState
{
    public int PlayerCol { get; init; } = 1;
    public int GoalCol { get; init; } = 7;
    public IReadOnlyList<ReactorBlock> Blocks { get; init; } = [];
    public bool ReachedGoal { get; init; }
    public bool IsCrushed { get; init; }
    public string? Flag { get; init; }
    public string? Message { get; init; }
    public int Code { get; init; }

    public static ReactorState FromJson(JsonElement root)
    {
        var blocks = new List<ReactorBlock>();
        if (root.TryGetProperty("blocks", out var blocksEl) && blocksEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var block in blocksEl.EnumerateArray())
            {
                blocks.Add(new ReactorBlock(
                    block.GetProperty("col").GetInt32(),
                    block.GetProperty("top_row").GetInt32(),
                    block.GetProperty("bottom_row").GetInt32(),
                    block.GetProperty("direction").GetString() ?? "up"));
            }
        }

        var playerCol = root.TryGetProperty("player", out var playerEl)
            ? playerEl.GetProperty("col").GetInt32()
            : 1;

        var goalCol = root.TryGetProperty("goal", out var goalEl)
            ? goalEl.GetProperty("col").GetInt32()
            : 7;

        var code = root.TryGetProperty("code", out var codeEl) ? codeEl.GetInt32() : 0;
        var message = root.TryGetProperty("message", out var msgEl) && msgEl.ValueKind == JsonValueKind.String
            ? msgEl.GetString()
            : null;
        var isCrushed = root.TryGetProperty("is_crushed", out var crushedFlag) && crushedFlag.GetBoolean();
        if (!isCrushed && code < 0 && message?.Contains("crushed", StringComparison.OrdinalIgnoreCase) == true)
            isCrushed = true;

        return new ReactorState
        {
            PlayerCol = playerCol,
            GoalCol = goalCol,
            Blocks = blocks.OrderBy(b => b.Col).ToList(),
            ReachedGoal = root.TryGetProperty("reached_goal", out var goalFlag) && goalFlag.GetBoolean(),
            IsCrushed = isCrushed,
            Flag = root.TryGetProperty("flag", out var flagEl) && flagEl.ValueKind == JsonValueKind.String
                ? flagEl.GetString()
                : null,
            Message = message,
            Code = code,
        };
    }

    public static ReactorState FromDocument(JsonDocument document)
        => FromJson(document.RootElement);

    public string FormatBottomRow()
    {
        var row = new char[7];
        Array.Fill(row, '.');
        row[GoalCol - 1] = 'G';

        foreach (var block in Blocks)
        {
            if (block.TopRow <= ReactorSimulator.PlayerRow && ReactorSimulator.PlayerRow <= block.BottomRow)
                row[block.Col - 1] = 'B';
        }

        row[PlayerCol - 1] = 'P';
        return new string(row);
    }

    public string FormatBlocks()
        => string.Join(", ", Blocks.Select(b => $"c{b.Col}[{b.TopRow}-{b.BottomRow} {b.Direction}]"));
}
