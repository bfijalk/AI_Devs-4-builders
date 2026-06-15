namespace Zadanie_03._03;

public static class ReactorSimulator
{
    public const int MinCol = 1;
    public const int MaxCol = 7;
    public const int PlayerRow = 5;

    public static readonly string[] MoveCommands = ["wait", "left", "right"];

    public static ReactorState Apply(ReactorState state, string command)
    {
        var blocks = state.Blocks.Select(MoveBlock).OrderBy(b => b.Col).ToList();
        var playerCol = state.PlayerCol;

        if (command == "right")
            playerCol = Math.Min(MaxCol, playerCol + 1);
        else if (command == "left")
            playerCol = Math.Max(MinCol, playerCol - 1);

        var crushed = IsCrushed(blocks, playerCol);

        return new ReactorState
        {
            PlayerCol = playerCol,
            GoalCol = state.GoalCol,
            Blocks = blocks,
            ReachedGoal = !crushed && playerCol == state.GoalCol,
            IsCrushed = crushed,
        };
    }

    public static bool IsCrushed(IReadOnlyList<ReactorBlock> blocks, int playerCol)
        => blocks.Any(b => b.Col == playerCol && b.TopRow <= PlayerRow && PlayerRow <= b.BottomRow);

    private static ReactorBlock MoveBlock(ReactorBlock block)
    {
        var top = block.TopRow;
        var bottom = block.BottomRow;
        var direction = block.Direction;

        if (direction == "up")
        {
            top--;
            bottom--;
            if (top <= 1)
                (top, bottom, direction) = (1, 2, "down");
        }
        else
        {
            top++;
            bottom++;
            if (bottom >= 5)
                (top, bottom, direction) = (4, 5, "up");
        }

        return block with { TopRow = top, BottomRow = bottom, Direction = direction };
    }

    public static string StateKey(ReactorState state)
    {
        var blocks = string.Join('|', state.Blocks.Select(b =>
            $"{b.Col}:{b.TopRow}:{b.BottomRow}:{b.Direction}"));
        return $"{state.PlayerCol}#{blocks}";
    }
}
