using System.CodeDom;
using System.Collections.Generic;
using System.Net;

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
        public string Name;
        public string Permission;
        public List<string> OnEnter = new List<string>();
        public List<string> OnLeave = new List<string>();
        public List<string> GlobalCommands = new List<string>();
        internal bool Loopback {
            get
            {
                if (CachedLoopback.HasValue)
                {
                    return CachedLoopback.Value;
                }

                return (CachedLoopback = IPAddress.IsLoopback(IPAddress.Parse(Address))).Value;
            }
        }

        private bool? CachedLoopback;

        public static Server Current = new Server {Name = "current", Port = 0};
    }
}