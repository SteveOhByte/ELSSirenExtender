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
                    for (int i = 0; i < 4; i++)
                    {
                        if (Input.IsGamepadConnected())
                            Input.DPadPress(Input.Direction.LEFT);
                        else
                            Input.KeyPress(Main.lightStageKey);
                        GameFiber.Sleep(10);
                    }
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