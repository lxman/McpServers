using System.Net;
using System.Net.Sockets;

namespace McpGateway.Tests;

/// <summary>Holds a real connected loopback pair open for the duration of a test.</summary>
internal sealed class LoopbackPair : IDisposable
{
    private readonly TcpListener _listener;
    private readonly TcpClient _client;
    private readonly Socket _accepted;

    public int ClientPort { get; }
    public int ServerPort { get; }

    public LoopbackPair()
    {
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        ServerPort = ((IPEndPoint)_listener.LocalEndpoint).Port;

        _client = new TcpClient();
        _client.Connect(IPAddress.Loopback, ServerPort);
        _accepted = _listener.AcceptSocket();

        ClientPort = ((IPEndPoint)_client.Client.LocalEndPoint!).Port;
    }

    public void Dispose()
    {
        _accepted.Dispose();
        _client.Dispose();
        _listener.Stop();
    }
}
