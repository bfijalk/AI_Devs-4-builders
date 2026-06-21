using System.Net;
using System.Text;

namespace Zadanie_03._05;

public static class TerrainMapVisualizer
{
    private static readonly (string Tile, string Label, string Description)[] Legend =
    [
        (".", "Grass", "Empty passable terrain"),
        ("T", "Tree", "Tree tile (+0.2 fuel on powered vehicles)"),
        ("W", "Water", "River or lake"),
        ("R", "Rock", "Blocks movement"),
        ("S", "Start", "Mission starting position"),
        ("G", "Goal", "Destination — reach to win"),
    ];

    private static readonly (char Symbol, string Ansi) UnknownConsoleStyle = ('?', string.Empty);
    private static readonly (string Label, string Color) UnknownHtmlStyle = ("?", "#374151");

    private static readonly Dictionary<string, (char Symbol, string Ansi)> ConsoleStyles = new()
    {
        ["."] = ('·', "\x1b[32m"),
        ["T"] = ('♣', "\x1b[33m"),
        ["W"] = ('~', "\x1b[34m"),
        ["R"] = ('#', "\x1b[90m"),
        ["S"] = ('S', "\x1b[92m\x1b[1m"),
        ["G"] = ('G', "\x1b[91m\x1b[1m"),
    };

    private static readonly Dictionary<string, (string Label, string Color)> HtmlStyles = new()
    {
        ["."] = ("Grass", "#3d7a37"),
        ["T"] = ("Tree", "#1f5f2a"),
        ["W"] = ("Water", "#2b6cb0"),
        ["R"] = ("Rock", "#6b7280"),
        ["S"] = ("Start", "#22c55e"),
        ["G"] = ("Goal", "#ef4444"),
    };

    public static string RenderToConsole(TerrainMapResponse map, bool useColors = true)
        => RenderBriefingToConsole(new GameBriefing(
            map.CityName, 10, 10, map, [], [], []), useColors, mapOnly: true);

    public static string RenderBriefingToConsole(
        GameBriefing briefing,
        bool useColors = true,
        bool mapOnly = false)
    {
        var sb = new StringBuilder();
        sb.AppendLine(RenderMapGrid(briefing.Map, useColors));

        if (mapOnly)
            return sb.ToString();

        sb.AppendLine();
        sb.AppendLine("=== MISSION BRIEFING ===");
        sb.AppendLine($"Destination : {briefing.DestinationCity}");
        sb.AppendLine($"Resources   : {briefing.StartingFood} food, {briefing.StartingFuel} fuel");
        sb.AppendLine("Objective   : Reach tile G without running out of food or fuel.");
        sb.AppendLine();

        sb.AppendLine("=== VEHICLES ===");
        foreach (var vehicle in briefing.Vehicles)
        {
            sb.AppendLine($"  {vehicle.Name.ToUpper(),-7}  fuel {vehicle.Consumption.Fuel,4:0.0}/move  food {vehicle.Consumption.Food,4:0.0}/move");
            sb.AppendLine($"           {vehicle.Note}");
            sb.AppendLine();
        }

        sb.AppendLine("=== MOVEMENT RULES ===");
        foreach (var note in briefing.MovementNotes)
        {
            sb.AppendLine($"  [{note.Id}] {note.Title}");
            sb.AppendLine($"  {note.Content}");
            sb.AppendLine();
        }

        sb.AppendLine("=== COMMANDS ===");
        sb.AppendLine("  Directions : up, down, left, right");
        sb.AppendLine("  Vehicles   : walk, horse, car, rocket  (choose at departure)");
        sb.AppendLine("  Special    : dismount  (leave vehicle, continue on foot)");

        return sb.ToString();
    }

    public static string RenderToHtml(TerrainMapResponse map)
        => RenderBriefingToHtml(new GameBriefing(
            map.CityName, 10, 10, map, [], [], []));

