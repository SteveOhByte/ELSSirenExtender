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
        public static Keys lightStageKey = Keys.J;
        public static Keys hornKey = Keys.E;
        public static Keys yelpKey = Keys.R;
        public static ControllerButtons hornButton = ControllerButtons.LeftThumb;
        public static ControllerButtons yelpButton = ControllerButtons.B;

        private static string config = AppDomain.CurrentDomain.BaseDirectory + @"\plugins\LSPDFR\ELSSirenExtender\config.lc";
        private static bool enableSirenCutoff = true;
        private static bool enableFriendlyHonk = true;
        private static bool enableYieldVehicles = true;
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
                LC.WriteValue(config, "AI vehicles go around when your lights are on", true);
                LC.WriteValue(config, "Leave engine running when exiting vehicle", false);
            }
            else
            {
                enableSirenCutoff = LC.ReadBool(config, "Cutoff siren when exiting vehicle");
                enableFriendlyHonk = LC.ReadBool(config, "AI-cops honk back to you");
                enableYieldVehicles = LC.ReadBool(config, "AI vehicles go around when your lights are on");
                enableLeaveEngineRunning = LC.ReadBool(config, "Leave engine running when exiting vehicle");
            }
            
            string elsIniFile = AppDomain.CurrentDomain.BaseDirectory + @"\ELS.ini";
            if (!File.Exists(elsIniFile))
            {
                Logger.Error("ELS.ini not found! Is ELS installed?");
                Notification.DisplayNotification(TimeSpan.FromSeconds(15), Color.FromArgb(150, 100, 0, 0), "ELSSirenExtender", "Error", "ELS.ini not found!");
                return;
            }
            
            IniReader iniReader = new IniReader(elsIniFile);
            if (!int.TryParse(iniReader.GetString("CONTROL", "Toggle_LSTG", "74"), out int lightStageKeyInt))
                Logger.Error("Invalid value for Toggle_LSTG in ELS.ini. Defaulting to 74.");

            if (!int.TryParse(iniReader.GetString("CONTROL", "Sound_Horn", "87"), out int hornKeyInt))
                Logger.Error("Invalid value for Sound_Horn in ELS.ini. Defaulting to 87.");
            
            if (!int.TryParse(iniReader.GetString("CONTROL", "Sound_Manul", "84"), out int yelpKeyInt))
                Logger.Error("Invalid value for Sound_Manul in ELS.ini. Defaulting to 84.");

            lightStageKey = (Keys)lightStageKeyInt;
            hornKey = (Keys)hornKeyInt;
            yelpKey = (Keys)yelpKeyInt;

            if (enableSirenCutoff)
                GameFiber.StartNew(SirenCutoff.Start, "Siren Cutoff Fibre");
            if (enableFriendlyHonk)
                GameFiber.StartNew(FriendlyHonk.Start, "Friendly Honk Fibre");
            if (enableYieldVehicles)
                GameFiber.StartNew(YieldVehicles.Start, "Yield Vehicles Fibre");
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
            // Use HashSet to avoid adding duplicate keys.
            HashSet<string> registryBasePaths = new HashSet<string> {
                @"Software\Classes\" + extension,
            };

            foreach (string registryPath in registryBasePaths)
            {
                // Check Registry.CurrentUser
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(registryPath))
                {
                    if (key != null)
                    {
                        return false;  // Association exists
                    }
                }
        
                // Check Registry.LocalMachine
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(registryPath))
                {
                    if (key != null)
                    {
                        return false;  // Association exists
                    }
                }

                // Check Registry.ClassesRoot
                using (RegistryKey key = Registry.ClassesRoot.OpenSubKey(registryPath))
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