using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Renci.SshNet;
using SshClient.Core.Models;

namespace SshClient.Core.Services;

/// <summary>
/// Manages SSH connections with support for multiple named connections.
/// Profiles are persisted to disk for reuse across sessions.
/// </summary>
public sealed class SshConnectionManager : IDisposable
{
    private readonly ILogger<SshConnectionManager> _logger;
    private readonly ConcurrentDictionary<string, ManagedConnection> _connections = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, SshConnectionProfile> _profiles = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _profilesFilePath;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private bool _disposed;

    private sealed class ManagedConnection(Renci.SshNet.SshClient sshClient, SshConnectionProfile profile)
        : IDisposable
    {
        public Renci.SshNet.SshClient SshClient { get; } = sshClient;
        public SftpClient? SftpClient { get; set; }
        public SshConnectionProfile Profile { get; } = profile;
        public DateTime ConnectedAt { get; } = DateTime.UtcNow;

        public void Dispose()
        {
            SftpClient?.Dispose();
            SshClient.Dispose();
        }
    }

    public SshConnectionManager(ILogger<SshConnectionManager> logger)
    {
        _logger = logger;
        
        // Set up profiles file in user's AppData
        string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string sshMcpDir = Path.Combine(appDataPath, "SshMcp");
        Directory.CreateDirectory(sshMcpDir);
        _profilesFilePath = Path.Combine(sshMcpDir, "profiles.json");
        
        // Load existing profiles from disk
        LoadProfilesFromDisk();
    }
    
