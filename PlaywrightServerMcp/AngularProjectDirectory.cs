namespace PlaywrightServerMcp;

/// <summary>
/// Validates the <c>workingDirectory</c> the Angular tools are pointed at.
/// <para>
/// That parameter means "the user's Angular project". Under stdio an omitted one fell back to
/// <see cref="Directory.GetCurrentDirectory"/>, which was the client's own directory and so was
/// usually right by accident. Behind the gateway the process runs from a VERSIONED DEPLOY
/// DIRECTORY, so the same fallback silently aimed every tool at the server's install -- reporting
/// on it, building in it, and in the case of <c>ng generate</c> writing into it.
/// </para>
/// <para>
/// There is no defensible default, so there isn't one: the tools refuse and say what to pass.
/// Contrast <see cref="OutputPaths"/>, which is about where OUR artefacts go and does have one.
/// </para>
/// </summary>
public static class AngularProjectDirectory
{
    private const string ExamplePath = """C:\Users\you\src\my-app""";

    /// <summary>Shown on every tool's <c>workingDirectory</c> parameter in the MCP schema.</summary>
    public const string ParameterDescription =
        "Absolute path to the Angular project root, e.g. " + ExamplePath + ". Required: this "
        + "server runs as a shared HTTP backend and has no working directory of your own to fall "
        + "back on.";

    private const string Guidance =
        "Pass the absolute path to the Angular project root, e.g. " + ExamplePath + ".";

    /// <summary>
    /// True when <paramref name="workingDirectory"/> is an existing absolute path. Otherwise false,
    /// with <paramref name="refusal"/> set to a message naming the parameter and what to pass.
    /// </summary>
    public static bool TryValidate(string workingDirectory, out string refusal)
    {
        if (string.IsNullOrWhiteSpace(workingDirectory))
        {
            refusal = "workingDirectory is required. This server runs as a shared HTTP backend, so "
                      + "there is no \"current directory\" of yours for it to assume -- an omitted "
                      + "path would resolve to the server's own deploy directory, which is never "
                      + "your project. " + Guidance;
            return false;
        }

        if (!Path.IsPathFullyQualified(workingDirectory))
        {
            refusal = $"workingDirectory '{workingDirectory}' is relative, and would resolve "
                      + "against the server's deploy directory rather than your project. " + Guidance;
            return false;
        }

        if (!Directory.Exists(workingDirectory))
        {
            refusal = $"workingDirectory '{workingDirectory}' does not exist. " + Guidance;
            return false;
        }

        refusal = "";
        return true;
    }
}
