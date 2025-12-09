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
        
        private string elsIniFile = AppDomain.CurrentDomain.BaseDirectory + @"\ELS.ini";
        private string dlsIniFile = AppDomain.CurrentDomain.BaseDirectory + @"\plugins\DLS.ini";
        
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
            
            // Check the cases: 1) Only ELS.ini exists, 2) Only DLS.ini exists, 3) Both exist (prefer ELS.ini), 4) Neither exist
            // Case 1:
            if (File.Exists(elsIniFile) && !File.Exists(dlsIniFile))
            {
                // Only ELS.ini exists
                SetupELSBindings();
            }
            // Case 2:
            else if (!File.Exists(elsIniFile) && File.Exists(dlsIniFile))
            {
                // Only DLS.ini exists
                SetupDLSBindings();
            }
            // Case 3:
            else if (File.Exists(elsIniFile) && File.Exists(dlsIniFile))
            {
                // Both exist, prefer ELS.ini
                SetupELSBindings();
            }
            // Case 4:
            else
            {
                Logger.Error("Neither ELS.ini nor DLS.ini found! Is ELS or DLS installed?");
                Notification.DisplayNotification(TimeSpan.FromSeconds(15), Color.FromArgb(150, 100, 0, 0), "ELSSirenExtender", "Error", "Neither ELS.ini nor DLS.ini found!");
                return;
            }

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

        private void SetupELSBindings()
        {
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
        }

        private void SetupDLSBindings()
        {
            IniReader iniReader = new IniReader(dlsIniFile);
            if (!char.TryParse(iniReader.GetString("Keyboard", "LightStage", "J"), out char lightStageKeyChar))
                Logger.Error("Invalid value for LightStage in DLS.ini. Defaulting to J.");
            
            if (!char.TryParse(iniReader.GetString("Keyboard", "Horn", "Y"), out char hornKeyChar))
                Logger.Error("Invalid value for Horn in DLS.ini. Defaulting to Y.");
            
            if (!char.TryParse(iniReader.GetString("Keyboard", "Manual", "T"), out char yelpKeyChar))
                Logger.Error("Invalid value for Manual in DLS.ini. Defaulting to T.");
            
            lightStageKey = (Keys)Enum.Parse(typeof(Keys), lightStageKeyChar.ToString(), true);
            hornKey = (Keys)Enum.Parse(typeof(Keys), hornKeyChar.ToString(), true);
            yelpKey = (Keys)Enum.Parse(typeof(Keys), yelpKeyChar.ToString(), true);
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