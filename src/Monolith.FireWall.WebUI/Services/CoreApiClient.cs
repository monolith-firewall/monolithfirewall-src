using System.Net.Sockets;
using System.Text;
using System.IO;

namespace Monolith.FireWall.WebUI.Services;

public class CoreApiClient
{
    private const string SocketPath = "/var/lib/monolith-firewall/run/monolith-core.sock";

    public async Task<string> SendRequestAsync(string requestJson, int timeoutMs = 5000)
    {
        try
        {
            Console.WriteLine($"[WebUI] Attempting to connect to Unix socket '{SocketPath}'...");
            using var client = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(timeoutMs));

            Console.WriteLine($"[WebUI] Calling ConnectAsync with {timeoutMs}ms timeout...");
            await client.ConnectAsync(new UnixDomainSocketEndPoint(SocketPath), cts.Token);
            Console.WriteLine("[WebUI] Connected successfully!");

            var requestBytes = Encoding.UTF8.GetBytes(requestJson);
            using var stream = new NetworkStream(client, ownsSocket: false);
            await stream.WriteAsync(requestBytes, cts.Token);
            await stream.FlushAsync(cts.Token);

            var buffer = new byte[4096];
            using var memory = new MemoryStream();

            while (true)
            {
                var bytesRead = await stream.ReadAsync(buffer, cts.Token);
                if (bytesRead <= 0)
                {
                    break;
                }

                await memory.WriteAsync(buffer.AsMemory(0, bytesRead), cts.Token);
            }

            return Encoding.UTF8.GetString(memory.ToArray());
        }
        catch (OperationCanceledException)
        {
            throw new Exception("Connection to Core service timed out. Is the Core service running?");
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to communicate with Core service: {ex.Message}", ex);
        }
    }
}
