using System.Collections.Generic;

namespace MsOfficeCrypto.Structures
{
    /// <summary>
    /// DataSpaces information summary
    /// </summary>
    public class DataSpacesInfo
    {
        /// <summary>
        /// How many data spaces are present
        /// </summary>
        public int DataSpaceCount { get; set; }
        /// <summary>
        /// How many transforms are present
        /// </summary>
        public int TransformCount { get; set; }
        /// <summary>
        /// List of data space definitions
        /// </summary>
        public List<DataSpaceDefinition> DataSpaces { get; set; } = new List<DataSpaceDefinition>();
        /// <summary>
        /// List of transform definitions
        /// </summary>
        public List<TransformDefinition> Transforms { get; set; } = new List<TransformDefinition>();
        /// <summary>
        /// Indicates if a data space map is present
        /// </summary>
        public bool HasDataSpaceMap { get; set; }
        /// <summary>
        /// Indicates if data space information is present
        /// </summary>
        public bool HasDataSpaceInfo { get; set; }
        /// <summary>
        /// Indicates if transform information is present
        /// </summary>
        public bool HasTransformInfo { get; set; }
    }
}