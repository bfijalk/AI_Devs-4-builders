using System.Text;
using System.Text.Json;

namespace Zadanie_04._01;

public static class OkoEditorApiDocumenter
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public static async Task<string> DocumentAsync(
        OkoEditorClient client,
        string outputDir = "files",
        CancellationToken cancellationToken = default)
    {
        using var helpResponse = await client.GetHelpAsync(cancellationToken);
        var root = helpResponse.RootElement;

        Directory.CreateDirectory(outputDir);

        var markdownPath = Path.Combine(outputDir, "okoeditor_api.md");
        var jsonPath = Path.Combine(outputDir, "okoeditor_api_help.json");

        var markdown = BuildMarkdown(root);
        var rawJson = JsonSerializer.Serialize(root, JsonOpts);

        await File.WriteAllTextAsync(markdownPath, markdown, cancellationToken);
        await File.WriteAllTextAsync(jsonPath, rawJson, cancellationToken);

        return markdownPath;
    }

    private static string BuildMarkdown(JsonElement root)
    {
        var sb = new StringBuilder();

        sb.AppendLine("# OKO Editor API");
        sb.AppendLine();
        sb.AppendLine("Endpoint: `POST https://hub.ag3nts.org/verify`");
        sb.AppendLine();
        sb.AppendLine("Task: `okoeditor`");
        sb.AppendLine();

        if (root.TryGetProperty("description", out var description))
        {
            sb.AppendLine(description.GetString());
            sb.AppendLine();
        }

        if (root.TryGetProperty("commands", out var commands) &&
            commands.ValueKind == JsonValueKind.Array)
        {
            foreach (var command in commands.EnumerateArray())
            {
                AppendCommandSection(sb, command);
            }
        }

        return sb.ToString().TrimEnd() + Environment.NewLine;
    }

    private static void AppendCommandSection(StringBuilder sb, JsonElement command)
    {
        var action = command.TryGetProperty("action", out var actionProp)
            ? actionProp.GetString() ?? "unknown"
            : "unknown";

        sb.AppendLine($"## `{action}`");
        sb.AppendLine();

        if (command.TryGetProperty("syntax", out var syntax))
        {
            sb.AppendLine("### Składnia żądania");
            sb.AppendLine();
            sb.AppendLine("```json");
            sb.AppendLine(JsonSerializer.Serialize(syntax, JsonOpts));
            sb.AppendLine("```");
            sb.AppendLine();
        }

        AppendStringListSection(sb, "Pola wymagane", "required_fields", command);
        AppendStringListSection(sb, "Pola opcjonalne", "optional_fields", command);
        AppendStringListSection(sb, "Zasady", "rules", command);
        AppendStringListSection(sb, "Uwagi", "notes", command);
    }

    private static void AppendStringListSection(
        StringBuilder sb,
        string title,
        string propertyName,
        JsonElement command)
    {
        if (!command.TryGetProperty(propertyName, out var items) ||
            items.ValueKind != JsonValueKind.Array ||
            items.GetArrayLength() == 0)
        {
            return;
        }

        sb.AppendLine($"### {title}");
        sb.AppendLine();

        foreach (var item in items.EnumerateArray())
        {
            sb.AppendLine($"- {item.GetString()}");
        }

        sb.AppendLine();
    }
}
