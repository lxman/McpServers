using System.Collections.Generic;

namespace RegistryTools
{
    /// <summary>
    /// Represents metadata about a registry key.
    /// </summary>
    public class RegistryKeyInfo
    {
        public string FullPath { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int SubKeyCount { get; set; }
        public int ValueCount { get; set; }
        public List<string> SubKeyNames { get; set; } = new List<string>();
        public List<string> ValueNames { get; set; } = new List<string>();

        public override string ToString()
        {
            return $"{FullPath} (SubKeys: {SubKeyCount}, Values: {ValueCount})";
        }
    }
}
