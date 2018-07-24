using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using OTAPI;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;

namespace Chireiden.Stellaria
{
    [ApiVersion(2, 1)]
    public class Stellaria : TerrariaPlugin
    {
        private static Hooks.Net.ReceiveDataHandler _receiveDataHandler;

        private readonly Dictionary<int, ForwardPlayer> _forward = new Dictionary<int, ForwardPlayer>();

        private Config _config;

        public Stellaria(Main game) : base(game)
        {
        }

        public override string Author => "SGKoishi";
        public override string Name => "Stellaria";
        public override Version Version => new Version(1, 0, 2, 0);
        public override string Description => "In-game multi world plugin";

        public override void Initialize()
        {
            Utils.ReadConfig("tshock\\stellaria.json",
                new Config
                {
                    Servers = new List<Server>
                    {
                        new Server
                        {
                            Address = "127.0.0.1",
                            Port = 7776,
                            Name = "s1"
                        },
                        new Server
                        {
                            Address = "127.0.0.1",
                            Port = 7777,
                            Name = "lobby"
                        },
                        new Server
                        {
                            Address = "127.0.0.1",
                            Port = 7778,
                            Name = "s2"
                        }
                    },
                    // The first message sent to server to join.
                    // Construct:
                    // Total Length (2 bytes)  15, 0,
                    // Packet id (1 byte)      1,
                    // String length (1 byte)  11,
                    // "Terraria194"           84, 101, 114, 114, 97, 114, 105, 97, 49, 57, 52
                    JoinBytes = new byte[] {15, 0, 1, 11, 84, 101, 114, 114, 97, 114, 105, 97, 49, 57, 52}
                }, out _config);
            var serverCount = _config.Servers.Count;
            if (_config.Servers.Select(f => f.Name).Distinct().Count() != serverCount)
            {
                TShock.Log.ConsoleError("[Stellaria] Server name conflict");
                return;
            }

            if (_config.Servers.Select(f => f.Address + ":" + f.Port).Distinct().Count() != serverCount)
            {
                TShock.Log.ConsoleError("[Stellaria] Server address conflict");
                return;
            }

            _receiveDataHandler = Hooks.Net.ReceiveData;
            Hooks.Net.ReceiveData = ReceiveData;
            ServerApi.Hooks.ServerLeave.Register(this, args =>
                _forward[args.Who] = new ForwardPlayer {Server = Server.Current});
            //Commands.ChatCommands.RemoveAll(c => c.HasAlias("ban") || c.HasAlias("who") || c.HasAlias("kick"));
            Commands.ChatCommands.Add(new Command("chireiden.stellaria.use", SwitchVerse, "sv"));
        }

        private void SwitchVerse(CommandArgs args)
        {
            if (args.Parameters.Count < 1)
            {
                args.Player.SendInfoMessage("Usage: /sv [server name] or /sv list");
                return;
            }

            var name = args.Parameters[0].ToLower();
            if (name == "list")
            {
                args.Player.SendInfoMessage(
                    $"Available world: {string.Join(", ", _config.Servers.Where(s => args.Player.HasPermission(s.Permission)).Select(s => s.Name))}");
                return;
            }

            var nvl = _config.Servers.Where(s => s.Name == name && args.Player.HasPermission(s.Permission));
            if (!nvl.Any())
            {
                nvl = _config.Servers.Where(s => s.Name.StartsWith(name) && args.Player.HasPermission(s.Permission));
                if (!nvl.Any())
                {
                    args.Player.SendInfoMessage($"No match: {name}");
                    return;
                }

                if (nvl.Count() > 1)
                {
                    args.Player.SendInfoMessage($"Multiple matches: {name}");
                    return;
                }
            }

            var nv = nvl.First();

            if (nv.Loopback || _forward[args.TPlayer.whoAmI].Server.Name == nv.Name)
            {
                args.Player.SendInfoMessage("Currect world: " + nv.Name);
                return;
            }

            args.Player.SendInfoMessage("Switch to " + nv);
            var tc = new TcpClient();
            tc.Connect(_forward[args.TPlayer.whoAmI].Server.Address, _forward[args.TPlayer.whoAmI].Server.Port);
            tc.Client.Send(_config.JoinBytes);
            _forward[args.TPlayer.whoAmI] = new ForwardPlayer
            {
                Buffer = new byte[1024],
                Connection = tc,
                Server = nv
            };
            ThreadPool.QueueUserWorkItem(ServerLoop, args.TPlayer.whoAmI);
        }

        private HookResult ReceiveData(MessageBuffer buffer, ref byte packetid, ref int readoffset, ref int start,
            ref int length)
        {
            if (packetid == 1)
            {
                _forward[buffer.whoAmI] = new ForwardPlayer {Server = Server.Current};
                return _receiveDataHandler.Invoke(buffer, ref packetid, ref readoffset, ref start, ref length);
            }

            if (_forward[buffer.whoAmI].Server.Name == "current")
            {
                return _receiveDataHandler.Invoke(buffer, ref packetid, ref readoffset, ref start, ref length);
            }

            if (packetid == 25)
            {
                // TODO: Handle some command (e.g. /lobby) if necessary
            }

            _forward[buffer.whoAmI].Connection?.Client
                ?.Send(buffer.readBuffer, start - 2, length + 2, SocketFlags.None);
            return HookResult.Cancel;
        }

        private void ServerLoop(object state)
        {
            var wai = (int) state;
            while (_forward[wai].Connection != null && _forward[wai].Connection.Connected)
            {
                try
                {
                    var r = _forward[wai].Connection.Client.Receive(_forward[wai].Buffer);
                    if (_forward[wai].Buffer[2] == 7 && !_forward[wai].Init8)
                    {
                        _forward[wai].Connection.Client.Send(new[]
                        {
                            (byte) 11, (byte) 0, (byte) 8,
                            (byte) 0, (byte) 0, (byte) 0, (byte) 0, // SpawnX here
                            (byte) 0, (byte) 0, (byte) 0, (byte) 0 // SpawnY here
                        });
                        _forward[wai].Init8 = true;
                        _forward[wai].Connection.Client.Send(new[]
                        {
                            (byte) 8, (byte) 0, (byte) 12, (byte) wai,
                            (byte) 0, (byte) 0, (byte) 0, (byte) 0 // SpawnXY (short) here
                        });
                        _forward[wai].Init12 = true;
                    }

                    Netplay.Clients[wai].Socket.AsyncSend(_forward[wai].Buffer, 0, r, delegate { });
                }
                catch
                {
                }
            }

            _forward[wai].Server = Server.Current;
        }
    }
}