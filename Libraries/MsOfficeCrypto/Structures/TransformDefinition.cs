using System;

namespace MsOfficeCrypto.Structures
{
    /// <summary>
    /// Transform definition structure
    /// </summary>
    public class TransformDefinition
    {
        /// <summary>
        /// The name of the transform
        /// </summary>
        public string Name { get; set; } = string.Empty;
        /// <summary>
        /// The type of transform
        /// </summary>
        public string TransformType { get; set; } = string.Empty;
        /// <summary>
        /// The configuration data for the transform
        /// </summary>
        public byte[] ConfigurationData { get; set; } = Array.Empty<byte>();
    }
}