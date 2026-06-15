namespace Zadanie_03._03;

public static class PageDownloader
{
    private const string ReactorPreviewUrl = "https://hub.ag3nts.org/reactor_preview.html";

    public static async Task<string> DownloadReactorPreviewAsync(
        string outputDir = "files",
        CancellationToken cancellationToken = default)
    {
        using var http = new HttpClient();
        var content = await http.GetStringAsync(ReactorPreviewUrl, cancellationToken);

        Directory.CreateDirectory(outputDir);
        var outputPath = Path.Combine(outputDir, "reactor_preview.html");
        await File.WriteAllTextAsync(outputPath, content, cancellationToken);

        return outputPath;
    }
}
