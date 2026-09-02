using Xunit;
namespace PlaywrightServerMcp.Tests;

/// <summary>
/// The Angular tools take a <c>workingDirectory</c> meaning "the user's Angular project". Under
/// stdio an omitted one fell back to <see cref="Directory.GetCurrentDirectory"/>, which was the
/// client's directory. Behind the gateway that is a VERSIONED DEPLOY DIRECTORY, so the fallback
/// silently pointed every tool at the server's own install. There is no sensible default here, so
/// the tools refuse.
/// </summary>
public class AngularProjectDirectoryTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void Refuses_a_blank_working_directory(string workingDirectory)
    {
        bool accepted = AngularProjectDirectory.TryValidate(workingDirectory, out string refusal);

        Assert.False(accepted);
        Assert.Contains("workingDirectory", refusal);
        Assert.Contains("absolute", refusal, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("my-app")]
    [InlineData("./my-app")]
    [InlineData(@"..\my-app")]
    [InlineData(@"src\app")]
    public void Refuses_a_relative_working_directory(string workingDirectory)
    {
        bool accepted = AngularProjectDirectory.TryValidate(workingDirectory, out string refusal);

        Assert.False(accepted);
        Assert.Contains(workingDirectory, refusal);
        Assert.Contains("absolute", refusal, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Refuses_an_absolute_directory_that_does_not_exist()
    {
        string missing = Path.Combine(Path.GetTempPath(), $"angular-{Guid.NewGuid():N}");

        bool accepted = AngularProjectDirectory.TryValidate(missing, out string refusal);

        Assert.False(accepted);
        Assert.Contains(missing, refusal);
        Assert.Contains("does not exist", refusal, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Accepts_an_existing_absolute_directory()
    {
        string existing = Path.Combine(Path.GetTempPath(), $"angular-{Guid.NewGuid():N}");
        Directory.CreateDirectory(existing);

        try
        {
            bool accepted = AngularProjectDirectory.TryValidate(existing, out string refusal);

            Assert.True(accepted);
            Assert.Empty(refusal);
        }
        finally
        {
            Directory.Delete(existing);
        }
    }

    /// <summary>
    /// The exact regression: the old fallback made a blank directory mean "wherever the server
    /// happens to be running", which always exists and so would always be accepted.
    /// </summary>
    [Fact]
    public void Does_not_fall_back_to_the_process_current_directory()
    {
        Assert.False(AngularProjectDirectory.TryValidate("", out string refusal));
        Assert.DoesNotContain(Directory.GetCurrentDirectory(), refusal);
    }
}
