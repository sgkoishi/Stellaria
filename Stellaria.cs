using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using Newtonsoft.Json;
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

        private Config config;

        public Stellaria(Main game) : base(game)
        {
        }

        public override string Author => "SGKoishi";
        public override string Name => "Stellaria";
        public override Version Version => new Version(1, 0, 0, 0);
        public override string Description => "In-game multi world plugin";

        public override void Initialize()
        {
            _receiveDataHandler = Hooks.Net.ReceiveData;
            Hooks.Net.ReceiveData = ReceiveData;
            ServerApi.Hooks.ServerLeave.Register(this, args =>
                _forward[args.Who] = new ForwardPlayer());
            ReadConfig("tshock\\stellaria.json", new Config(), out config);
            Commands.ChatCommands.Add(new Command("chireiden.stellaria.use", SwitchVerse, "sv"));
        }

        private void SwitchVerse(CommandArgs args)
        {
            var nv = int.Parse(args.Parameters[0]);
            if (_forward[args.TPlayer.whoAmI].Server != nv)
            {
                args.Player.SendInfoMessage("Switch to " + nv);
                _forward[args.TPlayer.whoAmI].Server = nv;
                var tc = new TcpClient();
                tc.Connect(config.Servers[_forward[args.TPlayer.whoAmI].Server].Address,
                    config.Servers[_forward[args.TPlayer.whoAmI].Server].Port);
                tc.Client.Send(config.JoinBytes);
                _forward[args.TPlayer.whoAmI].Connection = tc;
                _forward[args.TPlayer.whoAmI].Buffer = new byte[1024];
                ThreadPool.QueueUserWorkItem(InterserverLoop, args.TPlayer.whoAmI);
            }
        }

        private HookResult ReceiveData(MessageBuffer buffer, ref byte packetid, ref int readoffset, ref int start,
            ref int length)
        {
            if (packetid == 1)
            {
                _forward[buffer.whoAmI] = new ForwardPlayer();
                return _receiveDataHandler.Invoke(buffer, ref packetid, ref readoffset, ref start, ref length);
            }

            if (_forward[buffer.whoAmI].Server == -1)
            {
                return _receiveDataHandler.Invoke(buffer, ref packetid, ref readoffset, ref start, ref length);
            }

            // TODO: Handle some command (e.g. /lobby) if necessary
            _forward[buffer.whoAmI].Connection?.Client
                ?.Send(buffer.readBuffer, start - 2, length + 2, SocketFlags.None);
            return HookResult.Cancel;
        }

        private void InterserverLoop(object state)
        {
            var wai = (int) state;
            while (_forward[wai]?.Connection != null && _forward[wai].Connection.Connected)
            {
                var r = _forward[wai].Connection.Client.Receive(_forward[wai].Buffer);
                if (_forward[wai].Buffer[2] == 7 && !_forward[wai].Init8)
                {
                    // TODO: Create a BinaryWriter to Write(int) instead of % and >>
                    _forward[wai].Connection.Client.Send(new[]
                    {
                        (byte) 11, (byte) 0, (byte) 8,
                        (byte) (config.Servers[_forward[wai].Server].SpawnX % 256 % 256 % 256),
                        (byte) ((config.Servers[_forward[wai].Server].SpawnX >> 8) % 256 % 256),
                        (byte) ((config.Servers[_forward[wai].Server].SpawnX >> 16) % 256),
                        (byte) (config.Servers[_forward[wai].Server].SpawnX >> 24),
                        (byte) (config.Servers[_forward[wai].Server].SpawnY % 256 % 256 % 256),
                        (byte) ((config.Servers[_forward[wai].Server].SpawnY >> 8) % 256 % 256),
                        (byte) ((config.Servers[_forward[wai].Server].SpawnY >> 16) % 256),
                        (byte) (config.Servers[_forward[wai].Server].SpawnY >> 24)
                    });
                    _forward[wai].Init8 = true;
                    _forward[wai].Connection.Client.Send(new[]
                    {
                        (byte) 8, (byte) 0, (byte) 12, (byte) wai,
                        (byte) (config.Servers[_forward[wai].Server].SpawnX % 256 % 256 % 256),
                        (byte) ((config.Servers[_forward[wai].Server].SpawnX >> 8) % 256 % 256),
                        (byte) (config.Servers[_forward[wai].Server].SpawnY % 256 % 256 % 256),
                        (byte) ((config.Servers[_forward[wai].Server].SpawnY >> 8) % 256 % 256)
                    });
                    _forward[wai].Init12 = true;
                }

                Netplay.Clients[wai].Socket.AsyncSend(_forward[wai].Buffer, 0, r, delegate { });
            }
        }

        private static void ReadConfig<TConfig>(string path, TConfig defaultConfig, out TConfig config)
        {
            if (!File.Exists(path))
            {
                config = defaultConfig;
                File.WriteAllText(path, JsonConvert.SerializeObject(config, Formatting.Indented));
            }
            else
            {
                config = JsonConvert.DeserializeObject<TConfig>(File.ReadAllText(path));
                File.WriteAllText(path, JsonConvert.SerializeObject(config, Formatting.Indented));
            }
        }
    }
}