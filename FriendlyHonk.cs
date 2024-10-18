using System.Collections.Generic;
using System.Linq;
using Rage;
using Rage.Native;

namespace ELSSirenExtender
{
    public static class FriendlyHonk
    {
        public static void Start()
        {
            while (true)
            {
                Vehicle playerVehicle = Game.LocalPlayer.Character.CurrentVehicle;

                if (IsInVehicleWithSiren(playerVehicle))
                {
                    if (IsYelpKeyPressed())
                    {
                        HandleYelpSiren(playerVehicle);
                    }
                    else if (IsHornKeyPressed())
                    {
                        HandleHorn(playerVehicle);
                    }
                }

                GameFiber.Yield();
            }
        }

        private static bool IsInVehicleWithSiren(Vehicle vehicle)
        {
            return vehicle != null && vehicle.HasSiren;
        }

        private static bool IsYelpKeyPressed()
        {
            return Game.IsKeyDown(Main.yelpKey) || Game.IsControllerButtonDown(Main.yelpButton);
        }

        private static bool IsHornKeyPressed()
        {
            return Game.IsKeyDown(Main.hornKey) || Game.IsControllerButtonDown(Main.hornButton);
        }

        private static IEnumerable<Vehicle> GetNearbyVehiclesWithSiren(Vehicle playerVehicle, float range = 30f)
        {
            // Return vehicles nearby with sirens that are not the player's vehicle
            return World.GetAllVehicles().Where(v => v != null &&
                                                     v != playerVehicle &&
                                                     v.HasSiren &&
                                                     v.HasDriver &&
                                                     v.DistanceTo2D(Game.LocalPlayer.Character.Position) <= range &&
                                                     !v.IsSirenOn);
        }

        private static void HandleYelpSiren(Vehicle playerVehicle)
        {
            foreach (Vehicle v in GetNearbyVehiclesWithSiren(playerVehicle))
            {
                GameFiber.Sleep(500); // Delay to avoid flooding the system
                v.BlipSiren(false);
                break; // Only activate for the first matching vehicle
            }
        }

        private static void HandleHorn(Vehicle playerVehicle)
        {
            foreach (Vehicle v in GetNearbyVehiclesWithSiren(playerVehicle))
            {
                GameFiber.Sleep(500); // Delay to avoid flooding the system
                NativeFunction.Natives.START_VEHICLE_HORN(v, 10, "NORMAL", false);
                break; // Only activate for the first matching vehicle
            }
        }
    }
}