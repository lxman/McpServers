using Xunit;

namespace CodeAssist.Core.Tests.Integration;

/// <summary>
/// A fact that runs only when CODEASSIST_TEST_QDRANT_URL is set. These tests need a real Qdrant and a
/// real embedding server; they are the only way to catch the duplication bug end to end, because the
/// defect lives in the interaction between two writers rather than inside either one.
/// </summary>
public sealed class RequiresLiveServicesFactAttribute : FactAttribute
{
    public RequiresLiveServicesFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CODEASSIST_TEST_QDRANT_URL")))
        {
            Skip = "Set CODEASSIST_TEST_QDRANT_URL and CODEASSIST_TEST_OLLAMA_URL to run live-service tests.";
        }
    }
}
