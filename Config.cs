using System.Collections.Generic;

namespace Chireiden.Stellaria
{
    public class Config
    {
        public bool Host = true;
        public byte[] JoinBytes;
        public List<Server> Servers = new List<Server>();
    }

    public class Server
    {
        public string Address;
        public ushort Port;
        public int SpawnX;
        public int SpawnY;
    }
}