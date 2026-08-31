using McpGateway.Security;

namespace McpGateway.Supervision;

public sealed class HealthProbe(HttpClient client, BackendToken backendToken)
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
                // /health is authenticated like every other backend endpoint, so the probe has to
                // present the backend token or the gate can never open.
                using var request = new HttpRequestMessage(HttpMethod.Get, uri);
                request.Headers.Add("Authorization", $"Bearer {backendToken.Value}");

                using HttpResponseMessage response = await client.SendAsync(
                    request, cancellationToken);

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
