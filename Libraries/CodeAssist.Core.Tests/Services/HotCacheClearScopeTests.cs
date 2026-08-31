using System.Reflection;
using Xunit;

namespace CodeAssist.Core.Tests.Services;

/// <summary>
/// set_active_repository wipes other repositories' caches. Harmless when every session had its own
/// process; destructive once CodeAssist is one shared instance. The default must be opt-in.
/// </summary>
public sealed class HotCacheClearScopeTests
{
    [Fact]
    public void SetActiveRepository_DoesNotClearOtherCachesByDefault()
    {
        Type toolsType = Assembly.Load("CodeAssistMcp")
            .GetType("CodeAssistMcp.McpTools.RepositoryTools")
            ?? throw new InvalidOperationException("RepositoryTools not found.");

        MethodInfo method = toolsType.GetMethod("SetActiveRepository")
            ?? throw new InvalidOperationException("SetActiveRepository not found.");

        ParameterInfo parameter = method.GetParameters()
            .Single(p => p.Name == "clearOtherCaches");

        Assert.True(parameter.HasDefaultValue);
        Assert.Equal(false, parameter.DefaultValue);
    }
}
