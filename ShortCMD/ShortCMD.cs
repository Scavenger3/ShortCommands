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

namespace ShortCMD
{
    [APIVersion(1, 11)]
    public class ShortCMD : TerrariaPlugin
    {
        public static dsConfig getConfig { get; set; }
        internal static string getConfigPath { get { return Path.Combine(TShock.SavePath, "PluginConfigs/ShortCommands.json"); } }

        public override string Name
        {
            get { return "Short Commands"; }
        }

        public override string Author
        {
            get { return "by Scavenger"; }
        }

        public override string Description
        {
            get { return "Make your own Commands!"; }
        }

        public override Version Version
        {
            get { return new Version("1.0.1"); }
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

        public ShortCMD(Main game)
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
                if (File.Exists(getConfigPath))
                {
                    getConfig = dsConfig.Read(getConfigPath);
                }
                getConfig.Write(getConfigPath);
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
                if (File.Exists(getConfigPath))
                {
                    getConfig = dsConfig.Read(getConfigPath);
                    args.Player.SendMessage("Config file reloaded sucessfully!", Color.Green);
                }
                getConfig.Write(getConfigPath);
            }
            catch (Exception ex)
            {
                args.Player.SendMessage("Error in config file! Check log for more details.", Color.Red);
                Log.Error("Config Exception in ShortCommands Config file");
                Log.Error(ex.ToString());
            }
        }
        #endregion Config Reload

        public void OnChat(messageBuffer buff, int who, string text, HandledEventArgs e)
        {
            if (!text.StartsWith("/"))
                return;

            foreach (var Pair in getConfig.Commands)
            {
                if (Pair.Key == text)
                {
                    e.Handled = true;
                    foreach (var cmd in Pair.Value)
                    {
                        Commands.HandleCommand(TShock.Players[who], cmd);
                    }
                }
            }
        }
    }
}

namespace Config
{
    public class dsConfig
    {
        public Dictionary<string, string[]> Commands = new Dictionary<string, string[]> ();

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