namespace RegistryTools
{
    /// <summary>
    /// Defines the access mode for registry operations.
    /// </summary>
    public enum RegistryAccessMode
    {
        /// <summary>
        /// Read-only access. All write operations will throw InvalidOperationException.
        /// </summary>
        ReadOnly,
    
        /// <summary>
        /// Read and write access enabled.
        /// </summary>
        ReadWrite
    }
}
