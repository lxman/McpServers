namespace PlaywrightServerMcp;

/// <summary>
/// Where this server writes the artefacts it produces: screenshots, PDFs and downloads.
/// <para>
/// These were built from <see cref="Directory.GetCurrentDirectory"/>. Under stdio that was the
/// client's working directory; under the gateway it is a VERSIONED deploy directory, so every
/// deploy would start writing somewhere new and orphan everything produced before it. Same class of
/// bug as edgar's "./data" and selenium's "Screenshots".
/// </para>
/// <para>
/// This is deliberately NOT used for the Angular tools' <c>workingDirectory</c> defaults. Those
/// mean "the user's Angular project", which is a different thing from "where our output goes" and
/// has no sensible default under the gateway at all.
/// </para>
/// </summary>
public static class OutputPaths
{
    /// <summary>Absolute. Set once at composition; read wherever output is written.</summary>
    public static string Root { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PlaywrightServerMcp");
}
