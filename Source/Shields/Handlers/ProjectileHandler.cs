﻿using System;
using System.Reflection;
using Harmony;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace FrontierDevelopments.Shields.Handlers
{
    public class ProjectileHandler
    {
        private static readonly bool Enabled = true;
        
        private static readonly FieldInfo OriginField = typeof(Projectile).GetField("origin", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo DestinationField = typeof(Projectile).GetField("destination", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo TicksToImpactField = typeof(Projectile).GetField("ticksToImpact", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly PropertyInfo StartingTicksToImpactProperty = typeof(Projectile).GetProperty("StartingTicksToImpact", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo DetonationField = typeof(CompExplosive).GetMethod("Detonate", BindingFlags.Instance | BindingFlags.NonPublic);

        static ProjectileHandler()
        {
            if (OriginField == null)
            {
                Enabled = false;
                Log.Error("Frontier Developments Shields :: Projectile handler reflection error on field Projectile.origin");
            }
            if (DestinationField == null)
            {
                Enabled = false;
                Log.Error("Frontier Developments Shields :: Projectile handler reflection error on field Projectile.destination");
            }
            if (TicksToImpactField == null)
            {
                Enabled = false;
                Log.Error("Frontier Developments Shields :: Projectile handler reflection error on field Projectile.ticksToImpact");
            }
            if (StartingTicksToImpactProperty == null)
            {
                Enabled = false;
                Log.Error("Frontier Developments Shields :: Projectile handler reflection error on property Projectile.StartingTicksToImpact");
            }
            if (DetonationField == null)
            {
                Enabled = false;
                Log.Error("Frontier Developments Shields :: Projectile handler reflection error on property CompExplosive.Detonate");
            }
            
            Log.Message("Frontier Developments Shields :: Projectile handler " + (Enabled ? "enabled" : "disabled due to errors"));
        }
        
        [HarmonyPatch(typeof(Projectile), "Tick")]
        static class Patch_Projectile_Tick
        {
            static bool Prefix(Projectile __instance)
            {
                if (!Enabled) return true;
                
                var projectile = __instance;
                    
                var ticksToImpact = (int)TicksToImpactField.GetValue(projectile);
                var startingTicksToImpact = (int)StartingTicksToImpactProperty.GetValue(projectile, null);

                var origin3 = (Vector3) OriginField.GetValue(projectile);
                var destination3 = (Vector3) DestinationField.GetValue(projectile);

                var origin = new Vector2(origin3.x, origin3.z);
                var destination = new Vector2(destination3.x, destination3.z);
                var position = Vector2.Lerp(origin, destination, 1.0f - ticksToImpact / (float)startingTicksToImpact);
                
                try
                {
                    if (projectile.def.projectile.flyOverhead)
                    {
                        // the shield has blocked the projectile - invert to get if harmony should allow the original block
                        return !Mod.ShieldManager.ImpactShield(projectile.Map, position, origin, destination, (shield, vector3) =>
                            {
                                if (shield.Damage(projectile.def.projectile.damageAmountBase, position))
                                {
                                    projectile.Destroy();
                                    return true;
                                }
                                return false;
                            });
                    }
                    var ray = new Ray2D(position, Vector2.Lerp(origin, destination, 1.0f - (ticksToImpact - 1) / (float) startingTicksToImpact));
                    
                    // the shield has blocked the projectile - invert to get if harmony should allow the original block
                    return !Mod.ShieldManager.ImpactShield(projectile.Map, origin3, ray, 1, (shield, point) =>
                    {
                        if (shield.Damage(projectile.def.projectile.damageAmountBase, point))
                        {
                            var explosive = projectile.TryGetComp<CompExplosive>();
                            if (explosive != null)
                            {
                                projectile.Position = new IntVec3((int)point.x, (int)projectile.def.Altitude, (int)point.y);
                                object[] parameters = { projectile.Map };
                                DetonationField.Invoke(explosive, parameters);
                            }
                            projectile.Destroy();
                            return true;
                        }
                        return false;
                    });
                }
                catch (InvalidOperationException) {}
                return true;
            }
        }
    }
}