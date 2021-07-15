using System;
using System.IO;
using System.Text.RegularExpressions;

namespace StatsDump
{
	public static class Util
	{
		public static void LogError(Exception ex)
		{
			try
			{
				using (StreamWriter writer = new StreamWriter(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + @"\Decal Plugins\" + Globals.PluginName + "\\errors.txt", true))
				{
					writer.WriteLine("============================================================================");
					writer.WriteLine(DateTime.Now.ToString());
					writer.WriteLine("Error: " + ex.Message);
					writer.WriteLine("Source: " + ex.Source);
					writer.WriteLine("Stack: " + ex.StackTrace);
					if (ex.InnerException != null)
					{
						writer.WriteLine("Inner: " + ex.InnerException.Message);
						writer.WriteLine("Inner Stack: " + ex.InnerException.StackTrace);
					}
                    //writer.WriteLine(ex.ToString());
					writer.WriteLine("============================================================================");
					writer.WriteLine("");
					writer.Close();
				}
			}
			catch
			{
			}
		}

        public static void LogError(string error)
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + @"\Decal Plugins\" + Globals.PluginName + "\\errors.txt", true))
                {
                    writer.WriteLine("============================================================================");
                    writer.WriteLine(DateTime.Now.ToString());
                    writer.WriteLine("Error: " + error);
                    writer.WriteLine("============================================================================");
                    writer.WriteLine("");
                    writer.Close();
                }
            }
            catch
            {
            }
        }
        public static void WriteToChat(string message, int textcolor = 5)
		{
			try
			{
                //0 is green
                //1: is also green
                //2:
                //3:
                //4:
                //hot pink is 5
                //red is 6
                //lt blue is 7
                //lt red is 8
                Globals.Host.Actions.AddChatText("<{" + Globals.PluginName + "}>: " + message, 8); // + " [color " + textcolor.ToString() + "]", textcolor);
			}
			catch (Exception ex) { LogError(ex); }
		}

        // http://www.regular-expressions.info/reference.html

        // Local Chat
        // You say, "test"
        private static readonly Regex YouSay = new Regex("^You say, \"(?<msg>.*)\"$");
        // <Tell:IIDString:1343111160:PlayerName>PlayerName<\Tell> says, "asdf"
        private static readonly Regex PlayerSaysLocal = new Regex("^<Tell:IIDString:[0-9]+:(?<name>[\\w\\s'-]+)>[\\w\\s'-]+<\\\\Tell> says, \"(?<msg>.*)\"$");
        //
        // Master Arbitrator says, "Arena Three is now available for new warriors!"
        private static readonly Regex NpcSays = new Regex("^(?<name>[\\w\\s'-]+) says, \"(?<msg>.*)\"$");

        // Channel Chat
        // [Allegiance] <Tell:IIDString:0:PlayerName>PlayerName<\Tell> says, "kk"
        // [General] <Tell:IIDString:0:PlayerName>PlayerName<\Tell> says, "asdfasdfasdf"
        // [Fellowship] <Tell:IIDString:0:PlayerName>PlayerName<\Tell> says, "test"
        private static readonly Regex PlayerSaysChannel = new Regex("^\\[(?<channel>.+)]+ <Tell:IIDString:[0-9]+:(?<name>[\\w\\s'-]+)>[\\w\\s'-]+<\\\\Tell> says, \"(?<msg>.*)\"$");
        //
        // [Fellowship] <Tell:IIDString:0:Master Arbitrator>Master Arbitrator<\Tell> says, "Good Luck!"

        // Tells
        // You tell PlayerName, "test"
        private static readonly Regex YouTell = new Regex("^You tell .+, \"(?<msg>.*)\"$");
        // <Tell:IIDString:1343111160:PlayerName>PlayerName<\Tell> tells you, "test"
        private static readonly Regex PlayerTellsYou = new Regex("^<Tell:IIDString:[0-9]+:(?<name>[\\w\\s'-]+)>[\\w\\s'-]+<\\\\Tell> tells you, \"(?<msg>.*)\"$");
        //
        // Master Arbitrator tells you, "You fought in the Colosseum's Arenas too recently. I cannot reward you for 4s."
        private static readonly Regex NpcTellsYou = new Regex("^(?<name>[\\w\\s'-]+) tells you, \"(?<msg>.*)\"$");

        [Flags]
        public enum ChatFlags : byte
        {
            None = 0x00,

            PlayerSaysLocal = 0x01,
            PlayerSaysChannel = 0x02,
            YouSay = 0x04,

            PlayerTellsYou = 0x08,
            YouTell = 0x10,

            NpcSays = 0x20,
            NpcTellsYou = 0x40,

            All = 0xFF,
        }

        /// <summary>
        /// Returns true if the text was said by a person, envoy, npc, monster, etc..
        /// </summary>
        /// <param name="text"></param>
        /// <param name="chatFlags"></param>
        /// <returns></returns>
        public static bool IsChat(string text, ChatFlags chatFlags = ChatFlags.All)
        {
            if ((chatFlags & ChatFlags.PlayerSaysLocal) == ChatFlags.PlayerSaysLocal && PlayerSaysLocal.IsMatch(text))
                return true;

            if ((chatFlags & ChatFlags.PlayerSaysChannel) == ChatFlags.PlayerSaysChannel && PlayerSaysChannel.IsMatch(text))
                return true;

            if ((chatFlags & ChatFlags.YouSay) == ChatFlags.YouSay && YouSay.IsMatch(text))
                return true;


            if ((chatFlags & ChatFlags.PlayerTellsYou) == ChatFlags.PlayerTellsYou && PlayerTellsYou.IsMatch(text))
                return true;

            if ((chatFlags & ChatFlags.YouTell) == ChatFlags.YouTell && YouTell.IsMatch(text))
                return true;


            if ((chatFlags & ChatFlags.NpcSays) == ChatFlags.NpcSays && NpcSays.IsMatch(text))
                return true;

            if ((chatFlags & ChatFlags.NpcTellsYou) == ChatFlags.NpcTellsYou && NpcTellsYou.IsMatch(text))
                return true;

            return false;
        }

	}
}
