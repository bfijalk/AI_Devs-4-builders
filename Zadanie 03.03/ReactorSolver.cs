namespace Zadanie_03._03;

public static class ReactorSolver
{
    public static IReadOnlyList<string> FindPath(ReactorState initial)
    {
        var startKey = ReactorSimulator.StateKey(initial);
        var queue = new Queue<(ReactorState State, List<string> Path)>();
        var visited = new HashSet<string> { startKey };

        queue.Enqueue((initial, []));

        while (queue.Count > 0)
        {
            var (state, path) = queue.Dequeue();

            if (state.PlayerCol == state.GoalCol)
                return path;

            foreach (var command in ReactorSimulator.MoveCommands)
            {
                if (command == "left" && state.PlayerCol <= ReactorSimulator.MinCol)
                    continue;
                if (command == "right" && state.PlayerCol >= ReactorSimulator.MaxCol)
                    continue;

                var next = ReactorSimulator.Apply(state, command);
                if (next.IsCrushed)
                    continue;

                var key = ReactorSimulator.StateKey(next);
                if (!visited.Add(key))
                    continue;

                var nextPath = new List<string>(path) { command };
                queue.Enqueue((next, nextPath));
            }
        }

        throw new InvalidOperationException("Nie znaleziono bezpiecznej ścieżki do celu.");
    }
}
