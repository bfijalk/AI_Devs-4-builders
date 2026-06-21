namespace Zadanie_03._05;

public sealed class TerrainMap
{
    private readonly string[][] _tiles;

    public int Rows => _tiles.Length;
    public int Cols => _tiles[0].Length;
    public Position Start { get; }
    public Position Goal { get; }
    public string CityName { get; }

    private TerrainMap(string[][] tiles, Position start, Position goal, string cityName)
    {
        _tiles = tiles;
        Start = start;
        Goal = goal;
        CityName = cityName;
    }

    public static TerrainMap FromResponse(TerrainMapResponse response)
    {
        var tiles = response.Map;
        var hasStart = false;
        var hasGoal = false;
        var start = default(Position);
        var goal = default(Position);

        for (var row = 0; row < tiles.Length; row++)
        {
            for (var col = 0; col < tiles[row].Length; col++)
            {
                switch (tiles[row][col])
                {
                    case "S":
                        start = new Position(row, col);
                        hasStart = true;
                        break;
                    case "G":
                        goal = new Position(row, col);
                        hasGoal = true;
                        break;
                }
            }
        }

        if (!hasStart || !hasGoal)
            throw new InvalidOperationException("Map is missing start (S) or goal (G) tile.");

        return new TerrainMap(tiles, start, goal, response.CityName);
    }

    public string TileAt(Position pos) => _tiles[pos.Row][pos.Col];

    public bool CanEnter(Position pos, string travelMode)
    {
        if (!IsInside(pos))
            return false;

        return TileAt(pos) switch
        {
            "R" => false,
            "W" when travelMode is "car" or "rocket" => false,
            _ => true,
        };
    }

    public bool IsTree(Position pos) => TileAt(pos) == "T";

    public bool IsInside(Position pos) =>
        pos.Row >= 0 && pos.Row < Rows && pos.Col >= 0 && pos.Col < Cols;

    public static Position Step(Position pos, string direction) => direction switch
    {
        "up" => pos with { Row = pos.Row - 1 },
        "down" => pos with { Row = pos.Row + 1 },
        "left" => pos with { Col = pos.Col - 1 },
        _ => pos with { Col = pos.Col + 1 },
    };
}

public readonly record struct Position(int Row, int Col);
