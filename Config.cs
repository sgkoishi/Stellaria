using System.Collections.Generic;
using System.Linq;

namespace Chireiden.Stellaria
{
    public class Config
    {
        public bool Host = true;
        public byte[] JoinBytes;
        public byte[] Key;
        public string Name;
        public List<Server> Servers = new List<Server>();
    }

    public class Server
    {
        private static Server _current;
        public string Address;
        public List<string> GlobalCommands = new List<string>();
        public byte[] Key;
        public string Name;
        public List<string> OnEnter = new List<string>();
        public List<string> OnLeave = new List<string>();
        public string Permission;
        public ushort Port;
        public int SpawnX;
        public int SpawnY;

        internal static Server Current
        {
            get
            {
                if (_current != null)
                {
                    return _current;
                }

                return _current = Stellaria._config.Servers.Single(s => s.Name == Stellaria._config.Name);
            }
        }
    }
}