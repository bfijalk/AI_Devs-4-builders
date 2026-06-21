namespace Zadanie_03._05;

public static class RoutePlanner
{
    private static readonly string[] Directions = ["up", "down", "left", "right"];
    private const double TreeFuelPenalty = 0.2;

    public static RoutePlan? FindBestRoute(
        TerrainMap map,
        IReadOnlyList<VehicleInfo> vehicles,
        double startingFuel,
        double startingFood)
    {
        var consumptionByVehicle = vehicles.ToDictionary(
            v => v.Name,
            v => v.Consumption,
            StringComparer.OrdinalIgnoreCase);

        RoutePlan? best = null;

        foreach (var vehicle in vehicles)
        {
            var plan = FindRoute(map, vehicle.Name, consumptionByVehicle, startingFuel, startingFood);
            if (plan is null)
                continue;

            if (best is null || plan.CompareTo(best) < 0)
                best = plan;
        }

        return best;
    }

    private static RoutePlan? FindRoute(
        TerrainMap map,
        string vehicle,
        IReadOnlyDictionary<string, VehicleConsumption> consumptionByVehicle,
        double startingFuel,
        double startingFood)
    {
        var startState = new SearchState(map.Start, Dismounted: vehicle == "walk");
        var startNode = new SearchNode(
            startState,
            Steps: 0,
            Fuel: startingFuel,
            Food: startingFood,
            Commands: []);

        var queue = new PriorityQueue<SearchNode, (int Steps, double ResourceCost)>();
        queue.Enqueue(startNode, (0, 0));

        var bestResources = new Dictionary<SearchState, (double Fuel, double Food)>();

        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            var travelMode = node.State.TravelMode(vehicle);

            if (node.State.Position == map.Goal)
            {
                return new RoutePlan(
                    vehicle,
                    [vehicle, .. node.Commands],
                    node.Steps,
                    node.Fuel,
                    node.Food);
            }

            if (!TryRegisterState(bestResources, node.State, node.Fuel, node.Food))
                continue;

            if (!node.State.Dismounted && !string.Equals(vehicle, "walk", StringComparison.OrdinalIgnoreCase))
            {
                EnqueueDismount(queue, node, vehicle);
            }

            foreach (var direction in Directions)
            {
                var nextPos = TerrainMap.Step(node.State.Position, direction);
                if (!map.CanEnter(nextPos, travelMode))
                    continue;

                var moveCost = GetMoveCost(map, nextPos, travelMode, consumptionByVehicle);
                var nextFuel = node.Fuel - moveCost.Fuel;
                var nextFood = node.Food - moveCost.Food;

                if (nextFuel < -1e-9 || nextFood < -1e-9)
                    continue;

                var nextNode = new SearchNode(
                    node.State with { Position = nextPos },
                    node.Steps + 1,
                    nextFuel,
                    nextFood,
                    [.. node.Commands, direction]);

                queue.Enqueue(
                    nextNode,
                    (nextNode.Steps, moveCost.Fuel + moveCost.Food));
            }
        }

        return null;
    }

    private static void EnqueueDismount(
        PriorityQueue<SearchNode, (int Steps, double ResourceCost)> queue,
        SearchNode node,
        string vehicle)
    {
        var nextNode = new SearchNode(
            node.State with { Dismounted = true },
            node.Steps,
            node.Fuel,
            node.Food,
            [.. node.Commands, "dismount"]);

        queue.Enqueue(nextNode, (nextNode.Steps, 0));
    }

    private static VehicleConsumption GetMoveCost(
        TerrainMap map,
        Position destination,
        string travelMode,
        IReadOnlyDictionary<string, VehicleConsumption> consumptionByVehicle)
    {
        if (!consumptionByVehicle.TryGetValue(travelMode, out var baseCost))
            throw new InvalidOperationException($"Unknown travel mode: {travelMode}");

        var fuel = baseCost.Fuel;
        if (map.IsTree(destination) && travelMode is "car" or "rocket")
            fuel += TreeFuelPenalty;

        return new VehicleConsumption(fuel, baseCost.Food);
    }

    private static bool TryRegisterState(
        Dictionary<SearchState, (double Fuel, double Food)> bestResources,
        SearchState state,
        double fuel,
        double food)
    {
        if (bestResources.TryGetValue(state, out var previous)
            && previous.Fuel >= fuel - 1e-9
            && previous.Food >= food - 1e-9)
        {
            return false;
        }

        bestResources[state] = (fuel, food);
        return true;
    }

    private readonly record struct SearchState(Position Position, bool Dismounted)
    {
        public string TravelMode(string vehicle) =>
            Dismounted || string.Equals(vehicle, "walk", StringComparison.OrdinalIgnoreCase)
                ? "walk"
                : vehicle;
    }

    private readonly record struct SearchNode(
        SearchState State,
        int Steps,
        double Fuel,
        double Food,
        List<string> Commands);
}

public record RoutePlan(
    string Vehicle,
    IReadOnlyList<string> Commands,
    int MoveCount,
    double RemainingFuel,
    double RemainingFood)
{
    public int CompareTo(RoutePlan other)
    {
        var stepCompare = MoveCount.CompareTo(other.MoveCount);
        if (stepCompare != 0)
            return stepCompare;

        var resourceCompare = TotalResourceUsed.CompareTo(other.TotalResourceUsed);
        if (resourceCompare != 0)
            return resourceCompare;

        return RemainingFuel.CompareTo(other.RemainingFuel);
    }

    public double TotalResourceUsed =>
        (10 - RemainingFuel) + (10 - RemainingFood);
}
