using Rage;

namespace ELSSirenExtender
{
    public static class LeaveEngineRunning
    {
        public static void Start()
        {
            while (true)
            {
                GameFiber.Yield();

                if (NotLeavingVehicle()) continue;
                
                if (LeavingVehicle())
                {
                    GameFiber.Wait(100);
                    Game.LocalPlayer.Character.LastVehicle.IsEngineOn = true;
                }
            }
        }

        private static bool NotLeavingVehicle()
        {
            return !Game.LocalPlayer.Character.LastVehicle?.Driver ||
                    Game.LocalPlayer.Character.LastVehicle?.Driver != Game.LocalPlayer.Character ||
                    !Game.IsControlJustPressed(0, GameControl.VehicleExit);
        }

        private static bool LeavingVehicle()
        {
            return Game.LocalPlayer.Character.LastVehicle && Game.IsControlJustPressed(0, GameControl.VehicleExit);
        }
    }
}