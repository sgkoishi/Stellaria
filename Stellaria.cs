using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using MaxMind;
using OTAPI;
using Terraria;
using Terraria.Localization;
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
        public override Version Version => new Version(1, 0, 5, 0);
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
                            Name = "s1",
                            GlobalCommands = new List<string> {"sv", "who"},
                            Permission = "",
                            Key = Utils.RandomKey(32)
                        },
                        new Server
                        {
                            Address = "127.0.0.1",
                            Port = 7777,
                            Name = "lobby",
                            GlobalCommands = new List<string> {"sv", "who"},
                            Permission = "",
                            Key = Utils.RandomKey(32)
                        },
                        new Server
                        {
                            Address = "127.0.0.1",
                            Port = 7778,
                            Name = "s2",
                            GlobalCommands = new List<string> {"sv", "who"},
                            Permission = "",
                            Key = Utils.RandomKey(32)
                        }
                    },
                    // The first message sent to server to join.
                    // Construct:
                    // Total Length (2 bytes)  15, 0,
                    // Packet id (1 byte)      1,
                    // String length (1 byte)  11,
                    // "Terraria194"           84, 101, 114, 114, 97, 114, 105, 97, 49, 57, 52
                    JoinBytes = new byte[] {15, 0, 1, 11, 84, 101, 114, 114, 97, 114, 105, 97, 49, 57, 52},
                    Key = Utils.RandomKey(32)
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
            if (!_config.Host)
            {
                // Clear the ServerConnect hook, because it load host server as player's IP address
                // We will add another OnConnect to get correct IP address behind host.
                (typeof(HandlerCollection<ConnectEventArgs>)
                    .GetField("registrations", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.GetValue(ServerApi.Hooks.ServerConnect) as dynamic)?.Clear();
                ServerApi.Hooks.ServerConnect.Register(this, OnServerConnect);
            }

            Commands.ChatCommands.RemoveAll(c => c.HasAlias("who"));
            Commands.ChatCommands.Add(new Command(ListConnectedPlayers, "playing", "online", "who"));
            Commands.ChatCommands.Add(new Command("chireiden.stellaria.use", SwitchVerse, "sv"));
        }

        private void ListConnectedPlayers(CommandArgs args)
        {
            var invalidUsage = args.Parameters.Count > 2;
            var displayIdsRequested = false;
            var pageNumber = 1;
            if (!invalidUsage)
            {
                foreach (var parameter in args.Parameters)
                {
                    if (parameter.Equals("-i", StringComparison.InvariantCultureIgnoreCase))
                    {
                        displayIdsRequested = true;
                        continue;
                    }

                    if (!int.TryParse(parameter, out pageNumber))
                    {
                        invalidUsage = true;
                        break;
                    }
                }
            }

            if (invalidUsage)
            {
                args.Player.SendErrorMessage("Invalid usage, proper usage: {0}who [-i] [pagenumber]",
                    Commands.Specifier);
                return;
            }

            if (displayIdsRequested && !args.Player.HasPermission(Permissions.seeids))
            {
                args.Player.SendErrorMessage("You don't have the required permission to list player ids.");
                return;
            }

            args.Player.SendSuccessMessage("Total Online Players ({0}/{1})", TShock.Utils.ActivePlayers(),
                TShock.Config.MaxSlots);
            var players = from p in TShock.Players
                where p != null && p.Active
                group displayIdsRequested
                    ? $"{p.Name} (IX: {p.Index}{(p.User != null ? ", ID: " + p.User.ID : "")})"
                    : p.Name by _forward[p.Index].Server;
            var content = new List<string>();
            foreach (var server in players)
            {
                content.Add($"Players in {server.Key.Name} ({server.Count()}):");
                content.AddRange(PaginationTools.BuildLinesFromTerms(server));
            }

            PaginationTools.SendPage(args.Player, pageNumber, content,
                new PaginationTools.Settings
                {
                    IncludeHeader = false,
                    FooterFormat =
                        $"Type {Commands.Specifier}who {(displayIdsRequested ? "-i " : string.Empty)}{{0}} for more."
                });
        }

        private void OnServerConnect(ConnectEventArgs args)
        {
            if (TShock.ShuttingDown)
            {
                NetMessage.SendData(2, args.Who, -1, NetworkText.FromLiteral("Server is shutting down..."));
                args.Handled = true;
                return;
            }

            var player = new TSPlayer(args.Who);
            Utils.CacheIP?.SetValue(player, _forward[args.Who].IP);
            if (TShock.Utils.ActivePlayers() + 1 > TShock.Config.MaxSlots + TShock.Config.ReservedSlots)
            {
                TShock.Utils.ForceKick(player, TShock.Config.ServerFullNoReservedReason, true);
                args.Handled = true;
                return;
            }

            if (!FileTools.OnWhitelist(player.IP))
            {
                TShock.Utils.ForceKick(player, TShock.Config.WhitelistKickReason, true);
                args.Handled = true;
                return;
            }

            if (TShock.Geo != null)
            {
                var code = TShock.Geo.TryGetCountryCode(IPAddress.Parse(player.IP));
                player.Country = code == null ? "N/A" : GeoIPCountry.GetCountryNameByCode(code);
                if (code == "A1" && TShock.Config.KickProxyUsers)
                {
                    TShock.Utils.ForceKick(player, "Proxies are not allowed.", true);
                    args.Handled = true;
                    return;
                }
            }

            TShock.Players[args.Who] = player;
        }

        private void SwitchVerse(CommandArgs args)
        {
            if (args.Parameters.Count < 1)
            {
                args.Player.SendInfoMessage("Usage: /sv [server name] or /sv list");
                return;
            }

            var name = args.Parameters[0].ToLower();
            var ps = _config.Servers.Where(s => args.Player.HasPermission(s.Permission));
            if (name == "list")
            {
                args.Player.SendInfoMessage(
                    $"Available world: {string.Join(", ", ps.Select(s => s.Name))}");
                return;
            }

            var nvl = ps.Where(s => s.Name == name);
            if (!nvl.Any())
            {
                nvl = ps.Where(s => s.Name.StartsWith(name));
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
            // Send real IP to server. Use secret key to prevent modified client cheating about their IP.
            var ms = new MemoryStream();
            var bw = new BinaryWriter(ms);
            bw.Write((short) _config.Key.Length + args.Player.IP.Length + 4);
            bw.Write((byte) 94);
            bw.Write(_config.Key);
            bw.Write(args.Player.IP);
            tc.Client.Send(ms.ToArray());
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
            if (_config.Host)
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
                    var text = new BinaryReader(new MemoryStream(buffer.readBuffer, start + 1, length - 1))
                        .ReadString();
                    if (text.StartsWith(Commands.Specifier) || text.StartsWith(Commands.SilentSpecifier))
                    {
                        var p = Utils.ParseParameters(text);
                        // A GlobalCommand, handled by host server.
                        if (p.Count > 0 && _forward[buffer.whoAmI].Server.GlobalCommands.Contains(p[0]))
                        {
                            return _receiveDataHandler.Invoke(buffer, ref packetid, ref readoffset, ref start,
                                ref length);
                        }
                    }
                }

                _forward[buffer.whoAmI].Connection?.Client
                    ?.Send(buffer.readBuffer, start - 2, length + 2, SocketFlags.None);
                return HookResult.Cancel;
            }

            if (packetid != 94)
            {
                return _receiveDataHandler.Invoke(buffer, ref packetid, ref readoffset, ref start, ref length);
            }

            var validKey = true;
            for (var i = 0; i < _config.Key.Length; i++)
            {
                if (buffer.readBuffer[start + 1 + i] != _config.Key[i])
                {
                    validKey = false;
                    break;
                }
            }

            if (validKey)
            {
                _forward[buffer.whoAmI].IP = new BinaryReader(new MemoryStream(buffer.readBuffer,
                    start + 1 + _config.Key.Length,
                    length - 1 - _config.Key.Length)).ReadString();
                return HookResult.Cancel;
            }

            return _receiveDataHandler.Invoke(buffer, ref packetid, ref readoffset, ref start, ref length);
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
                        NetMessage.SendData(65, -1, -1, NetworkText.Empty, 0, wai, _forward[wai].Server.SpawnX,
                            _forward[wai].Server.SpawnY, 1);
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