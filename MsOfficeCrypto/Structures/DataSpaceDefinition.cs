using System.Collections.Generic;

namespace MsOfficeCrypto.Structures
{
    /// <summary>
    /// DataSpace definition structure
    /// </summary>
    public class DataSpaceDefinition
    {
        /// <summary>
        /// The name of the data space
        /// </summary>
        public string Name { get; set; } = string.Empty;
        /// <summary>
        /// List of reference components for the data space
        /// </summary>
        public List<string> ReferenceComponents { get; set; } = new List<string>();
        /// <summary>
        /// Reference to the transform used for the data space
        /// </summary>
        public string TransformReference { get; set; } = string.Empty;
    }
}