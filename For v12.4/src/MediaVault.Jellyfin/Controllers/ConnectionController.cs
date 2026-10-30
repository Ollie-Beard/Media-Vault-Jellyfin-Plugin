using System.Text.Json;
using MediaVault.Jellyfin.Configuration;
using Microsoft.AspNetCore.Mvc;

namespace MediaVault.Jellyfin.Controllers;

[ApiController]
[Route("MediaVault/Connection")]
public sealed class ConnectionController : ControllerBase
{
    private static readonly HttpClient HttpClient = new();

    [HttpPost("test-catalogue")]
    public async Task<ActionResult<ConnectionTestResult>> TestCatalogue(
        [FromBody] ConnectionTestRequest request,
        CancellationToken cancellationToken)
    {
        var source = request.Source?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(source))
        {
            return Ok(
                ConnectionTestResult.Failed(
                    "Catalogue source is empty."));
        }

        try
        {
            string json;

            // Match CatalogueStore's handling of local paths: check rooted
            // paths before attempting URI parsing so Windows paths such as
            // C:\path\catalogue.json are handled correctly.
            if (Path.IsPathRooted(source))
            {
                var fullPath = Path.GetFullPath(source);

                if (!System.IO.File.Exists(fullPath))
                {
                    return Ok(
                        ConnectionTestResult.Failed(
                            $"Catalogue file was not found: {fullPath}"));
                }

                json = await System.IO.File.ReadAllTextAsync(
                        fullPath,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            else if (Uri.TryCreate(
                         source,
                         UriKind.Absolute,
                         out var uri)
                     && uri.Scheme is "http" or "https")
            {
                json = await GetStringAsync(
                        uri,
                        request.TimeoutSeconds,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                return Ok(
                    ConnectionTestResult.Failed(
                        "Catalogue source must be an HTTP(S) URL or an existing server-local file path."));
            }

            using var document = JsonDocument.Parse(json);

            if (!document.RootElement.TryGetProperty(
                    "movies",
                    out var movies)
                || movies.ValueKind != JsonValueKind.Array)
            {
                return Ok(
                    ConnectionTestResult.Failed(
                        "Catalogue was reached, but its JSON does not contain a movies array."));
            }

            return Ok(
                ConnectionTestResult.Succeeded(
                    $"Catalogue connection successful. Found {movies.GetArrayLength()} movie entries."));
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Ok(
                ConnectionTestResult.Failed(
                    $"Catalogue connection failed: {ex.Message}"));
        }
    }

    [HttpPost("test-gphotosdl")]
    public async Task<ActionResult<ConnectionTestResult>> TestGPhotosDl(
        [FromBody] ConnectionTestRequest request,
        CancellationToken cancellationToken)
    {
        var source = request.Source?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(source))
        {
            return Ok(
                ConnectionTestResult.Failed(
                    "GPhotosDL server URL is empty."));
        }

        if (!Uri.TryCreate(
                source,
                UriKind.Absolute,
                out var baseUri)
            || baseUri.Scheme is not ("http" or "https"))
        {
            return Ok(
                ConnectionTestResult.Failed(
                    "GPhotosDL server URL must be an HTTP(S) URL."));
        }

        try
        {
            var rootUri = new Uri(
                baseUri.ToString().TrimEnd('/') + "/");

            using var timeout = CreateTimeoutToken(
                request.TimeoutSeconds,
                cancellationToken);

            var response = await HttpClient.GetAsync(
                    rootUri,
                    HttpCompletionOption.ResponseHeadersRead,
                    timeout.Token)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return Ok(
                    ConnectionTestResult.Failed(
                        $"GPhotosDL responded with HTTP {(int)response.StatusCode} ({response.ReasonPhrase})."));
            }

            return Ok(
                ConnectionTestResult.Succeeded(
                    "GPhotosDL connection successful."));
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Ok(
                ConnectionTestResult.Failed(
                    $"GPhotosDL connection failed: {ex.Message}"));
        }
    }

    private static async Task<string> GetStringAsync(
        Uri uri,
        int timeoutSeconds,
        CancellationToken cancellationToken)
    {
        using var timeout = CreateTimeoutToken(
            timeoutSeconds,
            cancellationToken);

        return await HttpClient
            .GetStringAsync(uri, timeout.Token)
            .ConfigureAwait(false);
    }

    private static CancellationTokenSource CreateTimeoutToken(
        int timeoutSeconds,
        CancellationToken cancellationToken)
    {
        var timeout = Math.Clamp(
            timeoutSeconds <= 0 ? 30 : timeoutSeconds,
            1,
            300);

        var linked =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken);

        linked.CancelAfter(TimeSpan.FromSeconds(timeout));

        return linked;
    }

    public sealed record ConnectionTestRequest(
        string? Source,
        int TimeoutSeconds = 30);

    public sealed record ConnectionTestResult(
        bool Success,
        string Message)
    {
        public static ConnectionTestResult Succeeded(string message) =>
            new(true, message);

        public static ConnectionTestResult Failed(string message) =>
            new(false, message);
    }
}