    private void LoadProfilesFromDisk()
    {
        try
        {
            if (File.Exists(_profilesFilePath))
            {
                string json = File.ReadAllText(_profilesFilePath);
                var profiles = JsonSerializer.Deserialize<List<SshConnectionProfile>>(json, JsonOptions);
                if (profiles != null)
                {
                    foreach (SshConnectionProfile profile in profiles)
                    {
                        _profiles[profile.Name] = profile;
                    }
                    _logger.LogInformation("Loaded {Count} SSH profiles from disk", profiles.Count);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load SSH profiles from {Path}", _profilesFilePath);
        }
    }
    
    private void SaveProfilesToDisk()
    {
        try
        {
            List<SshConnectionProfile> profiles = _profiles.Values.ToList();
            string json = JsonSerializer.Serialize(profiles, JsonOptions);
            File.WriteAllText(_profilesFilePath, json);
            _logger.LogDebug("Saved {Count} SSH profiles to disk", profiles.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save SSH profiles to {Path}", _profilesFilePath);
        }
    }

    /// <summary>
    /// Saves a connection profile for later use
    /// </summary>
    public void SaveProfile(SshConnectionProfile profile)
    {
        _profiles[profile.Name] = profile;
        SaveProfilesToDisk();
        _logger.LogInformation("Saved SSH profile: {ProfileName} for {Username}@{Host}:{Port}",
            profile.Name, profile.Username, profile.Host, profile.Port);
    }

    /// <summary>
    /// Gets all saved profiles
    /// </summary>
    public IReadOnlyList<SshConnectionProfile> GetProfiles()
    {
        return _profiles.Values.ToList();
    }

    /// <summary>
    /// Gets a specific profile by name
    /// </summary>
    public SshConnectionProfile? GetProfile(string name)
    {
        return _profiles.GetValueOrDefault(name);
    }

    /// <summary>
    /// Removes a saved profile
    /// </summary>
    public bool RemoveProfile(string name)
    {
        bool removed = _profiles.TryRemove(name, out _);
        if (removed) SaveProfilesToDisk();
        return removed;
    }

    /// <summary>
    /// Connects using a saved profile
    /// </summary>
    public async Task<SshConnectionInfo> ConnectAsync(string profileName, CancellationToken cancellationToken = default)
    {
        if (!_profiles.TryGetValue(profileName, out SshConnectionProfile? profile))
        {
            return new SshConnectionInfo
            {
                Name = profileName,
                Host = "unknown",
                Username = "unknown",
                IsConnected = false,
                LastError = $"Profile '{profileName}' not found"
            };
        }

        return await ConnectAsync(profile, cancellationToken);
    }

    /// <summary>
    /// Connects using a connection profile
    /// </summary>
    public async Task<SshConnectionInfo> ConnectAsync(SshConnectionProfile profile, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        try
        {
            // Disconnect existing connection with same name
            if (_connections.TryRemove(profile.Name, out ManagedConnection? existing))
            {
                _logger.LogInformation("Disconnecting existing connection: {Name}", profile.Name);
                existing.Dispose();
            }

            ConnectionInfo connectionInfo = CreateConnectionInfo(profile);
            var client = new Renci.SshNet.SshClient(connectionInfo);

            _logger.LogInformation("Connecting to {Username}@{Host}:{Port} as '{Name}'",
                profile.Username, profile.Host, profile.Port, profile.Name);

            await Task.Run(() => client.Connect(), cancellationToken);

            var managed = new ManagedConnection(client, profile);
            _connections[profile.Name] = managed;

            // Save profile for reconnection
            _profiles[profile.Name] = profile;
            SaveProfilesToDisk();

            _logger.LogInformation("Connected successfully: {Name}", profile.Name);

            return new SshConnectionInfo
            {
                Name = profile.Name,
                Host = profile.Host,
                Port = profile.Port,
                Username = profile.Username,
                IsConnected = true,
                ConnectedAt = managed.ConnectedAt,
                AuthMethod = GetAuthMethodName(profile),
                ServerVersion = client.ConnectionInfo.ServerVersion
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to {Host}:{Port}", profile.Host, profile.Port);
            return new SshConnectionInfo
            {
                Name = profile.Name,
                Host = profile.Host,
                Port = profile.Port,
                Username = profile.Username,
                IsConnected = false,
                LastError = ex.Message,
                AuthMethod = GetAuthMethodName(profile)
            };
        }
    }

    /// <summary>
    /// Disconnects a named connection
    /// </summary>
    public bool Disconnect(string connectionName)
    {
        if (_connections.TryRemove(connectionName, out ManagedConnection? connection))
        {
            _logger.LogInformation("Disconnecting: {Name}", connectionName);
            connection.Dispose();
            return true;
        }
        return false;
    }

    /// <summary>
    /// Disconnects all connections
    /// </summary>
    public void DisconnectAll()
    {
        foreach (string name in _connections.Keys.ToList())
        {
            Disconnect(name);
        }
    }

    /// <summary>
    /// Gets information about all connections
    /// </summary>
    public IReadOnlyList<SshConnectionInfo> GetConnections()
    {
        return _connections.Select(kvp => new SshConnectionInfo
        {
            Name = kvp.Key,
            Host = kvp.Value.Profile.Host,
            Port = kvp.Value.Profile.Port,
            Username = kvp.Value.Profile.Username,
            IsConnected = kvp.Value.SshClient.IsConnected,
            ConnectedAt = kvp.Value.ConnectedAt,
            AuthMethod = GetAuthMethodName(kvp.Value.Profile),
            ServerVersion = kvp.Value.SshClient.ConnectionInfo?.ServerVersion
        }).ToList();
    }

    /// <summary>
    /// Gets a specific connection info
    /// </summary>
    public SshConnectionInfo? GetConnection(string connectionName)
    {
        if (!_connections.TryGetValue(connectionName, out ManagedConnection? connection))
            return null;

        return new SshConnectionInfo
        {
            Name = connectionName,
            Host = connection.Profile.Host,
            Port = connection.Profile.Port,
            Username = connection.Profile.Username,
            IsConnected = connection.SshClient.IsConnected,
            ConnectedAt = connection.ConnectedAt,
            AuthMethod = GetAuthMethodName(connection.Profile),
            ServerVersion = connection.SshClient.ConnectionInfo?.ServerVersion
        };
    }

    /// <summary>
    /// Gets the underlying SSH client for a connection (internal use)
    /// </summary>
    internal Renci.SshNet.SshClient? GetSshClient(string connectionName)
    {
        return _connections.TryGetValue(connectionName, out ManagedConnection? connection) 
            ? connection.SshClient 
            : null;
    }

    /// <summary>
    /// Gets or creates an SFTP client for a connection
    /// </summary>
    internal SftpClient? GetOrCreateSftpClient(string connectionName)
    {
        if (!_connections.TryGetValue(connectionName, out ManagedConnection? connection))
            return null;

        if (connection.SftpClient is { IsConnected: true })
            return connection.SftpClient;

        // Create new SFTP client
        ConnectionInfo connectionInfo = CreateConnectionInfo(connection.Profile);
        var sftpClient = new SftpClient(connectionInfo);
        sftpClient.Connect();

        connection.SftpClient = sftpClient;
        return sftpClient;
    }

    private static ConnectionInfo CreateConnectionInfo(SshConnectionProfile profile)
    {
        var authMethods = new List<AuthenticationMethod>();

        // Private key authentication
        if (!string.IsNullOrEmpty(profile.PrivateKeyPath))
        {
            PrivateKeyFile keyFile = string.IsNullOrEmpty(profile.Passphrase)
                ? new PrivateKeyFile(profile.PrivateKeyPath)
                : new PrivateKeyFile(profile.PrivateKeyPath, profile.Passphrase);
            
            authMethods.Add(new PrivateKeyAuthenticationMethod(profile.Username, keyFile));
        }

        // Password authentication
        if (!string.IsNullOrEmpty(profile.Password))
        {
            authMethods.Add(new PasswordAuthenticationMethod(profile.Username, profile.Password));
        }

        if (authMethods.Count == 0)
        {
            throw new ArgumentException("At least one authentication method (key or password) must be provided");
        }

        return new ConnectionInfo(
            profile.Host,
            profile.Port,
            profile.Username,
            authMethods.ToArray())
        {
            Timeout = TimeSpan.FromSeconds(profile.TimeoutSeconds)
        };
    }

    private static string GetAuthMethodName(SshConnectionProfile profile)
    {
        if (!string.IsNullOrEmpty(profile.PrivateKeyPath))
            return "key";
        if (!string.IsNullOrEmpty(profile.Password))
            return "password";
        return "unknown";
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (ManagedConnection connection in _connections.Values)
        {
            try
            {
                connection.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error disposing connection");
            }
        }
        _connections.Clear();
    }
}