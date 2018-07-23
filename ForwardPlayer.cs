using System.Net.Sockets;

namespace Chireiden.Stellaria
{
    public class ForwardPlayer
    {
        public int Server = -1;
        public TcpClient Connection;
        public byte[] Buffer = new byte[1024];
        public bool Init8;
        public bool Init12;
    }
}