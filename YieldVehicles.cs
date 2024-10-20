using System;
using System.Collections.Generic;
using System.Linq;
using LSPD_First_Response.Mod.API;
using OhPluginEssentials;
using Rage;

namespace ELSSirenExtender
{
    public static class YieldVehicles
    {
        private static readonly List<Vehicle> yieldingVehicles = new List<Vehicle>();

        public static void Start()
        {
            Vector3 garage = new Vector3(396.42f, -970.0003f, -99.36382f);
            const float collectionRadius = 7;
            AppDomain.CurrentDomain.DomainUnload += TerminationHandler;
            List<Vehicle> ignoredVehicles = new List<Vehicle>();

            GameFiber.StartNew(CleanupCollectedVehicles, "Collected Vehicles Cleanup Fiber");

            while (true)
            {
                if (Game.LocalPlayer.Character.Position.DistanceTo(garage) > 5f && Game.LocalPlayer.Character.LastVehicle && Game.LocalPlayer.Character.LastVehicle.IsSirenOn && Game.LocalPlayer.Character.LastVehicle.Speed == 0f)
                {
                    Vector3 rearPos = Game.LocalPlayer.Character.LastVehicle.GetOffsetPosition(new Vector3(0, -4f, 0));
                    
                    foreach(Vehicle vehicle in Game.LocalPlayer.Character.GetNearbyVehicles(16)
                                .Where(v => v && v.FrontPosition.DistanceTo(rearPos) <= collectionRadius && v != Game.LocalPlayer.Character.LastVehicle
                                            && v.IsEngineOn && v.IsOnAllWheels && !v.IsSirenOn && !v.IsTrailer && !v.IsTrain
                                            && (Math.Abs(Game.LocalPlayer.Character.LastVehicle.Heading - v.Heading) < 90f
                                                || Math.Abs(Game.LocalPlayer.Character.LastVehicle.Heading - v.Heading) > 200f)
                                            && !yieldingVehicles.Contains(v) && !ignoredVehicles.Contains(v)))
                    {
                        if (VehicleShouldBeIgnored(vehicle) && !ignoredVehicles.Contains(vehicle))
                            ignoredVehicles.Add(vehicle);
                        else
                        {
                            SetVehicleAndDriverPersistence(vehicle);
                            yieldingVehicles.Add(vehicle);

                            GameFiber.StartNew(() => PerformYieldTasks(vehicle), "Yield Task Fiber");
                        }
                    }
                }
                GameFiber.Yield();
            } 
        }

        private static bool VehicleShouldBeIgnored(Vehicle vehicle)
        {
            if (Functions.GetCurrentPullover() != null && Functions.GetPulloverSuspect(Functions.GetCurrentPullover()) && Functions.GetPulloverSuspect(Functions.GetCurrentPullover()).CurrentVehicle == vehicle)
                return true;
            if (Functions.GetActivePursuit() != null && IsVehicleInCurrentPursuit())
                return true;
            
            return vehicle.Driver && !Peds.IsAmbient(vehicle.Driver);

            bool IsVehicleInCurrentPursuit()
            {
                return vehicle.HasDriver && vehicle.Driver && Functions.GetPursuitPeds(Functions.GetActivePursuit()).Contains(vehicle.Driver);
            }
        }

        private static void SetVehicleAndDriverPersistence(Vehicle vehicle)
        {
            vehicle.IsPersistent = true;
            if (vehicle.HasDriver)
                vehicle.Driver.IsPersistent = true;
        }

        private static void PerformYieldTasks(Vehicle vehicle)
        {
            if (!vehicle)
                return;
            if (!vehicle.Driver)
                return;

            while (vehicle && vehicle.Driver && Game.LocalPlayer.Character.LastVehicle && vehicle.FrontPosition.DistanceTo2D(Game.LocalPlayer.Character.LastVehicle.GetOffsetPosition(new Vector3(0, -4f, 0))) < 7f)
            {
                if (vehicle && vehicle.Driver && vehicle.Speed < 2f)
                {
                    vehicle.SteeringAngle = 45;
                    vehicle.Driver.Tasks.PerformDrivingManeuver(vehicle, VehicleManeuver.GoForwardWithCustomSteeringAngle, 1).WaitForCompletion();
                    if (vehicle && vehicle.Driver)
                    {
                        vehicle.Driver.Tasks.CruiseWithVehicle(5f, (VehicleDrivingFlags)558);
                    }
                }
                GameFiber.Sleep(100);
            }
            if (vehicle)
                Dismiss();

            void Dismiss()
            {
                if (!vehicle) return;
                
                if (vehicle.Driver)
                {
                    vehicle.Driver.Tasks.Clear();
                    vehicle.Driver.Dismiss();
                }
                vehicle.Dismiss();
            }
        }

        private static void CleanupCollectedVehicles()
        {
            while (true)
            {
                yieldingVehicles.RemoveAll(x => !x);
                GameFiber.Sleep(5000);
            }
        }

        private static void TerminationHandler(object sender, EventArgs e)
        {
            foreach(Vehicle v in yieldingVehicles.Where(v => v))
            {
                if (v.Driver)
                {
                    v.Driver.Dismiss();
                }
                v.Dismiss();
            }
            yieldingVehicles.Clear();
        }
    }
}