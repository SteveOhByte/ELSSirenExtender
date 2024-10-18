using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Security;
using System.Windows.Forms;
using LiteConfig;
using LSPD_First_Response.Mod.API;
using Microsoft.Win32;
using OhPluginEssentials;
using Rage;

namespace ELSSirenExtender
{
    public class Main : Plugin
    {
        private static Logger Logger;
        public static Keys firstSirenKey = Keys.D1;
        public static Keys secondSirenKey = Keys.D2;
        public static Keys hornKey = Keys.E;
        public static Keys yelpKey = Keys.R;
        public static ControllerButtons hornButton = ControllerButtons.LeftThumb;
        public static ControllerButtons yelpButton = ControllerButtons.B;

        private static string config = AppDomain.CurrentDomain.BaseDirectory + @"\plugins\LSPDFR\ELSSirenExtender\config.lc";
        private static bool enableSirenCutoff = true;
        private static bool enableFriendlyHonk = true;
        private static bool enableLeaveEngineRunning = false;

        private static readonly Version assemblyVersion = Assembly.GetExecutingAssembly().GetName().Version;
        private static readonly string version = $"{assemblyVersion.Major}.{assemblyVersion.Minor}.{assemblyVersion.Build}";

        private Prompt associationPrompt;
        private const float associationWaitTime = 15.0f;
        private float associationWaitTimer = 0.0f;
        
        public override void Initialize()
        {
            Game.LogTrivial("ELSSirenExtender Initializing...");
            Logger = new Logger();

            if (!File.Exists(config))
            {
                File.Create(config).Close();
                LC.WriteValue(config, "Cutoff siren when exiting vehicle", true);
                LC.WriteValue(config, "AI-cops honk back to you", true);
                LC.WriteValue(config, "Leave engine running when exiting vehicle", false);
            }
            else
            {
                enableSirenCutoff = LC.ReadBool(config, "Cutoff siren when exiting vehicle");
                enableFriendlyHonk = LC.ReadBool(config, "AI-cops honk back to you");
                enableLeaveEngineRunning = LC.ReadBool(config, "Leave engine running when exiting vehicle");
            }
            
            string elsIniFile = AppDomain.CurrentDomain.BaseDirectory + @"\ELS.ini";
            if (!File.Exists(elsIniFile))
            {
                Logger.Error("ELS.ini not found! Is ELS installed?");
                Notification.DisplayNotification(TimeSpan.FromSeconds(15), Color.FromArgb(150, 100, 0, 0), "ELSSirenExtender", "Error", "ELS.ini not found!");
                return;
            }
            
            string[] lines = File.ReadAllLines(elsIniFile);
            foreach (string line in lines)
            {
                if (line.StartsWith("Snd_SrnTon1"))
                    firstSirenKey = (Keys)int.Parse(line.Split('=')[1]);
                if (line.StartsWith("Snd_SrnTon2"))
                    secondSirenKey = (Keys)int.Parse(line.Split('=')[1]);
                if (line.StartsWith("Sound_Ahorn"))
                    hornKey = (Keys)int.Parse(line.Split('=')[1]);
                if (line.StartsWith("Sound_Manul"))
                    yelpKey = (Keys)int.Parse(line.Split('=')[1]);
            }

            if (enableSirenCutoff)
                GameFiber.StartNew(SirenCutoff.Start, "Siren Cutoff Fibre");
            if (enableFriendlyHonk)
                GameFiber.StartNew(FriendlyHonk.Start, "Friendly Honk Fibre");
            if (enableLeaveEngineRunning)
                GameFiber.StartNew(LeaveEngineRunning.Start, "Leave Engine Running Fibre");
            
            Functions.OnOnDutyStateChanged += OnOnDutyStateChangedHandler;
            
            Logger.Log($"ELSSirenExtender v {version} has been initialized at {DateTime.Now}");
            
            if (LCNotAssociated())
            {
                // Allow association choice for 10 seconds, then exit the checking loop
                associationPrompt = Prompt.DisplayPrompt("Press ~y~Y ~w~to connect .lc", "files to Notepad", TimeSpan.FromSeconds(associationWaitTime));
                GameFiber.StartNew(AssociateFiles);
            }
        }
        
        private void AssociateFiles()
        {
            while (associationWaitTimer < associationWaitTime)
            {
                associationWaitTimer += Game.FrameTime;
                GameFiber.Yield();
                
                if (Game.IsKeyDown(Keys.Y))
                {
                    try
                    {
                        CreateFileAssociation();
                    }
                    catch (SecurityException ex)
                    {
                        Logger.Log($"The plugin attempted to associate .lc files with Notepad, but was denied access: {ex.Message}");
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        Logger.Log($"The plugin attempted to associate .lc files with Notepad, but was denied access: {ex.Message}");
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex.Message);
                    }
                    associationPrompt.Stop();
                    break;
                }
            }
        }

        private bool LCNotAssociated()
        {
            string extension = ".lc";
            Dictionary<string, RegistryKey> registryBasePaths = new Dictionary<string, RegistryKey> {
                { @"Software\Classes\" + extension, Registry.CurrentUser },
                { @"Software\Classes\" + extension, Registry.LocalMachine },
                { extension, Registry.ClassesRoot }
            };

            foreach (KeyValuePair<string, RegistryKey> item in registryBasePaths)
            {
                using (RegistryKey key = item.Value.OpenSubKey(item.Key))
                {
                    if (key != null)
                    {
                        return false;  // Association exists
                    }
                }
            }

            return true;  // No association found
        }
        
        private void CreateFileAssociation()
        {
            string extension = ".lc";
            string keyName = @"Software\Classes\" + extension;

            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(keyName))
            {
                if (key != null)
                {
                    key.SetValue("", "LiteConfigFile");
                    using (RegistryKey subKey = key.CreateSubKey(@"shell\open\command"))
                    {
                        subKey.SetValue("", "notepad.exe \"%1\"");
                        Logger.Log($"{extension} files are now associated with Notepad.");
                    }
                }
                else
                {
                    Logger.Log("Failed to create file association.");
                }
            }
        }

        public override void Finally()
        {
            Logger.Log($"ELSSirenExtender v {version} has been cleaned up");
        }
        
        private void OnOnDutyStateChangedHandler(bool onDuty)
        {
            if (onDuty)
            {
                Logger.Log($"ELSSirenExtender v {version} has been loaded");

                Notification.DisplayNotification(TimeSpan.FromSeconds(15), Color.FromArgb(150, 0, 0, 0),
                    $"ELSSirenExtender v {version}", "by SteveOhByte", "has been loaded");
            }
        }
    }
}