    public static string RenderBriefingToHtml(GameBriefing briefing)
    {
        var map = briefing.Map;
        var rows = map.Map.Length;
        var cols = map.Map.FirstOrDefault()?.Length ?? 0;

        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("  <meta charset=\"utf-8\">");
        sb.AppendLine($"  <title>Mission briefing: {map.CityName}</title>");
        sb.AppendLine("  <style>");
        sb.AppendLine("    * { box-sizing: border-box; }");
        sb.AppendLine("    body { font-family: system-ui, sans-serif; background: #0f172a; color: #e2e8f0; margin: 0; padding: 1.5rem; }");
        sb.AppendLine("    h1 { margin: 0 0 0.25rem; font-size: 1.75rem; }");
        sb.AppendLine("    h2 { margin: 0 0 0.75rem; font-size: 1rem; color: #93c5fd; text-transform: uppercase; letter-spacing: 0.05em; }");
        sb.AppendLine("    .meta { color: #94a3b8; margin-bottom: 1.25rem; }");
        sb.AppendLine("    .layout { display: flex; gap: 1.5rem; align-items: flex-start; flex-wrap: wrap; }");
        sb.AppendLine("    .map-panel { flex: 0 0 auto; }");
        sb.AppendLine("    .sidebar { flex: 1 1 420px; min-width: 320px; display: flex; flex-direction: column; gap: 1rem; }");
        sb.AppendLine("    .card { background: #1e293b; border: 1px solid #334155; border-radius: 10px; padding: 1rem 1.1rem; }");
        sb.AppendLine("    .stats { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 0.75rem; }");
        sb.AppendLine("    .stat { background: #0f172a; border-radius: 8px; padding: 0.75rem; }");
        sb.AppendLine("    .stat-label { color: #94a3b8; font-size: 0.8rem; }");
        sb.AppendLine("    .stat-value { font-size: 1.25rem; font-weight: 700; margin-top: 0.15rem; }");
        sb.AppendLine("    .grid { display: inline-grid; gap: 2px; background: #334155; padding: 2px; border-radius: 8px; }");
        sb.AppendLine("    .cell { width: 38px; height: 38px; display: flex; align-items: center; justify-content: center; font-weight: 700; border-radius: 4px; font-size: 0.85rem; }");
        sb.AppendLine("    .header { background: transparent; color: #94a3b8; font-size: 11px; font-weight: 600; }");
        sb.AppendLine("    table { width: 100%; border-collapse: collapse; font-size: 0.92rem; }");
        sb.AppendLine("    th, td { text-align: left; padding: 0.45rem 0.35rem; border-bottom: 1px solid #334155; vertical-align: top; }");
        sb.AppendLine("    th { color: #94a3b8; font-weight: 600; font-size: 0.8rem; text-transform: uppercase; }");
        sb.AppendLine("    .vehicle-name { font-weight: 700; text-transform: capitalize; color: #fbbf24; white-space: nowrap; }");
        sb.AppendLine("    .note { color: #cbd5e1; font-size: 0.88rem; line-height: 1.45; margin: 0; }");
        sb.AppendLine("    .note-title { font-weight: 700; color: #f8fafc; margin-bottom: 0.25rem; }");
        sb.AppendLine("    .note-block + .note-block { margin-top: 0.85rem; padding-top: 0.85rem; border-top: 1px solid #334155; }");
        sb.AppendLine("    .legend-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 0.5rem; }");
        sb.AppendLine("    .legend-item { display: flex; align-items: center; gap: 0.6rem; background: #0f172a; padding: 0.45rem 0.55rem; border-radius: 6px; font-size: 0.85rem; }");
        sb.AppendLine("    .swatch { width: 24px; height: 24px; border-radius: 4px; display: flex; align-items: center; justify-content: center; font-weight: 700; flex-shrink: 0; }");
        sb.AppendLine("    .commands code { background: #0f172a; padding: 0.1rem 0.35rem; border-radius: 4px; color: #fde68a; }");
        sb.AppendLine("  </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine($"  <h1>{map.CityName}</h1>");
        sb.AppendLine($"  <div class=\"meta\">Mission briefing · {rows}x{cols} terrain map · reach the goal tile <strong>G</strong></div>");
        sb.AppendLine("  <div class=\"layout\">");
        sb.AppendLine("    <div class=\"map-panel card\">");
        sb.AppendLine("      <h2>Map</h2>");
        AppendMapGridHtml(sb, map);
        sb.AppendLine("    </div>");
        sb.AppendLine("    <div class=\"sidebar\">");

        sb.AppendLine("      <div class=\"card\">");
        sb.AppendLine("        <h2>Mission</h2>");
        sb.AppendLine("        <div class=\"stats\">");
        sb.AppendLine($"          <div class=\"stat\"><div class=\"stat-label\">Destination</div><div class=\"stat-value\">{WebUtility.HtmlEncode(briefing.DestinationCity)}</div></div>");
        sb.AppendLine($"          <div class=\"stat\"><div class=\"stat-label\">Starting food</div><div class=\"stat-value\">{briefing.StartingFood}</div></div>");
        sb.AppendLine($"          <div class=\"stat\"><div class=\"stat-label\">Starting fuel</div><div class=\"stat-value\">{briefing.StartingFuel}</div></div>");
        sb.AppendLine("          <div class=\"stat\"><div class=\"stat-label\">Map size</div><div class=\"stat-value\">10 × 10</div></div>");
        sb.AppendLine("        </div>");
        sb.AppendLine("      </div>");

        sb.AppendLine("      <div class=\"card\">");
        sb.AppendLine("        <h2>Vehicles</h2>");
        sb.AppendLine("        <table>");
        sb.AppendLine("          <thead><tr><th>Mode</th><th>Fuel/move</th><th>Food/move</th></tr></thead>");
        sb.AppendLine("          <tbody>");
        foreach (var vehicle in briefing.Vehicles)
        {
            sb.AppendLine("            <tr>");
            sb.AppendLine($"              <td class=\"vehicle-name\">{WebUtility.HtmlEncode(vehicle.Name)}</td>");
            sb.AppendLine($"              <td>{vehicle.Consumption.Fuel:0.0}</td>");
            sb.AppendLine($"              <td>{vehicle.Consumption.Food:0.0}</td>");
            sb.AppendLine("            </tr>");
        }
        sb.AppendLine("          </tbody>");
        sb.AppendLine("        </table>");
        foreach (var vehicle in briefing.Vehicles)
        {
            sb.AppendLine($"        <p class=\"note\" style=\"margin-top:0.75rem;\"><span class=\"vehicle-name\">{WebUtility.HtmlEncode(vehicle.Name)}:</span> {WebUtility.HtmlEncode(vehicle.Note)}</p>");
        }
        sb.AppendLine("      </div>");

        sb.AppendLine("      <div class=\"card\">");
        sb.AppendLine("        <h2>Movement rules</h2>");
        foreach (var note in briefing.MovementNotes)
        {
            sb.AppendLine("        <div class=\"note-block\">");
            sb.AppendLine($"          <div class=\"note-title\">{WebUtility.HtmlEncode(note.Title)}</div>");
            sb.AppendLine($"          <p class=\"note\">{WebUtility.HtmlEncode(note.Content)}</p>");
            sb.AppendLine("        </div>");
        }
        sb.AppendLine("      </div>");

        sb.AppendLine("      <div class=\"card\">");
        sb.AppendLine("        <h2>Map legend</h2>");
        sb.AppendLine("        <div class=\"legend-grid\">");
        foreach (var (tile, label, description) in Legend)
        {
            var style = HtmlStyles.GetValueOrDefault(tile, UnknownHtmlStyle);
            sb.AppendLine("          <div class=\"legend-item\">");
            sb.AppendLine($"            <div class=\"swatch\" style=\"background:{style.Color};\">{tile}</div>");
            sb.AppendLine($"            <div><strong>{label}</strong><br><span style=\"color:#94a3b8;\">{description}</span></div>");
            sb.AppendLine("          </div>");
        }
        sb.AppendLine("        </div>");
        sb.AppendLine("      </div>");

        sb.AppendLine("      <div class=\"card commands\">");
        sb.AppendLine("        <h2>Commands</h2>");
        sb.AppendLine("        <p class=\"note\">Directions: <code>up</code> <code>down</code> <code>left</code> <code>right</code></p>");
        sb.AppendLine("        <p class=\"note\">Vehicles (choose at departure): <code>walk</code> <code>horse</code> <code>car</code> <code>rocket</code></p>");
        sb.AppendLine("        <p class=\"note\">Special: <code>dismount</code> — leave vehicle and continue on foot.</p>");
        sb.AppendLine("        <p class=\"note\">North is at the top of the map. No refueling on the route.</p>");
        sb.AppendLine("      </div>");

        sb.AppendLine("    </div>");
        sb.AppendLine("  </div>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }

    public static async Task<string> SaveHtmlAsync(TerrainMapResponse map, string? outputPath = null)
    {
        outputPath ??= Path.Combine("files", $"{map.CityName.ToLowerInvariant()}_map.html");
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        await File.WriteAllTextAsync(outputPath, RenderToHtml(map));
        return Path.GetFullPath(outputPath);
    }

    public static async Task<string> SaveBriefingHtmlAsync(
        GameBriefing briefing,
        string? outputPath = null)
    {
        outputPath ??= Path.Combine("files", $"{briefing.Map.CityName.ToLowerInvariant()}_briefing.html");
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        await File.WriteAllTextAsync(outputPath, RenderBriefingToHtml(briefing));
        return Path.GetFullPath(outputPath);
    }

    private static string RenderMapGrid(TerrainMapResponse map, bool useColors)
    {
        var rows = map.Map.Length;
        var cols = map.Map.FirstOrDefault()?.Length ?? 0;
        var sb = new StringBuilder();

        sb.AppendLine($"=== {map.CityName} ({rows}x{cols}) ===");
        sb.AppendLine();

        sb.Append("     ");
        for (var col = 0; col < cols; col++)
            sb.Append($" {col,2}");
        sb.AppendLine();

        for (var row = 0; row < rows; row++)
        {
            sb.Append($"{row,3}  ");
            foreach (var tile in map.Map[row])
            {
                var style = ConsoleStyles.GetValueOrDefault(tile, UnknownConsoleStyle);
                if (useColors && style.Ansi.Length > 0)
                    sb.Append($"\x1b[0m{style.Ansi} {style.Symbol} \x1b[0m");
                else
                    sb.Append($" {style.Symbol} ");
            }
            sb.AppendLine();
        }

        sb.AppendLine();
        sb.AppendLine("Legend:");
        foreach (var (tile, label, description) in Legend)
        {
            var style = ConsoleStyles.GetValueOrDefault(tile, UnknownConsoleStyle);
            sb.AppendLine($"  {style.Symbol}  {label,-5}  {description}  (API: '{tile}')");
        }

        return sb.ToString();
    }

    private static void AppendMapGridHtml(StringBuilder sb, TerrainMapResponse map)
    {
        var rows = map.Map.Length;
        var cols = map.Map.FirstOrDefault()?.Length ?? 0;

        sb.Append("      <div class=\"grid\" style=\"grid-template-columns: repeat(")
            .Append(cols + 1)
            .AppendLine(", auto);\">");

        sb.AppendLine("        <div class=\"cell header\"></div>");
        for (var col = 0; col < cols; col++)
            sb.AppendLine($"        <div class=\"cell header\">{col}</div>");

        for (var row = 0; row < rows; row++)
        {
            sb.AppendLine($"        <div class=\"cell header\">{row}</div>");
            foreach (var tile in map.Map[row])
            {
                var style = HtmlStyles.GetValueOrDefault(tile, UnknownHtmlStyle);
                sb.AppendLine(
                    $"        <div class=\"cell\" title=\"{style.Label}\" style=\"background:{style.Color}; color:#fff;\">{tile}</div>");
            }
        }

        sb.AppendLine("      </div>");
    }
}
