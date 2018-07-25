using System.Net.Sockets;

namespace Chireiden.Stellaria
{
    public class ForwardPlayer
    {
        public Server Server;
        public TcpClient Connection;
        public byte[] Buffer;
        public bool Init8;
        public bool Init12;
        public string IP;
    }
}