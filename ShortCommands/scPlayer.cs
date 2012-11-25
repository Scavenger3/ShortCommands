using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TShockAPI;

namespace ShortCommands
{
	public class scPlayer
	{
		public int Index { get; set; }
		public TSPlayer tsPly { get { return TShock.Players[Index]; } }
		public Dictionary<string, DateTime> Cooldowns { get; set; }

		public scPlayer(int who)
		{
			this.Index = who;
			this.Cooldowns = new Dictionary<string, DateTime>();
		}

		public void removeOldCooldowns()
		{
			foreach (var Command in ShortCommands.getConfig.Commands)
			{
				if (this.Cooldowns.ContainsKey(Command.alias))
				{
					if ((DateTime.UtcNow - this.Cooldowns[Command.alias]).TotalSeconds >= Command.cooldown)
					{
						Cooldowns.Remove(Command.alias);
					}
				}
			}
		}
	}
}
