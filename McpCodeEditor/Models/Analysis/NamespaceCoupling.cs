namespace McpCodeEditor.Models.Analysis
{
    /// <summary>
    /// Represents coupling relationships between namespaces based on using statement analysis
    /// </summary>
    public class NamespaceCoupling
    {
        /// <summary>
        /// The namespace that is using other namespaces
        /// </summary>
        public string SourceNamespace { get; set; } = string.Empty;

        /// <summary>
        /// The namespace being used/referenced
        /// </summary>
        public string TargetNamespace { get; set; } = string.Empty;

        /// <summary>
        /// Number of times this namespace dependency appears across files
        /// </summary>
        public int UsageCount { get; set; }

        /// <summary>
        /// Files where this coupling relationship exists
        /// </summary>
        public List<string> SourceFiles { get; set; } = [];

        /// <summary>
        /// Whether this is an internal project namespace or external (System, NuGet packages)
        /// </summary>
        public bool IsInternalCoupling { get; set; }

        /// <summary>
        /// Whether this represents a cross-platform boundary violation
        /// </summary>
        public bool IsCrossPlatformCoupling { get; set; }

        /// <summary>
        /// Calculated coupling strength (0.0 to 1.0)
        /// </summary>
        public double CouplingStrength { get; set; }

        /// <summary>
        /// Platform or layer that the source namespace belongs to
        /// </summary>
        public string SourcePlatform { get; set; } = string.Empty;

        /// <summary>
        /// Platform or layer that the target namespace belongs to
        /// </summary>
        public string TargetPlatform { get; set; } = string.Empty;
    }
}
