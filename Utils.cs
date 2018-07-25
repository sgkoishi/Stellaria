using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using TShockAPI;

namespace Chireiden.Stellaria
{
    public class Utils
    {
        private static readonly MethodInfo _parseParameters =
            typeof(Commands).GetMethod("ParseParameters", BindingFlags.NonPublic | BindingFlags.Static);

        internal static FieldInfo CacheIP =
            typeof(TSPlayer).GetField("CacheIP", BindingFlags.Instance | BindingFlags.NonPublic);

        public static List<string> ParseParameters(string text)
        {
            return (List<string>) _parseParameters?.Invoke(null, new object[] {text.Remove(0, 1)});
        }

        private static Random rng = new Random();
        public static byte[] RandomKey(int length)
        {
            var ret = new byte[length];
            rng.NextBytes(ret);
            return ret;
        }

        public static void ReadConfig<TConfig>(string path, TConfig defaultConfig, out TConfig config)
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

        public static void Pli(TSPlayer player, string text, bool silent = false)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            var args = ParseParameters(text);
            if (args == null || args.Count < 1)
            {
                return;
            }

            var cmdName = args[0].ToLower();
            args.RemoveAt(0);
            var cmds = Commands.ChatCommands.Where(c => c.HasAlias(cmdName));
            if (!cmds.Any())
            {
                if (player.AwaitingResponse.ContainsKey(cmdName))
                {
                    Action<CommandArgs> call = player.AwaitingResponse[cmdName];
                    player.AwaitingResponse.Remove(cmdName);
                    call(new CommandArgs(text.Remove(0, 1), player, args));
                    return;
                }

                player.SendErrorMessage("Invalid command entered. Type /help for a list of valid commands.");
                return;
            }

            foreach (var cmd in cmds)
            {
                if (!cmd.AllowServer && !player.RealPlayer)
                {
                    player.SendErrorMessage("You must use this command in-game.");
                }
                else
                {
                    if (cmd.DoLog && silent == false)
                    {
                        TShock.Utils.SendLogs($"{player.Name} executed: /{text.Remove(0, 1)}.", Color.Red);
                    }

                    try
                    {
                        cmd.CommandDelegate(new CommandArgs(text.Remove(0, 1), false, player, args));
                    }
                    catch (Exception e)
                    {
                        player.SendErrorMessage("Command failed, check logs for more details.");
                        TShock.Log.Error(e.ToString());
                    }
                }
            }
        }
    }
}