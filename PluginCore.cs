using System;
using System.Collections.Generic;
using WindowsTimer = System.Windows.Forms.Timer;
using System.Xml;
using Microsoft.Win32;
using System.IO;

using Decal.Adapter;
using Decal.Adapter.Wrappers;
using MyClasses.MetaViewWrappers;


/*
 * Created by Mag-nus. 8/19/2011, VVS added by Virindi-Inquisitor.
 * 
 * No license applied, feel free to use as you wish. H4CK TH3 PL4N3T? TR45H1NG 0UR R1GHT5? Y0U D3C1D3!
 * 
 * Notice how I use try/catch on every function that is called or raised by decal (by base events or user initiated events like buttons, etc...).
 * This is very important. Don't crash out your users!
 * 
 * In 2.9.6.4+ Host and Core both have Actions objects in them. They are essentially the same thing.
 * You sould use Host.Actions though so that your code compiles against 2.9.6.0 (even though I reference 2.9.6.5 in this project)
 * 
 * If you add this plugin to decal and then also create another plugin off of this sample, you will need to change the guid in
 * Properties/AssemblyInfo.cs to have both plugins in decal at the same time.
 * 
 * If you have issues compiling, remove the Decal.Adapater and VirindiViewService references and add the ones you have locally.
 * Decal.Adapter should be in C:\Games\Decal 3.0\
 * VirindiViewService should be in C:\Games\VirindiPlugins\VirindiViewService\
*/

namespace StatsDump
{

    
    //Attaches events from core
	[WireUpBaseEvents]

    //View (UI) handling
    [MVView("StatsDump.mainView.xml")]
    [MVWireUpControlEvents]

	// FriendlyName is the name that will show up in the plugins list of the decal agent (the one in windows, not in-game)
	// View is the path to the xml file that contains info on how to draw our in-game plugin. The xml contains the name and icon our plugin shows in-game.
	// The view here is SamplePlugin.mainView.xml because our projects default namespace is SamplePlugin, and the file name is mainView.xml.
	// The other key here is that mainView.xml must be included as an embeded resource. If its not, your plugin will not show up in-game.
    [FriendlyName("StatsDump")]
	public class PluginCore : PluginBase
	{
		/// <summary>
		/// This is called when the plugin is started up. This happens only once.
		/// </summary>
        /// 
        private WindowsTimer tDumpTimer;
        long current_luminance = 0; //stores the current luminance values...
        long max_luminance = 0; //stores the max luminance value...
        bool contract_stipend_found = false;
        string stipend_start = ""; //stores the login date/time...
        double stipend_timer = 0; //stores the stipend timer at login...
        bool additionalStipendTimer = false; // sets a flag to look for the additional timer (per month)

		protected override void Startup()
		{
			try
			{
				// This initializes our static Globals class with references to the key objects your plugin will use, Host and Core.
				// The OOP way would be to pass Host and Core to your objects, but this is easier.
                Globals.Init("StatsDump", Host, Core);

                //CoreManager.Current.CharacterFilter.Logoff += new EventHandler<Decal.Adapter.Wrappers.LogoffEventArgs>(CharacterFilter_Logoff);

                //create folder in the Documents\Decal Plugins\ 
                /*
                DirectoryInfo pluginPersonalFolder = new DirectoryInfo(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + @"\Decal Plugins\" + Globals.PluginName);
				try
				{
					if (!pluginPersonalFolder.Exists)
						pluginPersonalFolder.Create();
				}
				catch (Exception ex) { Util.LogError(ex); }
                */
                //Initialize the view.
                MVWireupHelper.WireupStart(this, Host);

            }
			catch (Exception ex) { Util.LogError(ex); }
		}

