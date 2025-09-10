namespace McpCodeEditor.Models.Analysis
{
    /// <summary>
    /// Represents platform boundaries and their isolation characteristics
    /// </summary>
    public class PlatformBoundary
    {
        /// <summary>
        /// Name of the platform (e.g., "Angular", "WPF", "API", "Core")
        /// </summary>
        public string PlatformName { get; set; } = string.Empty;

        /// <summary>
        /// Root namespace pattern for this platform (e.g., "Accounting101.Angular")
        /// </summary>
        public string NamespacePattern { get; set; } = string.Empty;

        /// <summary>
        /// Namespaces that belong to this platform
        /// </summary>
        public List<string> Namespaces { get; set; } = [];

        /// <summary>
        /// Namespaces from other platforms that this platform uses (boundary violations)
        /// </summary>
        public List<string> ExternalCouplings { get; set; } = [];

        /// <summary>
        /// Shared/common namespaces used by this platform
        /// </summary>
        public List<string> SharedDependencies { get; set; } = [];

        /// <summary>
        /// Whether this platform maintains isolation (no cross-platform coupling)
        /// </summary>
        public bool IsIsolated => ExternalCouplings.Count == 0;

        /// <summary>
        /// Number of internal namespace dependencies within this platform
        /// </summary>
        public int InternalCouplingCount { get; set; }

        /// <summary>
        /// Number of external platform dependencies (boundary violations)
        /// </summary>
        public int ExternalCouplingCount => ExternalCouplings.Count;

        /// <summary>
        /// Isolation score (0.0 = highly coupled, 1.0 = perfectly isolated)
        /// </summary>
        public double IsolationScore { get; set; }

        /// <summary>
        /// Projects that belong to this platform
        /// </summary>
        public List<string> Projects { get; set; } = [];

        /// <summary>
        /// Platform type classification
        /// </summary>
        public PlatformType Type { get; set; } = PlatformType.Unknown;
    }

    /// <summary>
    /// Types of platforms detected
    /// </summary>
    public enum PlatformType
    {
        Unknown,
        Frontend,      // Angular, React, etc.
        Desktop,       // WPF, WinForms, etc.
        Api,           // Web API, REST services
        Core,          // Shared libraries, models
        DataAccess,    // Database, repositories
        Services,      // Business logic, services
        Tests          // Unit tests, integration tests
    }
}
