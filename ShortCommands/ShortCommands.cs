using System;
using System.Collections.Generic;
using System.Reflection;
using System.Drawing;
using Terraria;
using Hooks;
using TShockAPI;
using TShockAPI.DB;
using System.ComponentModel;
using System.IO;
using Newtonsoft.Json;
using Config;
using ShortCommands;
using System.Linq;

namespace ShortCommands
{
    [APIVersion(1, 12)]
    public class ShortCommands : TerrariaPlugin
    {
        public static dsConfig getConfig { get; set; }
        internal static string ConfigPath { get { return Path.Combine(TShock.SavePath, "PluginConfigs/ShortCommands.json"); } }
        
        public override string Name
        {
            get { return "ShortCommands"; }
        }

        public override string Author
        {
            get { return "by Scavenger"; }
        }

        public override string Description
        {
            get { return "Give your commands Alias's"; }
        }

        public override Version Version
        {
            get { return new Version("1.0.3"); }
        }

        public override void Initialize()
        {
            GameHooks.Initialize += OnInitialize;
            ServerHooks.Chat += OnChat;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                GameHooks.Initialize -= OnInitialize;
                ServerHooks.Chat += OnChat;
            }
            base.Dispose(disposing);
        }

        public ShortCommands(Main game)
            : base(game)
        {
            Order = -1;
            getConfig = new dsConfig();
        }

        public void OnInitialize()
        {
            Commands.ChatCommands.Add(new Command("shortcmd", scmd, "scmdrl"));
            SetupConfig();
        }

        #region Config
        public static void SetupConfig()
        {
            try
            {
                if (!File.Exists(ConfigPath))
                    NewConfig();
                getConfig = dsConfig.Read(ConfigPath);
                getConfig.Write(ConfigPath);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Error in ShortCommands config file");
                Console.ForegroundColor = ConsoleColor.Gray;
                Log.Error("Config Exception in ShortCommands Config file");
                Log.Error(ex.ToString());
            }
        }
        #endregion Config

        #region Config Reload
        public static void scmd(CommandArgs args)
        {
            try
            {
                if (!File.Exists(ConfigPath))
                    NewConfig();
                getConfig = dsConfig.Read(ConfigPath);
                getConfig.Write(ConfigPath);
                args.Player.SendMessage("Config file reloaded sucessfully!", Color.Green);
            }
            catch (Exception ex)
            {
                args.Player.SendMessage("Error in config file! Check log for more details.", Color.Red);
                Log.Error("Config Exception in ShortCommands Config file");
                Log.Error(ex.ToString());
            }
        }
        #endregion Config Reload

        #region Generate New Config
        public static void NewConfig()
        {
            File.WriteAllText(ConfigPath,
            "{" + Environment.NewLine +
            "  \"Commands\": [" + Environment.NewLine +
            "    {" + Environment.NewLine +
            "      \"alias\": \"/rname\"," + Environment.NewLine +
            "      \"commands\": [" + Environment.NewLine +
            "        \"/region name\"" + Environment.NewLine +
            "      ]," + Environment.NewLine +
            "      \"permission\": \"regionname\"" + Environment.NewLine +
            "    }," + Environment.NewLine +
            "    {" + Environment.NewLine +
            "      \"alias\": \"/buffme\"," + Environment.NewLine +
            "      \"commands\": [" + Environment.NewLine +
            "        \"/buff 1 500\"," + Environment.NewLine +
            "        \"/buff 2 500\"," + Environment.NewLine +
            "        \"/buff 3 500\"," + Environment.NewLine +
            "        \"/buff 5 500\"," + Environment.NewLine +
            "        \"/buff 11 500\"" + Environment.NewLine +
            "      ]," + Environment.NewLine +
            "      \"permission\": \"\"" + Environment.NewLine +
            "    }," + Environment.NewLine +
            "    {" + Environment.NewLine +
            "      \"alias\": \"/startgame\"," + Environment.NewLine +
            "      \"commands\": [" + Environment.NewLine +
            "        \"/hidewarp game false\"," + Environment.NewLine +
            "        \"/bc A new game is about to start!\"," + Environment.NewLine +
            "        \"/bc type /game to teleport there!\"" + Environment.NewLine +
            "      ]," + Environment.NewLine +
            "      \"permission\": \"\"" + Environment.NewLine +
            "    }," + Environment.NewLine +
            "	{" + Environment.NewLine +
            "      \"alias\": \"/endgame\"," + Environment.NewLine +
            "      \"commands\": [" + Environment.NewLine +
            "      \"/hidewarp game true\"," + Environment.NewLine +
            "      \"/bc The game has finished!\"" + Environment.NewLine +
            "    ]," + Environment.NewLine +
            "      \"permission\": \"\"" + Environment.NewLine +
            "    }," + Environment.NewLine +
            "  ]" + Environment.NewLine +
            "}");
        }
        #endregion

        public void OnChat(messageBuffer buff, int who, string text, HandledEventArgs e)
        {
            if (!text.StartsWith("/"))
                return;

            foreach (var Command in getConfig.Commands)
            {
                if (text == cCmd(Command.alias) || text.StartsWith(cCmd(Command.alias) + " "))
                {
                    e.Handled = true;
                    TSPlayer ply = TShock.Players[who];
                    if (Command.permission != "" && ply.Group.HasPermission(Command.permission))
                    {
                        var OldGroup = ply.Group;
                        ply.Group = new SuperAdminGroup();
                        foreach (var cmd in Command.commands)
                            Commands.HandleCommand(ply, cCmd(cmd) + text.Remove(0, Command.alias.Length));
                        ply.Group = OldGroup;
                    }
                    else if (Command.permission == "")
                    {
                        foreach (var cmd in Command.commands)
                            Commands.HandleCommand(ply, cCmd(cmd) + text.Remove(0, cCmd(Command.alias).Length));
                    }
                    else
                        ply.SendMessage("You do not have access to that command.", Color.Red);
                }
            }
        }

        public static string cCmd(string command)
        {
            if (!(command.StartsWith("/")))
                return "/" + command;
            return command;
        }
    }

    public class ShortCommand
    {
        public string alias;
        public string[] commands;
        public string permission;

        public ShortCommand(string alias, string[] commands, string permission)
        {
            this.alias = alias;
            this.commands = commands;
            this.permission = permission;
        }
    }
}

namespace Config
{
    public class dsConfig
    {
        public List<ShortCommand> Commands = new List<ShortCommand> ();

        public static dsConfig Read(string path)
        {
            if (!Directory.Exists(@"tshock/PluginConfigs"))
                Directory.CreateDirectory(@"tshock/PluginConfigs");
            if (!File.Exists(path))
                return new dsConfig();
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                return Read(fs);
            }
        }

        public static dsConfig Read(Stream stream)
        {
            using (var sr = new StreamReader(stream))
            {
                var cf = JsonConvert.DeserializeObject<dsConfig>(sr.ReadToEnd());
                if (ConfigRead != null)
                    ConfigRead(cf);
                return cf;
            }
        }
        public void Write(string path)
        {
            if (!Directory.Exists(@"tshock/PluginConfigs"))
                Directory.CreateDirectory(@"tshock/PluginConfigs");
            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Write))
            {
                Write(fs);
            }
        }

        public void Write(Stream stream)
        {
            var str = JsonConvert.SerializeObject(this, Formatting.Indented);
            using (var sw = new StreamWriter(stream))
            {
                sw.Write(str);
            }
        }

        public static Action<dsConfig> ConfigRead;
    }
}