        private void DumpTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                dumpXML();
                //tDumpTimer.Stop();
            }
            catch (Exception ex) { Util.LogError(ex); }
        }

		
        /// <summary>
		/// This is called when the plugin is shut down. This happens only once.
		/// </summary>
		protected override void Shutdown()
		{
			try
			{
                
                //shut down the timer
                if (tDumpTimer != null)
                {
                    tDumpTimer.Stop();
                    tDumpTimer.Tick -= DumpTimer_Tick;
                    tDumpTimer.Dispose();
                    tDumpTimer = null;
                }
                //Destroy the view.
                MVWireupHelper.WireupEnd(this);

			}
			catch (Exception ex) { Util.LogError(ex); }
		}

		[BaseEvent("LoginComplete", "CharacterFilter")]
		private void CharacterFilter_LoginComplete(object sender, EventArgs e)
		{
			try
			{
                dumpXML();
				//Util.WriteToChat("Plugin now online. Server population: " + Core.CharacterFilter.ServerPopulation);

                //init timer to dump the stats on a regular interval
                tDumpTimer = new WindowsTimer();
                tDumpTimer.Tick += new EventHandler(DumpTimer_Tick);
                tDumpTimer.Interval = 5 * 60 * 1000; //5 minutes
                tDumpTimer.Start();
                Util.WriteToChat("StatsDump Timer Running");

                //subscribe to this event to help process luminance and contracts
                Globals.Core.MessageProcessed += new EventHandler<MessageProcessedEventArgs>(Core_MessageProcessed);
                
                //so we can track specific messages, like stipend received...
                Globals.Core.ChatBoxMessage += new EventHandler<ChatTextInterceptEventArgs>(Current_ChatBoxMessage);

				// Subscribe to events here
				//Globals.Core.WorldFilter.ChangeObject += new EventHandler<ChangeObjectEventArgs>(WorldFilter_ChangeObject2);
              
			}
			catch (Exception ex) { Util.LogError(ex); }
		}


 		[BaseEvent("Logoff", "CharacterFilter")]
		private void CharacterFilter_Logoff(object sender, Decal.Adapter.Wrappers.LogoffEventArgs e)
		{
			try
			{
				// Unsubscribe to events here, but know that this event is not gauranteed to happen. I've never seen it not fire though.
				// This is not the proper place to free up resources, but... its the easy way. It's not proper because of above statement.
				//Globals.Core.WorldFilter.ChangeObject -= new EventHandler<ChangeObjectEventArgs>(WorldFilter_ChangeObject);
                dumpXML();

                Globals.Core.MessageProcessed -= new EventHandler<MessageProcessedEventArgs>(Core_MessageProcessed);

                Globals.Core.ChatBoxMessage -= new EventHandler<ChatTextInterceptEventArgs>(Current_ChatBoxMessage);
			}
			catch (Exception ex) { Util.LogError(ex); }
		}


        [BaseEvent("ChangeObject", "WorldFilter")]
        void WorldFilter_ChangeObject(object sender, ChangeObjectEventArgs e)
        {
            try
            {
                // This can get very spammy so I filted it to just print on ident received
               // if (e.Change == WorldChangeType.IdentReceived)
                 //   Util.WriteToChat("WorldFilter_ChangeObject: " + e.Changed.Name + " " + e.Change);
              //  Util.WriteToChat(e.Changed.Name + " " + e.Change);
            }
            catch (Exception ex) { Util.LogError(ex); }
        }
        
        [MVControlEvent("dumpStats", "Click")]
        void dumpStats_Click(object sender, MVControlEventArgs e)
        {
            try
            {
                //Core.CharacterFilter.Skills[CharFilterSkillType.Healing].TrainingTrainingType.Trained
                //Util.WriteToChat("Core.CharacterFilter.Skills.Count: " + Core.CharacterFilter.Skills.Count.ToString(), 14);

                Util.WriteToChat("Manually Dumping XML Stats to file...", 1);
                dumpXML();
                //GetFreeInventorySlots();

               //Util.WriteToChat("TEST: " + Core.CharacterFilter.GetCharProperty(230));
            }
            catch (Exception ex) { Util.LogError(ex); }
        }

        void dumpXML(){
            try
            {

                //create an XML file in the Decal Plugin Directory under the character's name...
                if (Core.CharacterFilter.Name.ToString() == "LoginNotComplete")
                {
                    //if we're not yet ready to get the stats...DON'T!
                    return;
                }
                XmlTextWriter xmlWriter = new XmlTextWriter(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + @"\Decal Plugins\" + Globals.PluginName + "\\" + Core.CharacterFilter.Server + " - " + Core.CharacterFilter.Name.ToString() + ".xml", System.Text.Encoding.UTF8);
                // Opens the document, starts the root node
                xmlWriter.WriteStartDocument();
                xmlWriter.WriteStartElement("character");
                try
                {
                    // ATTRIBUTES
                    xmlWriter.WriteStartElement("attributes");
                    xmlWriter.WriteStartElement("primary");
                    //strength
                    xmlWriter.WriteStartElement("attribute");
                    xmlWriter.WriteAttributeString("name", "Strength");
                    xmlWriter.WriteAttributeString("value", Core.CharacterFilter.Attributes[CharFilterAttributeType.Strength].Base.ToString());
                    xmlWriter.WriteAttributeString("base", Core.CharacterFilter.Attributes[CharFilterAttributeType.Strength].Creation.ToString());
                    xmlWriter.WriteAttributeString("xp", Core.CharacterFilter.Attributes[CharFilterAttributeType.Strength].Exp.ToString());
                    xmlWriter.WriteEndElement();
                    //Endurance
                    xmlWriter.WriteStartElement("attribute");
                    xmlWriter.WriteAttributeString("name", "Endurance");
                    xmlWriter.WriteAttributeString("value", Core.CharacterFilter.Attributes[CharFilterAttributeType.Endurance].Base.ToString());
                    xmlWriter.WriteAttributeString("base", Core.CharacterFilter.Attributes[CharFilterAttributeType.Endurance].Creation.ToString());
                    xmlWriter.WriteAttributeString("xp", Core.CharacterFilter.Attributes[CharFilterAttributeType.Endurance].Exp.ToString());
                    xmlWriter.WriteEndElement();
                    //Coordination
                    xmlWriter.WriteStartElement("attribute");
                    xmlWriter.WriteAttributeString("name", "Coordination");
                    xmlWriter.WriteAttributeString("value", Core.CharacterFilter.Attributes[CharFilterAttributeType.Coordination].Base.ToString());
                    xmlWriter.WriteAttributeString("base", Core.CharacterFilter.Attributes[CharFilterAttributeType.Coordination].Creation.ToString());
                    xmlWriter.WriteAttributeString("xp", Core.CharacterFilter.Attributes[CharFilterAttributeType.Coordination].Exp.ToString());
                    xmlWriter.WriteEndElement();
                    //Quickness
                    xmlWriter.WriteStartElement("attribute");
                    xmlWriter.WriteAttributeString("name", "Quickness");
                    xmlWriter.WriteAttributeString("value", Core.CharacterFilter.Attributes[CharFilterAttributeType.Quickness].Base.ToString());
                    xmlWriter.WriteAttributeString("base", Core.CharacterFilter.Attributes[CharFilterAttributeType.Quickness].Creation.ToString());
                    xmlWriter.WriteAttributeString("xp", Core.CharacterFilter.Attributes[CharFilterAttributeType.Quickness].Exp.ToString());
                    xmlWriter.WriteEndElement();
                    //Focus
                    xmlWriter.WriteStartElement("attribute");
                    xmlWriter.WriteAttributeString("name", "Focus");
                    xmlWriter.WriteAttributeString("value", Core.CharacterFilter.Attributes[CharFilterAttributeType.Focus].Base.ToString());
                    xmlWriter.WriteAttributeString("base", Core.CharacterFilter.Attributes[CharFilterAttributeType.Focus].Creation.ToString());
                    xmlWriter.WriteAttributeString("xp", Core.CharacterFilter.Attributes[CharFilterAttributeType.Focus].Exp.ToString());
                    xmlWriter.WriteEndElement();
                    //Self
                    xmlWriter.WriteStartElement("attribute");
                    xmlWriter.WriteAttributeString("name", "Self");
                    xmlWriter.WriteAttributeString("value", Core.CharacterFilter.Attributes[CharFilterAttributeType.Self].Base.ToString());
                    xmlWriter.WriteAttributeString("base", Core.CharacterFilter.Attributes[CharFilterAttributeType.Self].Creation.ToString());
                    xmlWriter.WriteAttributeString("xp", Core.CharacterFilter.Attributes[CharFilterAttributeType.Self].Exp.ToString());
                    xmlWriter.WriteEndElement();
                    xmlWriter.WriteEndElement(); // </primary>

                    xmlWriter.WriteStartElement("secondary");
                    //health
                    xmlWriter.WriteStartElement("attribute");
                    xmlWriter.WriteAttributeString("name", "Health");
                    xmlWriter.WriteAttributeString("value", Core.CharacterFilter.Vitals[CharFilterVitalType.Health].Base.ToString());
                    int baseHealth = Core.CharacterFilter.Attributes[CharFilterAttributeType.Endurance].Base / 2;
                    xmlWriter.WriteAttributeString("base", baseHealth.ToString());
                    xmlWriter.WriteAttributeString("xp", Core.CharacterFilter.Vitals[CharFilterVitalType.Health].XP.ToString());
                    xmlWriter.WriteEndElement();
                    //stamina
                    xmlWriter.WriteStartElement("attribute");
                    xmlWriter.WriteAttributeString("name", "Stamina");
                    xmlWriter.WriteAttributeString("value", Core.CharacterFilter.Vitals[CharFilterVitalType.Stamina].Base.ToString());
                    xmlWriter.WriteAttributeString("base", Core.CharacterFilter.Attributes[CharFilterAttributeType.Endurance].Base.ToString());
                    xmlWriter.WriteAttributeString("xp", Core.CharacterFilter.Vitals[CharFilterVitalType.Stamina].XP.ToString());
                    xmlWriter.WriteEndElement();
                    //mana
                    xmlWriter.WriteStartElement("attribute");
                    xmlWriter.WriteAttributeString("name", "Mana");
                    xmlWriter.WriteAttributeString("value", Core.CharacterFilter.Vitals[CharFilterVitalType.Mana].Base.ToString());
                    xmlWriter.WriteAttributeString("base", Core.CharacterFilter.Attributes[CharFilterAttributeType.Self].Base.ToString());
                    xmlWriter.WriteAttributeString("xp", Core.CharacterFilter.Vitals[CharFilterVitalType.Mana].XP.ToString());
                    xmlWriter.WriteEndElement();

                    xmlWriter.WriteEndElement(); // </secondary>
                    xmlWriter.WriteEndElement();


                    //======SKILLS========
                    xmlWriter.WriteStartElement("skills");

                    //spec skills
                    xmlWriter.WriteStartElement("specialized");
                    foreach (CharFilterSkillType mySkillType in Enum.GetValues(typeof(CharFilterSkillType)))
                    {
                        if (mySkillType != CharFilterSkillType.Axe &&
                            mySkillType != CharFilterSkillType.Bow &&
                            mySkillType != CharFilterSkillType.Crossbow &&
                            mySkillType != CharFilterSkillType.Dagger &&
                            mySkillType != CharFilterSkillType.Gearcraft &&
                            mySkillType != CharFilterSkillType.Mace &&
                            mySkillType != CharFilterSkillType.Spear &&
                            mySkillType != CharFilterSkillType.Staff &&
                            mySkillType != CharFilterSkillType.Sword &&
                            mySkillType != CharFilterSkillType.ThrownWeapons &&
                            mySkillType != CharFilterSkillType.Unarmed)
                        {
                            if (Core.CharacterFilter.Skills[mySkillType].Training.ToString() == "Specialized")
                            {
                                xmlWriter.WriteStartElement("skill");
                                xmlWriter.WriteAttributeString("name", Core.CharacterFilter.Skills[mySkillType].Name.ToString());
                                xmlWriter.WriteAttributeString("value", Core.CharacterFilter.Skills[mySkillType].Base.ToString());
                                xmlWriter.WriteAttributeString("raised", Core.CharacterFilter.Skills[mySkillType].Increment.ToString());
                                xmlWriter.WriteAttributeString("xp", Core.CharacterFilter.Skills[mySkillType].XP.ToString());
                                xmlWriter.WriteEndElement(); // </skill>
                            }
                        }
                    }
                    //summoning
                    Decal.Interop.Filters.SkillInfo SummoningSkillInfo;
                    SummoningSkillInfo = Core.CharacterFilter.Underlying.get_Skill((Decal.Interop.Filters.eSkillID)54);
                    if (SummoningSkillInfo.Training.ToString() == "eTrainSpecialized")
                    {
                        xmlWriter.WriteStartElement("skill");
                        xmlWriter.WriteAttributeString("name", SummoningSkillInfo.Name.ToString());
                        xmlWriter.WriteAttributeString("value", SummoningSkillInfo.Base.ToString());
                        xmlWriter.WriteAttributeString("raised", SummoningSkillInfo.Increment.ToString());
                        if (SummoningSkillInfo.Exp < 0)
                        {
                            xmlWriter.WriteAttributeString("xp", (SummoningSkillInfo.Exp + 4294967296).ToString());
                        }
                        else
                        {
                            xmlWriter.WriteAttributeString("xp", SummoningSkillInfo.Exp.ToString());
                        }

                        //                                xmlWriter.WriteAttributeString("xp", Core.CharacterFilter.Skills[mySkillType].`
                        xmlWriter.WriteEndElement(); // </skill>
                    }
                    xmlWriter.WriteEndElement(); // </specialized>

                    //trained skills
                    xmlWriter.WriteStartElement("trained");
                    foreach (CharFilterSkillType mySkillType in Enum.GetValues(typeof(CharFilterSkillType)))
                    {
                        if (mySkillType != CharFilterSkillType.Axe &&
                            mySkillType != CharFilterSkillType.Bow &&
                            mySkillType != CharFilterSkillType.Crossbow &&
                            mySkillType != CharFilterSkillType.Dagger &&
                            mySkillType != CharFilterSkillType.Gearcraft &&
                            mySkillType != CharFilterSkillType.Mace &&
                            mySkillType != CharFilterSkillType.Spear &&
                            mySkillType != CharFilterSkillType.Staff &&
                            mySkillType != CharFilterSkillType.Sword &&
                            mySkillType != CharFilterSkillType.ThrownWeapons &&
                            mySkillType != CharFilterSkillType.Unarmed)
                        {
                            if (Core.CharacterFilter.Skills[mySkillType].Training.ToString() == "Trained")
                            {
                                xmlWriter.WriteStartElement("skill");
                                xmlWriter.WriteAttributeString("name", Core.CharacterFilter.Skills[mySkillType].Name.ToString());
                                xmlWriter.WriteAttributeString("value", Core.CharacterFilter.Skills[mySkillType].Base.ToString());
                                xmlWriter.WriteAttributeString("raised", Core.CharacterFilter.Skills[mySkillType].Increment.ToString());
                                if (Core.CharacterFilter.Skills[mySkillType].XP < 0)
                                {
                                    xmlWriter.WriteAttributeString("xp", (Core.CharacterFilter.Skills[mySkillType].XP + 4294967296).ToString());
                                }
                                else 
                                {
                                    xmlWriter.WriteAttributeString("xp", Core.CharacterFilter.Skills[mySkillType].XP.ToString());
                                }
                                
//                                xmlWriter.WriteAttributeString("xp", Core.CharacterFilter.Skills[mySkillType].`
                                xmlWriter.WriteEndElement(); // </skill>
                            }
                        }
                    }

                    //summoning
                    if (SummoningSkillInfo.Training.ToString() == "eTrainTrained")
                    {
                        xmlWriter.WriteStartElement("skill");
                        xmlWriter.WriteAttributeString("name", SummoningSkillInfo.Name.ToString());
                        xmlWriter.WriteAttributeString("value", SummoningSkillInfo.Base.ToString());
                        xmlWriter.WriteAttributeString("raised", SummoningSkillInfo.Increment.ToString());
                        if (SummoningSkillInfo.Exp < 0)
                        {
                            xmlWriter.WriteAttributeString("xp", (SummoningSkillInfo.Exp + 4294967296).ToString());
                        }
                        else
                        {
                            xmlWriter.WriteAttributeString("xp", SummoningSkillInfo.Exp.ToString());
                        }

                        //                                xmlWriter.WriteAttributeString("xp", Core.CharacterFilter.Skills[mySkillType].`
                        xmlWriter.WriteEndElement(); // </skill>
                    }

                    xmlWriter.WriteEndElement(); // </trained>

                    xmlWriter.WriteEndElement(); // </skills>

                    //======MISC========
                    xmlWriter.WriteStartElement("misc");

                        //<name value="Allita" />
                    xmlWriter.WriteStartElement("name");
                    xmlWriter.WriteAttributeString("value", Core.CharacterFilter.Name.ToString());
                    xmlWriter.WriteEndElement(); // </name>
                    xmlWriter.WriteStartElement("account");
                    xmlWriter.WriteAttributeString("value", Core.CharacterFilter.AccountName.ToString());
                    xmlWriter.WriteEndElement(); // </account>
                    xmlWriter.WriteStartElement("rank");
                    xmlWriter.WriteAttributeString("value", Core.CharacterFilter.Rank.ToString());
                    xmlWriter.WriteEndElement(); // </rank>
                    xmlWriter.WriteStartElement("title");
                    xmlWriter.WriteAttributeString("value", Core.CharacterFilter.ClassTemplate.ToString());
                    xmlWriter.WriteEndElement(); // </title>
                    xmlWriter.WriteStartElement("gender");
                    xmlWriter.WriteAttributeString("value", Core.CharacterFilter.Gender.ToString());
                    xmlWriter.WriteEndElement(); // </gender>
                    xmlWriter.WriteStartElement("race");
                    xmlWriter.WriteAttributeString("value", Core.CharacterFilter.Race.ToString());
                    xmlWriter.WriteEndElement(); // </race>
                    xmlWriter.WriteStartElement("skillcredits");
                    xmlWriter.WriteAttributeString("value", Core.CharacterFilter.SkillPoints.ToString());
                    xmlWriter.WriteEndElement(); // </name
                    xmlWriter.WriteStartElement("deaths");
                    xmlWriter.WriteAttributeString("value", Core.CharacterFilter.Deaths.ToString());
                    xmlWriter.WriteEndElement(); // </deaths

                    xmlWriter.WriteStartElement("location");
                    if(Globals.Core.WorldFilter.GetByName(Core.CharacterFilter.Name).Count > 0 && Globals.Core.WorldFilter.GetByName(Core.CharacterFilter.Name).First.Coordinates() != null)
                        xmlWriter.WriteAttributeString("value", Globals.Core.WorldFilter.GetByName(Core.CharacterFilter.Name).First.Coordinates().ToString());
                    xmlWriter.WriteEndElement(); // </location

                    xmlWriter.WriteStartElement("landblock");
                    xmlWriter.WriteAttributeString("value", CoreManager.Current.Actions.Landcell.ToString("X8")); //write out landblock in hex
                    xmlWriter.WriteEndElement(); // </landblock

                    xmlWriter.WriteStartElement("lastseen");
                    xmlWriter.WriteAttributeString("value", DateTime.Now.ToString("ddd, MMM d, yyyy h:mm:ss tt"));
                    xmlWriter.WriteEndElement(); // </lastseen
                    xmlWriter.WriteStartElement("luminance");
                    //xmlWriter.WriteAttributeString("value", Core.CharacterFilter.under;
                    xmlWriter.WriteAttributeString("value", current_luminance.ToString());
                    xmlWriter.WriteAttributeString("max", max_luminance.ToString());
                    xmlWriter.WriteEndElement(); // </luminance

                    xmlWriter.WriteStartElement("level");
                    xmlWriter.WriteAttributeString("value", Core.CharacterFilter.Level.ToString());
                    xmlWriter.WriteEndElement(); // </name
                    xmlWriter.WriteStartElement("vitae");
                    xmlWriter.WriteAttributeString("value", Core.CharacterFilter.Vitae.ToString());
                    xmlWriter.WriteEndElement(); // </name
                    xmlWriter.WriteStartElement("xp");
                    xmlWriter.WriteAttributeString("value", Core.CharacterFilter.TotalXP.ToString());
                    xmlWriter.WriteAttributeString("unspent", Core.CharacterFilter.UnassignedXP.ToString());
                    xmlWriter.WriteAttributeString("level", Core.CharacterFilter.XPToNextLevel.ToString());
                    xmlWriter.WriteEndElement(); // </name
                    xmlWriter.WriteStartElement("followers");
                    xmlWriter.WriteAttributeString("value", Core.CharacterFilter.Followers.ToString());
                    xmlWriter.WriteEndElement(); // </followers
                    xmlWriter.WriteStartElement("burden");
                    xmlWriter.WriteAttributeString("value", Core.CharacterFilter.Burden.ToString());
                    xmlWriter.WriteAttributeString("units", Core.CharacterFilter.BurdenUnits.ToString());
                    xmlWriter.WriteEndElement(); // </burden
                    xmlWriter.WriteStartElement("inventory");
                    xmlWriter.WriteAttributeString("free", GetFreeInventorySlots().ToString());
                    xmlWriter.WriteEndElement(); // </burden

                    //Core.CharacterFilter.Vassals
                    xmlWriter.WriteStartElement("augments");
                    // Util.WriteToChat("Starting Augments...", 6);
//                        if(Core.CharacterFilter.GetCharProperty(int(Augmentations.ClutchMiser)))
                    foreach (Augmentations myAug in Enum.GetValues(typeof(Augmentations)))
                    {
                        if(Core.CharacterFilter.GetCharProperty((int)myAug) > 0)
                        {
                            xmlWriter.WriteStartElement("augment");
                            xmlWriter.WriteAttributeString("name", myAug.ToString());
                            xmlWriter.WriteAttributeString("value", Core.CharacterFilter.GetCharProperty((int)myAug).ToString());
                            xmlWriter.WriteEndElement(); // </skill>
                        }
                    }
                    if (Core.CharacterFilter.GetCharProperty(326) > 0)
                    {
                        xmlWriter.WriteStartElement("augment");
                        xmlWriter.WriteAttributeString("name", "JackOfAllTrades");
                        xmlWriter.WriteAttributeString("value", Core.CharacterFilter.GetCharProperty(326).ToString());
                        xmlWriter.WriteEndElement(); // </skill>
                    }
                    if (Core.CharacterFilter.GetCharProperty(365) > 0)
                    {
                        xmlWriter.WriteStartElement("augment");
                        xmlWriter.WriteAttributeString("name", "AuraWorld");
                        xmlWriter.WriteAttributeString("value", Core.CharacterFilter.GetCharProperty(365).ToString());
                        xmlWriter.WriteEndElement(); // </skill>
                    }
                    if (Core.CharacterFilter.GetCharProperty(340) > 0)
                    {
                        xmlWriter.WriteStartElement("augment");
                        xmlWriter.WriteAttributeString("name", "AuraManaInfusion");
                        xmlWriter.WriteAttributeString("value", Core.CharacterFilter.GetCharProperty(365).ToString());
                        xmlWriter.WriteEndElement(); // </skill>
                    }

                    //365 = World Aura
                    /*
                    for (int i = 345; i < 390; i++)
                    {
                        if (i != 365) //world aura, dealt with above
                        {
                            if (Core.CharacterFilter.GetCharProperty(i) > 0)
                            {
                                xmlWriter.WriteStartElement("augment");
                                xmlWriter.WriteAttributeString("name", ("UnknownAugment" + i).ToString());
                                xmlWriter.WriteAttributeString("value", Core.CharacterFilter.GetCharProperty(i).ToString());
                                xmlWriter.WriteEndElement(); // </skill>
                            }
                        }
                    }
                    */


                    xmlWriter.WriteEndElement(); // </augments>

                    /* SPELL COMPONENTS */
                    xmlWriter.WriteStartElement("spellcomps");

                    xmlWriter.WriteStartElement("lead");
                    xmlWriter.WriteAttributeString("value", getItemInventoryCount("Lead Scarab").ToString());
                    xmlWriter.WriteEndElement(); // </lead>

                    xmlWriter.WriteStartElement("iron");
                    xmlWriter.WriteAttributeString("value", getItemInventoryCount("Iron Scarab").ToString());
                    xmlWriter.WriteEndElement(); // </comp>

                    xmlWriter.WriteStartElement("copper");
                    xmlWriter.WriteAttributeString("value", getItemInventoryCount("Copper Scarab").ToString());
                    xmlWriter.WriteEndElement(); // </comp>

                    xmlWriter.WriteStartElement("silver");
                    xmlWriter.WriteAttributeString("value", getItemInventoryCount("Silver Scarab").ToString());
                    xmlWriter.WriteEndElement(); // </comp>

                    xmlWriter.WriteStartElement("gold");
                    xmlWriter.WriteAttributeString("value", getItemInventoryCount("Gold Scarab").ToString());
                    xmlWriter.WriteEndElement(); // </comp>

                    xmlWriter.WriteStartElement("pyreal");
                    xmlWriter.WriteAttributeString("value", getItemInventoryCount("Pyreal Scarab").ToString());
                    xmlWriter.WriteEndElement(); // </comp>

                    xmlWriter.WriteStartElement("platinum");
                    xmlWriter.WriteAttributeString("value", getItemInventoryCount("Platinum Scarab").ToString());
                    xmlWriter.WriteEndElement(); // </comp>

                    xmlWriter.WriteStartElement("diamond");
                    xmlWriter.WriteAttributeString("value", getItemInventoryCount("Diamond Scarab").ToString());
                    xmlWriter.WriteEndElement(); // </comp>

                    xmlWriter.WriteStartElement("mana");
                    xmlWriter.WriteAttributeString("value", getItemInventoryCount("Mana Scarab").ToString());
                    xmlWriter.WriteEndElement(); // </comp>

                    xmlWriter.WriteStartElement("dark");
                    xmlWriter.WriteAttributeString("value", getItemInventoryCount("Dark Scarab").ToString());
                    xmlWriter.WriteEndElement(); // </comp>

                    xmlWriter.WriteStartElement("prisimatic");
                    xmlWriter.WriteAttributeString("value", getItemInventoryCount("Prismatic Taper").ToString());
                    xmlWriter.WriteEndElement(); // </comp>
                    
                    xmlWriter.WriteEndElement(); // </spellcomps>

                    //Virindi Comps Hud...
                    getVirindiCompsData(xmlWriter);

                    /* ALLEGIANCE INFO */
                    xmlWriter.WriteStartElement("allegiance");
                    try
                    {
                        /* MONARCH */
                        xmlWriter.WriteStartElement("monarch");
                        xmlWriter.WriteAttributeString("name", Core.CharacterFilter.Monarch.Name);
                        xmlWriter.WriteAttributeString("loyalty", Core.CharacterFilter.Monarch.Loyalty.ToString());
                        xmlWriter.WriteAttributeString("leadership", Core.CharacterFilter.Monarch.Leadership.ToString());
                        xmlWriter.WriteAttributeString("race", Core.CharacterFilter.Monarch.Race.ToString());
                        xmlWriter.WriteAttributeString("rank", Core.CharacterFilter.Monarch.Rank.ToString());
                        xmlWriter.WriteAttributeString("gender", Core.CharacterFilter.Monarch.Gender.ToString());
                        xmlWriter.WriteAttributeString("followers", Core.CharacterFilter.MonarchFollowers.ToString());
                        xmlWriter.WriteEndElement(); // </monarch>

                        /* PATRON */
                        xmlWriter.WriteStartElement("patron");
                        xmlWriter.WriteAttributeString("name", Core.CharacterFilter.Patron.Name);
                        xmlWriter.WriteAttributeString("xp", Core.CharacterFilter.Patron.XP.ToString());
                        xmlWriter.WriteAttributeString("loyalty", Core.CharacterFilter.Patron.Loyalty.ToString());
                        xmlWriter.WriteAttributeString("leadership", Core.CharacterFilter.Patron.Leadership.ToString());
                        xmlWriter.WriteAttributeString("race", Core.CharacterFilter.Patron.Race.ToString());
                        xmlWriter.WriteAttributeString("rank", Core.CharacterFilter.Patron.Rank.ToString());
                        xmlWriter.WriteAttributeString("gender", Core.CharacterFilter.Patron.Gender.ToString());
                        xmlWriter.WriteEndElement(); // </patron>

                        //loop through vassals...
                        xmlWriter.WriteStartElement("vassals");
                        foreach (AllegianceInfoWrapper vassal in Core.CharacterFilter.Vassals)
                        {
                            xmlWriter.WriteStartElement("vassal");
                            xmlWriter.WriteAttributeString("name", vassal.Name);
                            xmlWriter.WriteAttributeString("xp", vassal.XP.ToString());
                            xmlWriter.WriteAttributeString("loyalty", vassal.Loyalty.ToString());
                            xmlWriter.WriteAttributeString("leadership", vassal.Leadership.ToString());
                            xmlWriter.WriteAttributeString("race", vassal.Race.ToString());
                            xmlWriter.WriteAttributeString("rank", vassal.Rank.ToString());
                            xmlWriter.WriteAttributeString("gender", vassal.Gender.ToString());
                           // Util.WriteToChat("Vassal Detected: " + vassal.Name + ", Loyalty: " + vassal.Loyalty);
                            xmlWriter.WriteEndElement(); // </vassals>
                        }
                        xmlWriter.WriteEndElement(); // </vassals>
                    }
                    catch (Exception ex) { 
                        //NO ALLEGIANCE, DON'T DO ANYTHING
                    }
                    xmlWriter.WriteEndElement(); // </allegiance>

                    //save contract status
                    xmlWriter.WriteStartElement("contracts");
                        //stipend
                        xmlWriter.WriteStartElement("contract");
                        if (contract_stipend_found)
                        {
                            xmlWriter.WriteAttributeString("name", "Stipend: General");
                            xmlWriter.WriteAttributeString("start", stipend_start);
                            xmlWriter.WriteAttributeString("timer", stipend_timer.ToString());
                            xmlWriter.WriteEndElement(); // </contract>
                        }

                    xmlWriter.WriteEndElement(); // </contracts>

                    xmlWriter.WriteEndElement(); // </misc>


                    xmlWriter.WriteStartElement("character_prop");
                    for (int i = 1; i < 500; i++)
                    {
                        if (Core.CharacterFilter.GetCharProperty(i) > 0)
                        {
                            xmlWriter.WriteStartElement("property");
                            xmlWriter.WriteAttributeString("id", (i).ToString());
                            xmlWriter.WriteAttributeString("value", Core.CharacterFilter.GetCharProperty(i).ToString());
                            xmlWriter.WriteEndElement(); // </property>
                        }
                    }
                    xmlWriter.WriteEndElement(); //</character_prop>
                }
                finally
                {
                    // Ends the document.
                    xmlWriter.WriteEndDocument();
                    // close writer
                    xmlWriter.Close();
                }
            }
            catch (Exception ex) { Util.LogError(ex); }
        }

      
        /* MY STUFF */
        void MyEventHandler(object sender, EventArgs e)
        {
            Util.WriteToChat("MyEventHandler Triggered");
        }
        /* END MY STUFF */

        int getItemInventoryCount(string itemName)
        {
            List<int> packList = new List<int>();
            int tmpCount = 0;
            foreach (WorldObject worldObject in Globals.Core.WorldFilter.GetByContainer(Globals.Core.CharacterFilter.Id))
            {
                if (itemName.Equals(worldObject.Name, StringComparison.Ordinal))//worldObject.Name == itemName)
                {
                    if (worldObject.Values(LongValueKey.StackCount) > 1)
                    {
                        tmpCount += worldObject.Values(LongValueKey.StackCount);
                    }
                    else
                    {
                        tmpCount = tmpCount + 1;
                    }
                }
                if (worldObject.Category == 512)
                {
                    packList.Add(worldObject.Id);
                }

            }

            if (packList.Count > 0)
            {
                foreach (int packID in packList)
                {
                    foreach (WorldObject worldObject in Globals.Core.WorldFilter.GetByContainer(packID))
                    {
                        if (itemName.Equals(worldObject.Name, StringComparison.Ordinal))//worldObject.Name == itemName)
                        {
                            if (worldObject.Values(LongValueKey.StackCount) > 1)
                            {
                                tmpCount += worldObject.Values(LongValueKey.StackCount);
                            }
                            else
                            {
                                tmpCount = tmpCount + 1;
                            }
                        }
                    }
                }
            }

            return (tmpCount);
        }

        private string getVirindiHudsPath()
        {
            //key HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\Decal\Plugins\{C6B1DF06-FF20-459E-8302-AA346CBFDA01}
            //string value: Path

            string hudPath = (string)Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\Decal\Plugins\{C6B1DF06-FF20-459E-8302-AA346CBFDA01}", "Path", null);
            return (hudPath);
        }

        private void getVirindiCompsData(XmlWriter xmlWriter)
        {
            //key HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\Decal\Plugins\{C6B1DF06-FF20-459E-8302-AA346CBFDA01}
            //string value: Path
            string hudPath = getVirindiHudsPath();
            if(hudPath == null){
                return;
            }

            List<string> hudItems = new List<string>(); ;
            List<int> hudIcons = new List<int>(); ;
            if (File.Exists(hudPath + "\\hudsettings_" + Core.CharacterFilter.Server + "_" + Core.CharacterFilter.Name.ToString() + ".vdb"))
            {
                string[] lines = File.ReadAllLines(hudPath + "\\hudsettings_" + Core.CharacterFilter.Server + "_" + Core.CharacterFilter.Name.ToString() + ".vdb");
                int startPos = Array.IndexOf(lines, "_") + 1;
                if (startPos > 0)
                {

                    //loop through the file and get the names of the items
                    int next = startPos + 1;
                    int numberItems = Int32.Parse(lines[next]);
                    if (numberItems > 0)
                    {
                        int endPos = next + (numberItems * 2) + 1;
                        for (int i = next + 2; i < endPos; i++)
                        {
                            //get listing of items
                            hudItems.Add(lines[i]);
                            i++;
                        }
                    }

                    //loop through the file and get the names of the items
                    startPos = Array.IndexOf(lines, "_", next) + 1;
                    next = startPos + 1;
                    if (numberItems > 0)
                    {
                        int endPos = next + (numberItems * 2) + 1;
                        for (int i = next + 2; i < endPos; i++)
                        {
                            //get icons of the items
                            hudIcons.Add(Int32.Parse(lines[i]));
                            i++;
                        }
                    }
                }
            }

            if (hudItems.Count > 0)
            {
                xmlWriter.WriteStartElement("virindi_comps");

                for (int i = 0; i < hudItems.Count; i++)
                {

                    xmlWriter.WriteStartElement("item");
                    xmlWriter.WriteAttributeString("name", hudItems[i]);
                    xmlWriter.WriteAttributeString("value", getItemInventoryCount(hudItems[i]).ToString());
                    xmlWriter.WriteAttributeString("icon", hudIcons[i].ToString());
                    xmlWriter.WriteEndElement(); // </item>

                }
                xmlWriter.WriteEndElement(); // </virindi_comps>

            }

            return;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public static int GetFreeInventorySlots()
        {
            try
            {
                List<int> packList = new List<int>();
                int slotsEmpty = 0;
                int slotsFull = 0;
                foreach (WorldObject worldObject in Globals.Core.WorldFilter.GetByContainer(Globals.Core.CharacterFilter.Id))
                {
                    if (worldObject.Category == 512)
                    {
                        packList.Add(worldObject.Id);
                    }
                    else
                    {
                        if (worldObject.Values(LongValueKey.EquippedSlots) == 0) //make sure this item is not equipped!
                        {
                            slotsFull++;
                            //Util.WriteToChat(worldObject.Name.ToString() + " in " + worldObject.Container.ToString());
                        }
                        else
                        {
                           // Util.WriteToChat("EQUIPPED ITEM: " + worldObject.Name.ToString() + " in " + worldObject.Container.ToString());
                            
                        }
                    }
                }
                slotsEmpty = 102 - slotsFull;
               // Util.WriteToChat("Slots Full in Main - " + slotsFull.ToString());
                        
                if (packList.Count > 0)
                {
                    foreach (int packID in packList)
                    {
                        slotsFull = 0; //reset the count for this pack
                        foreach (WorldObject worldObject in Globals.Core.WorldFilter.GetByContainer(packID))
                        {
                            slotsFull++;
                        }
                        //Util.WriteToChat("Slots Full in Pack - " + slotsFull.ToString());
                        slotsEmpty += 24 - slotsFull;
                    }
                }
                //Util.WriteToChat(slotsEmpty.ToString() + " inventory slots empty.");
               
                return slotsEmpty;
            }
            catch (Exception ex) { Util.LogError(ex); return 0; }
        }

        [BaseEvent("MessageProcessed")]
        void Core_MessageProcessed(object sender, Decal.Adapter.MessageProcessedEventArgs e)
        {
            //Util.WriteToChat("Event Processed: " + e.Message.Type.ToString());
            //Need this to track luminance
            switch (e.Message.Type)
            {
                case 0x02CF: //Set character qword
                    {
                        int key = e.Message.Value<int>("key");
                        if (key == 0x06)
                        {
                            current_luminance = e.Message.Value<long>("value");
                        }
                        if (key == 0x07)
                        {
                            max_luminance = e.Message.Value<long>("value");
                        }
                    }
                    break;
                case 0xF7B0: //Ordered message - Game Event
                    switch (e.Message.Value<int>("event"))
                    {
                        case 0x0013: //Login character
                            {
                                Decal.Adapter.MessageStruct properties = e.Message.Struct("properties");
                                if ((properties.Value<int>("flags") & 0x00000080) > 0)
                                {
                                    short qwordcount = properties.Value<short>("qwordCount");
                                    for (short i = 0; i < qwordcount; ++i)
                                    {
                                        int key = properties.Struct("qwords").Struct(i).Value<int>("key");
                                        if (key == 0x06)
                                        {
                                           // Util.WriteToChat("0x06 Found current luminance: " + e.Message.Value<long>("value").ToString());
                                            current_luminance = properties.Struct("qwords").Struct(i).Value<long>("value");
                                            break;
                                        }
                                        if (key == 0x07)
                                        {
                                            //Util.WriteToChat("0x07 Found max luminance: " + e.Message.Value<long>("value").ToString());
                                            max_luminance = e.Message.Value<long>("value");
                                        }
                                    }
                                }
                            }
                            break;
                        case 0x0314: //Contracts...
                            {
                                try

                                {
                                    // Parse the Contract structure manually
                                    using (BinaryReader binaryReader = new BinaryReader(new MemoryStream(e.Message.RawData)))
                                    {
                                        binaryReader.ReadUInt32(); // 0xF7B0
                                        binaryReader.ReadUInt32(); // Character GUID
                                        binaryReader.ReadUInt32(); // Sequence
                                        binaryReader.ReadUInt32(); // 0x0314

                                        var count = binaryReader.ReadUInt16();
                                        /*var tablesize = */binaryReader.ReadInt16();

                                        for(var i = 0; i < count; i++)
                                        {
                                            var key = binaryReader.ReadUInt32();
                                            var _version = binaryReader.ReadUInt32();
                                            var _contract_id = binaryReader.ReadUInt32(); // should equal the "key"
                                            var _contract_stage = binaryReader.ReadUInt32();
                                            var _time_when_done = binaryReader.ReadDouble();
                                            var _time_when_repeats = binaryReader.ReadDouble();

                                            //Util.WriteToChat($"Found ContractID {_contract_id}");

                                            switch (_contract_id)
                                            {
                                                case 245:
                                                    contract_stipend_found = true;
                                                    //DateTime theDate = DateTime.Now;
                                                    stipend_start = DateTime.Now.ToString("F");
                                                    stipend_timer = _time_when_repeats;
                                                    break;
                                            }
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Util.LogError(ex);
                                }
                            }
                            break;
                        case 0x0315: //Contract Update...
                            {
                                try
                                {
                                    using (BinaryReader binaryReader = new BinaryReader(new MemoryStream(e.Message.RawData)))
                                    {
                                        binaryReader.ReadUInt32(); // 0xF7B0
                                        binaryReader.ReadUInt32(); // Character GUID
                                        binaryReader.ReadUInt32(); // Sequence
                                        binaryReader.ReadUInt32(); // 0x0315

                                        var _version = binaryReader.ReadUInt32();
                                        var _contract_id = binaryReader.ReadUInt32(); // should equal the "key"
                                        var _contract_stage = binaryReader.ReadUInt32();
                                        var _time_when_done = binaryReader.ReadDouble();
                                        var _time_when_repeats = binaryReader.ReadDouble();

                                        var bDeleteContract = binaryReader.ReadBoolean();
                                        var bSetAsDisplayContract = binaryReader.ReadBoolean();

                                        //Util.WriteToChat("StatsDump: Contracts 0x0315 Found = " + contract_count.ToString());
                                        //Util.WriteToChat($"x315 Contract Update: Contract ID = {_contract_id}, Status = {_contract_stage}, Timer = {_time_when_repeats}");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Util.LogError(ex);
                                }
                            }
                            break;
                    }
                    break;
            }
        }

        //[BaseEvent("LoginComplete", "CharacterFilter")]
        void Current_ChatBoxMessage(object sender, ChatTextInterceptEventArgs e)
        {
            try
            {
                if (String.IsNullOrEmpty(e.Text))
                    return;

                // You cast Surge of Festering on yourself
                // Aetheria surges on MyCharactersName with the power of Surge of Festering!

                //Util.WriteToChat(e.Text);
                    
                
                //19:46:47 Monroe tells you, "You have returned young one!"
                //19:46:47 Monroe tells you, "Another stipend for your service."
                //19:46:47 Monroe gives you Stipend.

                //if you've been handed a stipend...
                if (e.Text.StartsWith("Monroe gives you Stipend."))
                {
                    contract_stipend_found = true;
                    Util.WriteToChat("Stipend Completion Detected -- Updating Stats");
                    stipend_start = DateTime.Now.ToString("F"); //set stipend start time to NOW!
                    stipend_timer = 60 * 60 * 24 * 6; // 6 days (60 seconds * 60 minutes * 24 hours * 6 days)
                    dumpXML(); // dump our stats so we save this status NOW
                }

                if (e.Text.StartsWith("Monroe tells you, \"Try back next month.\""))
                {
                    Util.WriteToChat("Additional stipend timer detected...");
                    additionalStipendTimer = true;
                }
                if (e.Text.StartsWith("You must wait") && additionalStipendTimer == true)
                {

                    string myText = e.Text;

                    int myPos = 0;
                    int myd = 0;
                    int myh = 0;
                    int mym = 0;
                    int mys = 0;

                    //days
                    myPos = e.Text.IndexOf("d ");
                    if (myPos != -1)
                    {
                       // Util.WriteToChat(myPos.ToString());
                        myd = Convert.ToInt32(myText.Substring(myPos - 2, 2).Trim());
                    }

                    //hours
                    myPos = myText.IndexOf("h ");
                    if (myPos != -1)
                    {
                       // Util.WriteToChat(myPos.ToString());
                        myh = Convert.ToInt32(myText.Substring(myPos - 2, 2).Trim());
                    }

                    //minutes
                    myPos = myText.IndexOf("m ");
                    if (myPos != -1)
                    {
                        mym = Convert.ToInt32(myText.Substring(myPos - 2, 2).Trim());
                    }

                    //seconds
                    myPos = myText.IndexOf("s ");
                    if (myPos != -1)
                    {
                        mys = Convert.ToInt32(myText.Substring(myPos - 2, 2).Trim());
                    }

                    stipend_timer = (myd * 24 * 60 * 60) + (myh * 60 * 60) + (mym * 60) + mys;
                    additionalStipendTimer = false; //reset this flag
                    dumpXML(); // dump our stats so we save this status NOW
                    Util.WriteToChat("Saving Updated Timer");
                }


//10:56:23 Monroe tells you, "I am afraid you've received all you can for this month."
//10:56:23 Monroe tells you, "Try back next month."
//10:56:23 You must wait 1d 9h 37m 44s to receive stipends again.
               
            }
            catch (Exception ex) { Util.LogError(ex); }
        }
    }
}
