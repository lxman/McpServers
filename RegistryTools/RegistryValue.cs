using Microsoft.Win32;

namespace RegistryTools
{
    /// <summary>
    /// Represents a registry value with its name, data, and type.
    /// </summary>
    public class RegistryValue
    {
        public string Name { get; set; } = string.Empty;
        public object? Data { get; set; }
        public RegistryValueKind Kind { get; set; }

        public RegistryValue()
        {
        }

        public RegistryValue(string name, object? data, RegistryValueKind kind)
        {
            Name = name;
            Data = data;
            Kind = kind;
        }

        public override string ToString()
        {
            return $"{Name} ({Kind}): {Data}";
        }
    }
}
