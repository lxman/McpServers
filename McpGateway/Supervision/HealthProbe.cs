namespace McpGateway.Supervision;

public sealed class HealthProbe(HttpClient client)
{
    public async Task<bool> WaitUntilHealthyAsync(
        int port, TimeSpan timeout, CancellationToken cancellationToken)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow + timeout;
        var uri = new Uri($"http://127.0.0.1:{port}/health");

        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                using HttpResponseMessage response = await client.GetAsync(uri, cancellationToken);
                if (response.IsSuccessStatusCode) return true;
            }
            catch (HttpRequestException)
            {
                // Not listening yet.
            }

            await Task.Delay(100, cancellationToken);
        }

        return false;
    }
}
