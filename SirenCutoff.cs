using OhPluginEssentials;
using Rage;

namespace ELSSirenExtender
{
    public static class SirenCutoff
    {
        public static void Start()
        {
            while (true)
            {
                if (DetectedPlayerExiting())
                {
                    InputSimulator.KeyPress(Main.firstSirenKey);
                    
                    GameFiber.Sleep(10);
                    
                    InputSimulator.KeyPress(Main.secondSirenKey);
                    
                    GameFiber.Sleep(10);
                    
                    InputSimulator.KeyPress(Main.secondSirenKey);
                }
                GameFiber.Yield();
            }       
        }

        private static bool DetectedPlayerExiting()
        {
            return Game.LocalPlayer.Character.IsAlive && Game.LocalPlayer.Character.IsInAnyVehicle(false) &&
                   Game.LocalPlayer.Character.CurrentVehicle && Game.LocalPlayer.Character.CurrentVehicle.IsSirenOn &&
                   Game.IsControlJustPressed(0, GameControl.VehicleExit);
        }
    }
}