using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using TShockAPI;

namespace ShortCommands
{
	public class scConfig
	{
		public List<scCommand> Commands = new List<scCommand>();

		public static scConfig Read(string path)
		{
			if (!File.Exists(path))
				return new scConfig();
			using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
			{
				return Read(fs);
			}
		}

		public static scConfig Read(Stream stream)
		{
			using (var sr = new StreamReader(stream))
			{
				var cf = JsonConvert.DeserializeObject<scConfig>(sr.ReadToEnd());
				if (ConfigRead != null)
					ConfigRead(cf);
				return cf;
			}
		}
		public void Write(string path)
		{
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

		public static Action<scConfig> ConfigRead;

		#region Config
		public static void SetupConfig()
		{
			try
			{
				baseReload();
			}
			catch (Exception ex)
			{
				Log.ConsoleError("Exception in ShortCommands Config file");
				Log.Error(ex.ToString());
			}
		}
		#endregion Config

		#region Config Reload
		public static void CMDscmdrl(CommandArgs args)
		{
			try
			{
				baseReload();
				args.Player.SendSuccessMessage("Config file reloaded sucessfully!");
			}
			catch (Exception ex)
			{
				args.Player.SendErrorMessage("Error in config file! Check log for more details.");
				Log.Error("Config Exception in ShortCommands Config file");
				Log.Error(ex.ToString());
			}
		}
		#endregion Config Reload

		#region base
		private static void baseReload()
		{
			if (!Directory.Exists(ShortCommands.configDir))
				Directory.CreateDirectory(ShortCommands.configDir);
			if (!File.Exists(ShortCommands.configPath))
				NewConfig();
			ShortCommands.getConfig = scConfig.Read(ShortCommands.configPath);
			ShortCommands.getConfig.Write(ShortCommands.configPath);
		}
		#endregion

		#region Generate New Config
		public static void NewConfig()
		{
			File.WriteAllText(ShortCommands.configPath,
			"{" + Environment.NewLine +
			"  \"Commands\": [" + Environment.NewLine +
			"    {" + Environment.NewLine +
			"      \"alias\": \"/rname\"," + Environment.NewLine +
			"      \"commands\": [" + Environment.NewLine +
			"        \"/region name\"" + Environment.NewLine +
			"      ]," + Environment.NewLine +
			"      \"permission\": \"regionname\"," + Environment.NewLine +
			"      \"cooldown\": 0" + Environment.NewLine +
			"    }," + Environment.NewLine +
			"    {" + Environment.NewLine +
			"      \"alias\": \"/buffme\"," + Environment.NewLine +
			"      \"commands\": [" + Environment.NewLine +
			"        \"/buff 1 {0}\"," + Environment.NewLine +
			"        \"/buff 2 {0}\"," + Environment.NewLine +
			"        \"/buff 3 {0}\"," + Environment.NewLine +
			"        \"/buff 5 {0}\"," + Environment.NewLine +
			"        \"/buff 11 {0}\"" + Environment.NewLine +
			"      ]," + Environment.NewLine +
			"      \"permission\": \"buffme\"," + Environment.NewLine +
			"      \"cooldown\": 60" + Environment.NewLine +
			"    }," + Environment.NewLine +
			"    {" + Environment.NewLine +
			"      \"alias\": \"/startgame\"," + Environment.NewLine +
			"      \"commands\": [" + Environment.NewLine +
			"        \"/hidewarp game false\"," + Environment.NewLine +
			"        \"/bc A new game is about to start!\"," + Environment.NewLine +
			"        \"/bc type /game to teleport there!\"" + Environment.NewLine +
			"      ]," + Environment.NewLine +
			"      \"permission\": \"\"," + Environment.NewLine +
			"      \"cooldown\": 0" + Environment.NewLine +
			"    }," + Environment.NewLine +
			"	{" + Environment.NewLine +
			"      \"alias\": \"/endgame\"," + Environment.NewLine +
			"      \"commands\": [" + Environment.NewLine +
			"      \"/hidewarp game true\"," + Environment.NewLine +
			"      \"/bc The game has finished!\"" + Environment.NewLine +
			"    ]," + Environment.NewLine +
			"      \"permission\": \"\"," + Environment.NewLine +
			"      \"cooldown\": 0" + Environment.NewLine +
			"    }," + Environment.NewLine +
			"  ]" + Environment.NewLine +
			"}");
		}
		#endregion
	}
}
