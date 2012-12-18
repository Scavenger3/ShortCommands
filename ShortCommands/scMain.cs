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
using ShortCommands;
using System.Linq;
using System.Text;

namespace ShortCommands
{
    [APIVersion(1, 12)]
    public class ShortCommands : TerrariaPlugin
    {
        public static scConfig getConfig { get; set; }
        internal static string configPath { get { return Path.Combine(TShock.SavePath, "PluginConfigs", "ShortCommands.json"); } }
		internal static string configDir { get { return Path.Combine(TShock.SavePath, "PluginConfigs"); } }
		public static List<scPlayer> scPlayers { get; set; }
		public static Dictionary<string, Dictionary<string, DateTime>> offlineCooldowns { get; set; }

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
            get { return "Give your commands Aliases"; }
        }

        public override Version Version
        {
            get { return Assembly.GetExecutingAssembly().GetName().Version; }
        }

        public override void Initialize()
        {
            GameHooks.Initialize += onInitialize;
            ServerHooks.Chat += onChat;
			NetHooks.GreetPlayer += onJoin;
			ServerHooks.Leave += onLeave;
			WorldHooks.SaveWorld += onSave;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                GameHooks.Initialize -= onInitialize;
                ServerHooks.Chat -= onChat;
				NetHooks.GreetPlayer -= onJoin;
				ServerHooks.Leave -= onLeave;
				WorldHooks.SaveWorld -= onSave;
            }
            base.Dispose(disposing);
        }

        public ShortCommands(Main game) : base(game)
        {
			Order = -5;
            getConfig = new scConfig();
			scPlayers = new List<scPlayer>();
			offlineCooldowns = new Dictionary<string, Dictionary<string, DateTime>>();
        }

        public void onInitialize()
        {
            Commands.ChatCommands.Add(new Command("shortcmd", scConfig.CMDscmdrl, "scmdrl"));
			scConfig.SetupConfig();
        }

		#region onSave
		public void onSave(bool ResetTime, HandledEventArgs e)
		{
			lock (scPlayers)
			{
				foreach (var sPly in scPlayers)
				{
					lock (sPly.Cooldowns)
					{
						if (sPly != null && sPly.Cooldowns.Count > 0)
							sPly.removeOldCooldowns();
					}
				}
			}
			lock (offlineCooldowns)
			{
				var names = offlineCooldowns.Keys.ToList();
				foreach (var name in names)
				{
					foreach (var Command in ShortCommands.getConfig.Commands)
					{
						if (offlineCooldowns[name].ContainsKey(Command.alias))
						{
							if ((DateTime.UtcNow - offlineCooldowns[name][Command.alias]).TotalSeconds >= Command.cooldown)
							{
								offlineCooldowns[name].Remove(Command.alias);
							}
						}
					}
					if (offlineCooldowns[name].Count < 1)
						offlineCooldowns.Remove(name);
				}
			}
			return;
		}
		#endregion

		#region scPlayer
		public void onJoin(int who, HandledEventArgs args)
		{
			lock (scPlayers)
			{
				string name = TShock.Players[who].Name;
				scPlayer add = new scPlayer(who);
				if (offlineCooldowns.ContainsKey(name))
				{
					add.Cooldowns = offlineCooldowns[name];
					offlineCooldowns.Remove(name);
					add.removeOldCooldowns();
				}
				scPlayers.Add(add);
			}
		}
		public void onLeave(int who)
		{
			try
			{
				lock (scPlayers)
				{
					for (int i = 0; i < scPlayers.Count; i++)
					{
						if (scPlayers[i].Index == who)
						{
							var sPly = scPlayers[i];
							sPly.removeOldCooldowns();
							if (sPly.Cooldowns.Count > 0)
							{
								if (offlineCooldowns.ContainsKey(sPly.tsPly.Name))
									offlineCooldowns[sPly.tsPly.Name] = sPly.Cooldowns;
								else
									offlineCooldowns.Add(sPly.tsPly.Name, sPly.Cooldowns);
							}
							scPlayers.RemoveAt(i);
							break;
						}
					}
				}
			}
			catch { }
		}
		public static scPlayer getscPlayer(int who)
		{
			lock (scPlayers)
			{
				foreach (var sPly in scPlayers)
				{
					if (sPly.Index == who)
						return sPly;
				}
			}
			return null;
		}
		#endregion

		public void onChat(messageBuffer buff, int who, string text, HandledEventArgs e)
		{
			if (!text.StartsWith("/"))
				return;

			foreach (var command in getConfig.Commands)
			{
				if (text == command.alias || text.StartsWith(command.alias + " "))
				{
					e.Handled = true;
					var sPly = getscPlayer(who);
					if (!string.IsNullOrWhiteSpace(command.permission) && sPly.tsPly.Group.HasPermission(command.permission))
					{
						if (hasCooldown(sPly, command))
							return;
						parseCommand(getscPlayer(who), text, command, true);
					}
					else if (string.IsNullOrWhiteSpace(command.permission))
					{
						if (hasCooldown(sPly, command))
							return;
						parseCommand(getscPlayer(who), text, command, false);
					}
					else
						sPly.tsPly.SendMessage("You do not have permission to execute that command.", Color.Red);
				}
			}
		}

		#region hasCooldown
		public static bool hasCooldown(scPlayer sPly, scCommand command)
		{
			if (sPly.tsPly.Group.HasPermission("shortcmd.nocooldown") || command.cooldown < 1) return false;
			if (sPly.Cooldowns.ContainsKey(command.alias))
			{
				var seconds = (DateTime.UtcNow - sPly.Cooldowns[command.alias]).TotalSeconds;
				if (seconds < command.cooldown)
				{
					sPly.tsPly.SendMessage("You must wait another {0} seconds before using that command!".SFormat((int)(command.cooldown - seconds)), Color.Red);
					return true;
				}
			}
			return false;
		}
		#endregion

		#region parseCommand
		public static void parseCommand(scPlayer sPly, string Text, scCommand command, bool CanBypassPermissions)
		{
			List<string> Parameters = parseParameters(Text, true);
			Parameters.RemoveAt(0); //remove the cmd alias (/cmd)

			List<Command> validCommands = new List<Command>();
			List<string> validText = new List<string>();
			List<List<string>> validArgs = new List<List<string>>();

			bool IsError = false;
			int maxParameters = 0;
			List<string> invalidCommands = new List<string>();
			List<string> cantRun = new List<string>();
			List<string> formatException = new List<string>();
			foreach (var executeCmd in command.commands)
			{
				//Commands.HandleCommand(sPly.tsPly, string.Format(cmd, Parameters));

				//Custom Handle:
				#region Custom Handle
				string cmdText = executeCmd.Remove(0, 1);

				int maxParams = maxFormat(cmdText) + 1;
				if (Parameters.Count >= maxParams)
				{
					try
					{
						cmdText = string.Format(cmdText, Parameters.ToArray());
					}
					catch (FormatException)
					{
						formatException.Add(string.Concat("/", (cmdText.Contains(' ') ? cmdText.Split(' ')[0] : cmdText)));
						continue;
					}
				}
				else
				{
					IsError = true;
					if (maxParams > maxParameters)
						maxParameters = maxParams;
					continue;
				}

				var args = parseParameters(cmdText);
				if (args.Count < 1 || string.IsNullOrWhiteSpace(cmdText))
					continue;

				string cmdName = args[0].ToLower();
				args.RemoveAt(0);

				for (int i = 0; i < args.Count; i++)
				{
					if (args[i].ToLower() == "%name")
						args[i] = string.Format("\"{0}\"", sPly.tsPly.Name);
					else if (args[i].ToLower() == "%account")
						args[i] = string.Format("\"{0}\"", sPly.tsPly.UserAccountName) ?? string.Empty;
					else if (args[i].ToLower() == "%group")
						args[i] = string.Format("\"{0}\"", sPly.tsPly.Group.Name);
				}

				IEnumerable<Command> cmds = Commands.ChatCommands.Where(c => c.HasAlias(cmdName));

				if (cmds.Count() == 0)
				{
					if (sPly.tsPly.AwaitingResponse.ContainsKey(cmdName))
					{
						Action<CommandArgs> call = sPly.tsPly.AwaitingResponse[cmdName];
						sPly.tsPly.AwaitingResponse.Remove(cmdName);
						call(new CommandArgs(cmdText, sPly.tsPly, args));
						continue;
					}
					IsError = true;
					invalidCommands.Add(string.Concat("/", (cmdText.Contains(' ') ? cmdText.Split(' ')[0] : cmdText)));
					continue;
				}
				foreach (Command cmd in cmds)
				{
					if (!CanBypassPermissions && !cmd.CanRun(sPly.tsPly))
					{
						IsError = true;
						cantRun.Add(string.Concat("/", cmdText));
						break;

					}
					else if (!cmd.AllowServer && !sPly.tsPly.RealPlayer)
						break;
					else
					{
						validCommands.Add(cmd);
						validText.Add(cmdText);
						validArgs.Add(args);
					}
				}
				#endregion
			}
			if (validCommands.Count > 0)
			{
				handleCommad(sPly, Text, validCommands, validText, validArgs, CanBypassPermissions);
				if (sPly.Cooldowns.ContainsKey(command.alias))
					sPly.Cooldowns[command.alias] = DateTime.UtcNow;
				else
					sPly.Cooldowns.Add(command.alias, DateTime.UtcNow);
			}
			if (IsError)
			{
				if (maxParameters > 0)
					sPly.tsPly.SendErrorMessage("{0} parameter{1} required.".SFormat(maxParameters, (maxParameters > 1 ? "s are" : " is")));
				if (invalidCommands.Count > 0)
					sPly.tsPly.SendErrorMessage("Invalid command{0} {1}.".SFormat((invalidCommands.Count > 1 ? "s" : string.Empty), string.Join(", ", invalidCommands)));
				if (cantRun.Count > 0)
					sPly.tsPly.SendErrorMessage("You do not have permission to execute {0}.".SFormat(string.Join(", ", cantRun)));
				if (formatException.Count > 0)
					sPly.tsPly.SendErrorMessage("Format Exception in command{0} {1}.".SFormat((formatException.Count > 1 ? "s" : string.Empty), string.Join(", ", formatException)));
			}
		}
		#endregion

		#region handleCommand
		public static void handleCommad(scPlayer sPly, string Text, List<Command> validCommands, List<string> validText, List<List<string>> validArgs, bool CanBypassPermissions)
		{
			TShock.Utils.SendLogs(string.Format("{0} executed ShortCommand: {1}.", sPly.tsPly.Name, Text), Color.Red);
			for (int i = 0; i < validCommands.Count; i++)
			{
				Command cmd = validCommands[i];
				string cmdText = validText[i];
				List<string> args = validArgs[i];
				if (CanBypassPermissions && !sPly.tsPly.Group.HasPermission(cmd.Permission))
				{
					var oldGroup = sPly.tsPly.Group;
					sPly.tsPly.Group = new SuperAdminGroup();
					cmd.Run(cmdText, sPly.tsPly, args);
					sPly.tsPly.Group = oldGroup;
				}
				else
					cmd.Run(cmdText, sPly.tsPly, args);
			}
		}
		#endregion

		#region maxFormat
		public static int maxFormat(string text)
		{
			if (text.Contains('{') && text.Contains('}'))
			{
				int start = -1;
				int len = -1;
				int max = -1;
				for (int i = 0; i < text.Length; i++)
				{
					if (text[i] == '{')
						start = i + 1;
					if (text[i] == '}' && start > -1)
					{
						len = i - start;
						string val = text.Substring(start, len);

						if (val.Contains(','))
							val = val.Split(',')[0];
						else if (val.Contains(':'))
							val = val.Split(':')[0];

						int newmax;
						if (int.TryParse(val, out newmax) && newmax > max)
							max = newmax;
					}
				}
				return max;
			}
			return -1;
		}
		#endregion

		#region parseParameters
		private static List<String> parseParameters(string str, bool includeQuotes = false)
		{
			var ret = new List<string>();
			var sb = new StringBuilder();
			bool instr = false;
			for (int i = 0; i < str.Length; i++)
			{
				char c = str[i];

				if (instr)
				{
					if (c == '\\')
					{
						if (i + 1 >= str.Length)
							break;
						c = getEscape(str[++i]);
					}
					else if (c == '"')
					{
						if (includeQuotes)
							sb.Append(c);
						ret.Add(sb.ToString());
						sb.Clear();
						instr = false;
						continue;
					}
					sb.Append(c);
				}
				else
				{
					if (isWhiteSpace(c))
					{
						if (sb.Length > 0)
						{
							ret.Add(sb.ToString());
							sb.Clear();
						}
					}
					else if (c == '"')
					{
						if (sb.Length > 0)
						{
							ret.Add(sb.ToString());
							sb.Clear();
						}
						instr = true;
						if (includeQuotes)
							sb.Append(c);
					}
					else
					{
						sb.Append(c);
					}
				}
			}
			if (sb.Length > 0)
				ret.Add(sb.ToString());

			return ret;
		}
		private static char getEscape(char c)
		{
			switch (c)
			{
				case '\\':
					return '\\';
				case '"':
					return '"';
				case 't':
					return '\t';
				default:
					return c;
			}
		}
		private static bool isWhiteSpace(char c)
		{
			return c == ' ' || c == '\t' || c == '\n';
		}
		#endregion
	}
}