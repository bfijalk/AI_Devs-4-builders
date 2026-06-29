using System.Text.Json;

namespace Zadanie_04._02;

public sealed record WindPowerApiResult(
    bool Success,
    int StatusCode,
    string Body,
    JsonDocument? Document = null)
{
    public static WindPowerApiResult FromResponse(int statusCode, string body)
    {
        JsonDocument? document = null;
        try
        {
            document = JsonDocument.Parse(body);
        }
        catch
        {
            // ignored
        }

        return new WindPowerApiResult(
            statusCode is >= 200 and < 300,
            statusCode,
            body,
            document);
    }

    public string ToJson()
    {
        if (Document is not null)
        {
            return JsonSerializer.Serialize(
                Document.RootElement,
                new JsonSerializerOptions { WriteIndented = true });
        }

        return Body;
    }

    public string? GetMessage()
    {
        if (Document?.RootElement.TryGetProperty("message", out var message) == true)
        {
            return message.GetString();
        }

        return null;
    }
}
