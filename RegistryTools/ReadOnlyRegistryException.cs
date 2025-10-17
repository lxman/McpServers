using System;

namespace RegistryTools
{
    /// <summary>
    /// Exception thrown when attempting write operations in read-only mode.
    /// </summary>
    public class ReadOnlyRegistryException : InvalidOperationException
    {
        public ReadOnlyRegistryException()
            : base("Cannot perform write operation: Registry access is in read-only mode.")
        {
        }

        public ReadOnlyRegistryException(string message)
            : base(message)
        {
        }

        public ReadOnlyRegistryException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
