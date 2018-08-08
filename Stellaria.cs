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
        internal static Config _config;
        private readonly Dictionary<int, ForwardPlayer> _forward = new Dictionary<int, ForwardPlayer>();

        public Stellaria(Main game) : base(game)
        {
        }

        public override string Author => "SGKoishi";
        public override string Name => "Stellaria";
        public override Version Version => Assembly.GetExecutingAssembly().GetName().Version;
        public override string Description => "In-game multi world plugin";

        public override void Initialize()
        {
            _receiveDataHandler = Hooks.Net.ReceiveData;
            Hooks.Net.ReceiveData = ReceiveData;
            ServerApi.Hooks.ServerLeave.Register(this, OnLeave);
            var key = Utils.RandomKey(32);
            Utils.ReadConfig("tshock\\stellaria.json",
                new Config
                {
                    Servers = new List<Server>
                    {
                        new Server
                        {
                            Address = "127.0.0.1",
                            Port = 7776,
                            Name = "wrapper",
                            GlobalCommands = new List<string> {"sv", "who"},
                            Permission = "",
                            Key = key,
                            SpawnX = 1000,
                            SpawnY = 300,
                            Invisible = true
                        },
                        new Server
                        {
                            Address = "127.0.0.1",
                            Port = 7777,
                            Name = "lobby",
                            GlobalCommands = new List<string> {"sv", "who"},
                            Permission = "",
                            Key = Utils.RandomKey(32),
                            SpawnX = 1000,
                            SpawnY = 300
                        },
                        new Server
                        {
                            Address = "127.0.0.1",
                            Port = 7778,
                            Name = "game1",
                            GlobalCommands = new List<string> {"sv", "who"},
                            Permission = "",
                            Key = Utils.RandomKey(32),
                            SpawnX = 1000,
                            SpawnY = 300
                        },
                        new Server
                        {
                            Address = "127.0.0.1",
                            Port = 7779,
                            Name = "game2",
                            GlobalCommands = new List<string> {"sv", "who"},
                            Permission = "",
                            Key = Utils.RandomKey(32),
                            SpawnX = 1000,
                            SpawnY = 300
                        }
                    },
                    // The first message sent to server to join.
                    // Construct:
                    // Packet id (1 byte)      1,
                    // String length (1 byte)  11,
                    // "Terraria194"           84, 101, 114, 114, 97, 114, 105, 97, 49, 57, 52
                    JoinBytes = new byte[] {1, 11, 84, 101, 114, 114, 97, 114, 105, 97, 49, 57, 52},
                    Key = key,
                    Name = "lobby"
                }, out _config);
            if (_config.Host)
            {
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

                ServerApi.Hooks.NetSendBytes.Register(this, OnSendBytes, int.MaxValue);
            }
            else
            {
                // Clear the ServerConnect hook, because it load host server as player's IP address
                // We will add another OnConnect to get correct IP address behind host.
                // https://stackoverflow.com/questions/51532079/call-a-public-method-of-a-objects-actual-type
                // ((IList) typeof(HandlerCollection<ConnectEventArgs>)
                //     .GetField("registrations", BindingFlags.Instance | BindingFlags.NonPublic)
                //     .GetValue(ServerApi.Hooks.ServerConnect)).Clear();
                ServerApi.Hooks.ServerConnect.Register(this, OnServerConnect, int.MaxValue);
            }

            Commands.ChatCommands.RemoveAll(c => c.HasAlias("who"));
            Commands.ChatCommands.Add(new Command(ListConnectedPlayers, "playing", "online", "who"));
            Commands.ChatCommands.Add(new Command("chireiden.stellaria.use", SwitchVerse, "sv"));
        }

        private void OnSendBytes(SendBytesEventArgs args)
        {
            if (_forward[args.Socket.Id].Server.Name != _config.Name)
            {
                args.Handled = true;
            }
        }

        private void OnLeave(LeaveEventArgs args)
        {
            _forward.Remove(args.Who);
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

            var activePlayers = TShock.Players.Where(p => p != null && (p.Active || _forward.ContainsKey(p.Index))).ToList();
            args.Player.SendSuccessMessage($"Total Online Players ({activePlayers.Count()}/{TShock.Config.MaxSlots})");
            var players = from p in activePlayers
                group displayIdsRequested
                    ? $"{p.Name} (IX: {p.Index}{(p.Account != null ? ", ID: " + p.Account.ID : "")})"
                    : p.Name by _forward[p.Index].Server;
            var content = new List<string>();
            var hiddenPlayers = 0;
            foreach (var server in players)
            {
                if (!args.Player.HasPermission(server.Key.Permission))
                {
                    hiddenPlayers += server.Count();
                    continue;
                }
                content.Add($"Players in {server.Key.Name} ({server.Count()}):");
                content.AddRange(PaginationTools.BuildLinesFromTerms(server));
            }

            if (hiddenPlayers != 0)
            {
                content.Add($"{hiddenPlayers} players in hidden server.");
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
            if (TShock.Utils.GetActivePlayerCount() + 1 > TShock.Config.MaxSlots + TShock.Config.ReservedSlots)
            {
                player.Kick(TShock.Config.ServerFullNoReservedReason, true);
                args.Handled = true;
                return;
            }

            if (!FileTools.OnWhitelist(player.IP))
            {
                player.Kick(TShock.Config.WhitelistKickReason, true);
                args.Handled = true;
                return;
            }

            if (TShock.Geo != null)
            {
                var code = TShock.Geo.TryGetCountryCode(IPAddress.Parse(player.IP));
                player.Country = code == null ? "N/A" : GeoIPCountry.GetCountryNameByCode(code);
                if (code == "A1" && TShock.Config.KickProxyUsers)
                {
                    player.Kick("Proxies are not allowed.", true);
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
            var permittedWorld = _config.Servers.Where(s => args.Player.HasPermission(s.Permission)).ToList();
            if (name == "list")
            {
                args.Player.SendInfoMessage(
                    $"Available world: {string.Join(", ", permittedWorld.Where(s => !s.Invisible).Select(s => s.Name))}");
                return;
            }

            var matchName = permittedWorld.Where(s => s.Name == name).ToList();
            if (!matchName.Any())
            {
                matchName = permittedWorld.Where(s => s.Name.StartsWith(name)).ToList();
                if (!matchName.Any())
                {
                    args.Player.SendInfoMessage($"No match: {name}");
                    return;
                }

                if (matchName.Count() > 1)
                {
                    args.Player.SendInfoMessage($"Multiple matches: {name}");
                    return;
                }
            }

            var newWorld = matchName.First();
            var whoAmI = args.TPlayer.whoAmI;
            if (_forward[whoAmI].Server.Name == newWorld.Name)
            {
                args.Player.SendInfoMessage("Currect world: " + newWorld.Name);
                return;
            }

            args.Player.SendInfoMessage("Switch to " + newWorld.Name);
            if (newWorld.Name == _config.Name)
            {
                NetMessage.SendData(14, -1, whoAmI, null, whoAmI, (args.TPlayer.active = true).GetHashCode());
                NetMessage.SendData(4, -1, whoAmI, null, whoAmI);
                NetMessage.SendData(13, -1, whoAmI, null, whoAmI);
                // Back to host server, no new TcpClient.
                _forward[whoAmI].Connection.Close();
                _forward[whoAmI].Server = newWorld;
                _forward[whoAmI].Connection = null;
                return;
            }

            // Turn player to !active in host server
            NetMessage.SendData(14, -1, whoAmI, null, whoAmI, (args.TPlayer.active = false).GetHashCode());
            var tc = new TcpClient();
            tc.Connect(newWorld.Address, newWorld.Port);
            // Send real IP to server. Use secret key to prevent modified client cheating about their IP.
            var ms = new MemoryStream();
            var bw = new BinaryWriter(ms);
            bw.Write((short) (15 + _config.Key.Length + 1 + args.Player.IP.Length));
            bw.Write(_config.JoinBytes);
            bw.Write(newWorld.Key);
            bw.Write(args.Player.IP);
            tc.Client.Send(ms.ToArray());
            _forward[whoAmI] = new ForwardPlayer
            {
                Buffer = new byte[1024],
                Connection = tc,
                Server = newWorld
            };
            ThreadPool.QueueUserWorkItem(ServerLoop, whoAmI);
        }

        private HookResult ReceiveData(MessageBuffer buffer, ref byte packetid, ref int readoffset, ref int start,
            ref int length)
        {
            if (packetid == 1)
            {
                _forward[buffer.whoAmI] = new ForwardPlayer {Server = Server.Current};
                if (length <= 15)
                {
                    return _receiveDataHandler.Invoke(buffer, ref packetid, ref readoffset, ref start, ref length);
                }

                var validKey = true;
                for (var i = 0; i < _config.Key.Length; i++)
                {
                    if (buffer.readBuffer[start + 13 + i] != _config.Key[i])
                    {
                        validKey = false;
                        break;
                    }
                }

                if (validKey)
                {
                    _forward[buffer.whoAmI].IP =
                        Utils.ReadFromBinaryReader(buffer.readBuffer, start + 13 + _config.Key.Length);
                }

                return _receiveDataHandler.Invoke(buffer, ref packetid, ref readoffset, ref start, ref length);
            }

            if (_config.Host)
            {
                if (packetid == 82)
                {
                    if (buffer.readBuffer[start + 1] == 1 &&
                        Utils.ReadFromBinaryReader(buffer.readBuffer, start + 3) == "Say")
                    {
                        var text = Utils.ReadFromBinaryReader(buffer.readBuffer, start + 7);
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
                }

                if (_forward[buffer.whoAmI].Server.Name != _config.Name)
                {
                    _forward[buffer.whoAmI].Connection?.Client
                        ?.Send(buffer.readBuffer, start - 2, length + 2, SocketFlags.None);
                    return HookResult.Cancel;
                }
            }

            return _receiveDataHandler.Invoke(buffer, ref packetid, ref readoffset, ref start, ref length);
        }

        private void ServerLoop(object state)
        {
            var whoAmI = (int) state;
            while (_forward[whoAmI].Connection != null && _forward[whoAmI].Connection.Connected)
            {
                try
                {
                    if (_forward[whoAmI].Buffer[2] == 7 && !_forward[whoAmI].Init8)
                    {
                        _forward[whoAmI].Connection.Client.Send(new[]
                        {
                            (byte) 11, (byte) 0, (byte) 8,
                            (byte) 0, (byte) 0, (byte) 0, (byte) 0, // SpawnX here
                            (byte) 0, (byte) 0, (byte) 0, (byte) 0 // SpawnY here
                        });
                        _forward[whoAmI].Init8 = true;
                        _forward[whoAmI].Connection.Client.Send(new[]
                        {
                            (byte) 8, (byte) 0, (byte) 12, (byte) whoAmI,
                            (byte) 0, (byte) 0, (byte) 0, (byte) 0 // SpawnXY (short) here
                        });
                        _forward[whoAmI].Init12 = true;
                        NetMessage.SendData(65, -1, -1, NetworkText.Empty, 0, whoAmI, _forward[whoAmI].Server.SpawnX * 16,
                            _forward[whoAmI].Server.SpawnY * 16, 1);
                    }

                    Netplay.Clients[whoAmI].Socket.AsyncSend(_forward[whoAmI].Buffer, 0,
                        _forward[whoAmI].Connection.Client.Receive(_forward[whoAmI].Buffer), delegate { });
                }
                catch
                {
                }
            }

            _forward[whoAmI].Server = Server.Current;
        }
    }
}