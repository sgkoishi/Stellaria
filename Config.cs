using System.Collections.Generic;

namespace Chireiden.Stellaria
{
    public class Config
    {
        public bool Host = true;

        // The first message sent to server to join.
        // Construct:
        // Total Length (2 bytes)  15, 0,
        // Packet id (1 byte)      1,
        // String length (1 byte)  11,
        // "Terraria194"           84, 101, 114, 114, 97, 114, 105, 97, 49, 57, 52
        public byte[] JoinBytes = {15, 0, 1, 11, 84, 101, 114, 114, 97, 114, 105, 97, 49, 57, 52};
        public List<Server> Servers = new List<Server>();
    }

    public class Server
    {
        public string Address = "127.0.0.1";
        public ushort Port = 7777;
        public int SpawnX = 200;
        public int SpawnY = 200;
    }
}