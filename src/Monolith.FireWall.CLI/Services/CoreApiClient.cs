using System.Net.Sockets;
using System.Text;

namespace Monolith.FireWall.CLI.Services;

public class CoreApiClient
{
    private const string SocketPath = "/var/lib/monolith-firewall/run/monolith-core.sock";
    private readonly int _timeoutMs;

    public CoreApiClient(int timeoutMs = 5000)
    {
        _timeoutMs = timeoutMs;
    }

    public async Task<string> SendRequestAsync(string requestJson, CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromMilliseconds(_timeoutMs));
            
            // Set socket timeout
            client.ReceiveTimeout = _timeoutMs;
            client.SendTimeout = _timeoutMs;

            await client.ConnectAsync(new UnixDomainSocketEndPoint(SocketPath), cts.Token);

            var requestBytes = Encoding.UTF8.GetBytes(requestJson);
            using var stream = new NetworkStream(client, ownsSocket: false);
            await stream.WriteAsync(requestBytes, cts.Token);
            await stream.FlushAsync(cts.Token);

            var buffer = new byte[4096];
            using var memory = new MemoryStream();
            
            // Read with timeout - use cancellation token properly
            var readCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token);
            readCts.CancelAfter(TimeSpan.FromMilliseconds(_timeoutMs));
            
            try
            {
                while (!readCts.Token.IsCancellationRequested)
                {
                    var bytesRead = await stream.ReadAsync(buffer, readCts.Token);
                    if (bytesRead <= 0)
                    {
                        break;
                    }

                    await memory.WriteAsync(buffer.AsMemory(0, bytesRead), readCts.Token);
                }
            }
            catch (OperationCanceledException) when (readCts.Token.IsCancellationRequested)
            {
                // Timeout occurred
                throw new TimeoutException($"Read operation timed out after {_timeoutMs}ms");
            }

            var response = Encoding.UTF8.GetString(memory.ToArray());
            if (string.IsNullOrEmpty(response))
            {
                throw new Exception("Empty response from Core service");
            }
            
            return response;
        }
        catch (OperationCanceledException)
        {
            throw new Exception("Connection to Core service timed out. Is the Core service running?");
        }
        catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AddressNotAvailable || ex.ErrorCode == 2)
        {
            throw new Exception($"Core service socket not found at {SocketPath}. Is the Core service running?");
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to communicate with Core service: {ex.Message}", ex);
        }
    }

    public bool IsAvailable()
    {
        return File.Exists(SocketPath);
    }
}
