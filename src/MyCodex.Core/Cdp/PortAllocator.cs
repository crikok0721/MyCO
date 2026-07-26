using System.Net;
using System.Net.Sockets;

namespace MyCodex.Cdp;

public static class PortAllocator
{
    public static int GetRandomLoopbackPort()
    {
        for (var attempt = 0; attempt < 32; attempt++)
        {
            var port = Random.Shared.Next(49152, 65536);
            try
            {
                var listener = new TcpListener(IPAddress.Loopback, port);
                listener.Start();
                listener.Stop();
                return port;
            }
            catch (SocketException)
            {
                // Try another IANA dynamic/private port.
            }
        }

        throw new InvalidOperationException("Unable to reserve a random loopback TCP port.");
    }
}
