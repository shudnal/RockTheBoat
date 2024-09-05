using System;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using ServerSync;

namespace RockTheBoat
{
    [BepInPlugin(pluginID, pluginName, pluginVersion)]
    public class RockTheBoat : BaseUnityPlugin
    {
        const string pluginID = "shudnal.RockTheBoat";
        const string pluginName = "Rock the Boat";
        const string pluginVersion = "1.0.6";

        private readonly Harmony harmony = new Harmony(pluginID);

        internal static readonly ConfigSync configSync = new ConfigSync(pluginID) { DisplayName = pluginName, CurrentVersion = pluginVersion, MinimumRequiredVersion = pluginVersion };

        private static ConfigEntry<bool> modEnabled;
        private static ConfigEntry<bool> configLocked;

        private static ConfigEntry<bool> loggingEnabled;

        private static ConfigEntry<float> cameraMaxDistanceOnBoat;
        private static ConfigEntry<bool> cameraSetMaxDistanceOnShip;

        private static ConfigEntry<float> exploreRadiusMultiplier;

        private static ConfigEntry<bool> removeShipWithHammer;
        private static ConfigEntry<bool> preventDrowningNearTheShip;

        private static ConfigEntry<float> emptyShipDamageMultiplier;
        private static ConfigEntry<float> playerDamageTakenMultiplier;
        private static ConfigEntry<float> impactDamageMultiplier;
        private static ConfigEntry<float> shipDamageToPlayerBuildings;

        private static ConfigEntry<float> sailingSpeedMultiplier;
        private static ConfigEntry<float> backwardSpeedMultiplier;
        private static ConfigEntry<float> steeringSpeedMultiplier;
        private static ConfigEntry<float> paddlingSteerSpeedMultiplier;
        private static ConfigEntry<float> perRowerSpeedMultiplier;

        private static ConfigEntry<bool> nitroEnabled;

        internal static float nitroDownTime;
        internal static bool nitroStaminaDepleted;

        internal static RockTheBoat instance;

        private void Awake()
        {
            harmony.PatchAll();

            instance = this;

            ConfigInit();
            _ = configSync.AddLockingConfigEntry(configLocked);

            Game.isModded = true;
        }

        private void OnDestroy()
        {
            Config.Save();
            instance = null;
            harmony?.UnpatchSelf();
        }

        public static void LogInfo(object data)
        {
            if (loggingEnabled.Value)
                instance.Logger.LogInfo(data);
        }

        private void ConfigInit()
        {
            config("General", "NexusID", 2525, "Nexus mod ID for updates", false);

            modEnabled = config("General", "Enabled", defaultValue: true, "Enable the mod");
            configLocked = config("General", "Lock Configuration", defaultValue: true, "Configuration is locked and can be changed by server admins only.");
            loggingEnabled = config("General", "Enable logging", defaultValue: false, "Enable logging for ward events. [Not Synced with Server]", false);

            cameraMaxDistanceOnBoat = config("Camera", "Max distance on boat", defaultValue: 16f, "Maximum camera distance while attached to boat");
            cameraSetMaxDistanceOnShip = config("Camera", "Set max distance on boat", defaultValue: true, "Set boat camera distance while you attached to boat whether or not you are controlling the ship");

            cameraMaxDistanceOnBoat.SettingChanged += (sender, args) => PatchCameraDistance();

            exploreRadiusMultiplier = config("Minimap", "Explore radius multiplier", defaultValue: 1.0f, "Set multiplier for map explore radius while you are on boat");

            playerDamageTakenMultiplier = config("Damage", "Player damage taken multiplier", defaultValue: 1.0f, "Damage taken by players on board from creatures");
            emptyShipDamageMultiplier = config("Damage", "Empty ship damage multiplier", defaultValue: 1.0f, "Set multiplier for ship damage taken when no one are on board");
            impactDamageMultiplier = config("Damage", "Impact damage multiplier", defaultValue: 1.0f, "Set multiplier for ship damage taken on terrain hit");
            shipDamageToPlayerBuildings = config("Damage", "Damage done to player buildings multiplier", defaultValue: 1.0f, "Set multiplier when ship damages player buildings");

            removeShipWithHammer = config("Misc", "Remove ship with hammer", defaultValue: false, "Ships become removable by hammer like good old times. Relog required on change.");
            preventDrowningNearTheShip = config("Misc", "Prevent drowning near the ship", defaultValue: true, "Prevents drowning if you are touching the ship");

            sailingSpeedMultiplier = config("Speed", "Sailing speed multiplier", defaultValue: 1.0f, "Wind force applied to ship when sails is up");
            backwardSpeedMultiplier = config("Speed", "Backward speed multiplier", defaultValue: 1.0f, "Paddling force applied to ship when sails is down");
            steeringSpeedMultiplier = config("Speed", "Steering speed multiplier", defaultValue: 1.0f, "Speed of steering (direction change) that depends on forward speed, faster you move faster you steer.");
            paddlingSteerSpeedMultiplier = config("Speed", "Steering paddling speed multiplier", defaultValue: 1.0f, "Speed of steering (direction change) when paddling, independent of forward speed.");
            perRowerSpeedMultiplier = config("Speed", "Paddling force per rower multiplier", defaultValue: 0.25f, "Speed of paddling (forward and steering) added per every other player attached to the ship.");
            nitroEnabled = config("Speed", "Use stamina to boost steering speed", defaultValue: true, "Press Run button to boost steering. Stamina will be depleting.");
        }

        private static bool IsTouchingShip()
        {
            return Ship.GetLocalShip() != null;
        }

        private static bool IsValheimRaftShip(Ship ship)
        {
            return ship.name.StartsWith("MBRaft") || ship.GetComponents<Component>().Any(comp => comp.GetType().Name == "MoveableBaseShipComponent");
        }

        private static void ModifyHitDamage(ref HitData hit, float value)
        {
            hit.m_damage.Modify(Math.Max(value, 0));
        }

        ConfigEntry<T> config<T>(string group, string name, T defaultValue, ConfigDescription description, bool synchronizedSetting = true)
        {
            ConfigEntry<T> configEntry = Config.Bind(group, name, defaultValue, description);

            SyncedConfigEntry<T> syncedConfigEntry = configSync.AddConfigEntry(configEntry);
            syncedConfigEntry.SynchronizedConfig = synchronizedSetting;

            return configEntry;
        }

        ConfigEntry<T> config<T>(string group, string name, T defaultValue, string description, bool synchronizedSetting = true) => config(group, name, defaultValue, new ConfigDescription(description), synchronizedSetting);

        internal static void PatchCameraDistance()
        {
            if (!modEnabled.Value)
                return;

            if (GameCamera.instance == null)
                return;

            if (cameraMaxDistanceOnBoat.Value > 0)
                GameCamera.instance.m_maxDistanceBoat = cameraMaxDistanceOnBoat.Value;
        }

        internal static void PatchShipProperties(Ship ship)
        {
            string prefabName = Utils.GetPrefabName(ship.gameObject);

        }

        [HarmonyPatch(typeof(GameCamera), nameof(GameCamera.Awake))]
        public static class GameCamera_Awake_CameraSettings
        {
            public static void Postfix()
            {
                PatchCameraDistance();
            }
        }
        
        [HarmonyPatch(typeof(GameCamera), nameof(GameCamera.UpdateCamera))]
        public static class GameCamera_UpdateCamera_MaxDistanceOnBoat
        {
            [HarmonyPriority(Priority.Last)]
            public static void Prefix(GameCamera __instance, ref float __state)
            {
                if (!modEnabled.Value)
                    return;

                if (!cameraSetMaxDistanceOnShip.Value)
                    return;

                if (Player.m_localPlayer == null || !Player.m_localPlayer.IsAttachedToShip())
                    return;

                __state = __instance.m_maxDistance;

                __instance.m_maxDistance = __instance.m_maxDistanceBoat;
            }

            [HarmonyPriority(Priority.First)]
            public static void Postfix(GameCamera __instance, float __state)
            {
                if (__state != 0f)
                    __instance.m_maxDistance = __state;
            }
        }

        [HarmonyPatch(typeof(Minimap), nameof(Minimap.UpdateExplore))]
        public static class Minimap_UpdateExplore_ExploreRadiusOnBoat
        {
            [HarmonyPriority(Priority.Last)]
            public static void Prefix(Minimap __instance, Player player, ref float __state)
            {
                if (!modEnabled.Value)
                    return;

                if (exploreRadiusMultiplier.Value == 1.0f)
                    return;

                if (player == Player.m_localPlayer && IsTouchingShip())
                {
                    __state = __instance.m_exploreRadius;
                    __instance.m_exploreRadius *= exploreRadiusMultiplier.Value;
                }
            }

            [HarmonyPriority(Priority.First)]
            public static void Postfix(Minimap __instance, Player player, ref float __state)
            {
                if (__state != 0f)
                    __instance.m_exploreRadius = __state;
            }
        }

        [HarmonyPatch(typeof(Piece), nameof(Piece.Awake))]
        public static class Piece_Awake_ShipRemovableByHammer
        {
            public static void Postfix(Piece __instance)
            {
                if (!modEnabled.Value) return;

                if (__instance.m_canBeRemoved) return;

                if (!removeShipWithHammer.Value) return;

                Ship componentInChildren = __instance.GetComponentInChildren<Ship>();

                if (componentInChildren == null || IsValheimRaftShip(componentInChildren)) return;

                __instance.m_canBeRemoved = true;
            }
        }

        [HarmonyPatch(typeof(Ship), nameof(Ship.CustomFixedUpdate))]
        public static class Ship_CustomFixedUpdate_ShipSpeed
        {
            public static float m_sailForceFactor;
            public static float m_backwardForce;
            public static float m_stearVelForceFactor;
            public static float m_stearForce;

            private static void DepleteShipRunStamina(Player player, bool checkRun, float dt)
            {
                if (!checkRun)
                    return;

                bool flag = player.HaveStamina();

                player.UseStamina(dt * 15f * Game.m_moveStaminaRate);

                if (flag && !player.HaveStamina())
                {
                    nitroStaminaDepleted = true;
                    Hud.instance.StaminaBarEmptyFlash();
                }
            }

            [HarmonyPriority(Priority.Last)]
            public static void Prefix(Ship __instance, float fixedDeltaTime)
            {
                if (!modEnabled.Value)
                    return;

                if ((bool)__instance.m_nview && !__instance.m_nview.IsOwner())
                    return;

                m_sailForceFactor = __instance.m_sailForceFactor;
                m_backwardForce = __instance.m_backwardForce;
                m_stearVelForceFactor = __instance.m_stearVelForceFactor;
                m_stearForce = __instance.m_stearForce;

                __instance.m_sailForceFactor *= sailingSpeedMultiplier.Value; // Wind force 
                __instance.m_backwardForce *= backwardSpeedMultiplier.Value; // Paddling force
                __instance.m_stearVelForceFactor *= steeringSpeedMultiplier.Value; // Steering speed, amount of angle change per fixed frame, wind and paddling
                __instance.m_stearForce *= paddlingSteerSpeedMultiplier.Value; // Steering speed (paddling only)

                int rowersCount = __instance.m_players.Count(player => player.IsAttachedToShip() && player.GetControlledShip() != __instance);
                __instance.m_stearForce *= 1f + perRowerSpeedMultiplier.Value * rowersCount;
                __instance.m_backwardForce *= 1f + perRowerSpeedMultiplier.Value * rowersCount;

                if (nitroEnabled.Value && Player.m_localPlayer != null && Player.m_localPlayer.GetControlledShip() == __instance)
                {
                    nitroStaminaDepleted = nitroStaminaDepleted || !Player.m_localPlayer.HaveStamina();

                    bool nitro = ZInput.GetButton("Run") || ZInput.GetButton("JoyRun");
                    if (nitro)
                        nitroDownTime += (fixedDeltaTime * 1.25f);
                    else
                    {
                        nitroDownTime = 0f;
                        nitroStaminaDepleted = false;
                    }

                    float shift = nitro && !nitroStaminaDepleted ? 1.5f + Mathf.Min(1f, nitroDownTime / 5) : 1;

                    __instance.m_backwardForce *= shift;
                    __instance.m_stearForce *= shift;

                    DepleteShipRunStamina(Player.m_localPlayer, shift > 1, fixedDeltaTime);
                }
            }

            [HarmonyPriority(Priority.First)]
            public static void Postfix(Ship __instance)
            {
                if (!modEnabled.Value)
                    return;

                if ((bool)__instance.m_nview && !__instance.m_nview.IsOwner())
                    return;

                __instance.m_sailForceFactor = m_sailForceFactor;
                __instance.m_backwardForce = m_backwardForce;
                __instance.m_stearVelForceFactor = m_stearVelForceFactor;
                __instance.m_stearForce = m_stearForce;
            }
        }

        [HarmonyPatch(typeof(Ship), nameof(Ship.UpdateUpsideDmg))]
        public static class Ship_UpdateUpsideDmg_PreventShipDamage
        {
            private static void Prefix(Ship __instance, ref float ___m_upsideDownDmg, ref float __state)
            {
                if (!modEnabled.Value) 
                    return;

                if (emptyShipDamageMultiplier.Value == 1.0f)
                    return;

                if (__instance.HasPlayerOnboard())
                    return;

                __state = ___m_upsideDownDmg;

                ___m_upsideDownDmg *= emptyShipDamageMultiplier.Value;
            }

            private static void Postfix(Ship __instance, ref float ___m_upsideDownDmg, float __state)
            {
                if (!modEnabled.Value)
                    return;

                if (__state == 0f)
                    return;

                ___m_upsideDownDmg = __state;
            }
        }

        [HarmonyPatch(typeof(Ship), nameof(Ship.UpdateWaterForce))]
        public static class Ship_UpdateWaterForce_PreventShipDamage
        {
            private static void Prefix(Ship __instance, ref float ___m_waterImpactDamage, ref float __state)
            {
                if (!modEnabled.Value)
                    return;

                if (emptyShipDamageMultiplier.Value == 1.0f)
                    return;

                if (__instance.HasPlayerOnboard())
                    return;

                __state = ___m_waterImpactDamage;

                ___m_waterImpactDamage *= emptyShipDamageMultiplier.Value;
            }

            private static void Postfix(Ship __instance, ref float ___m_waterImpactDamage, float __state)
            {
                if (!modEnabled.Value)
                    return;

                if (__state == 0f)
                    return;

                ___m_waterImpactDamage = __state;
            }
        }

        [HarmonyPatch(typeof(Player), nameof(Player.OnSwimming))]
        public static class Player_OnSwimming_PreventDrowningNearTheShip
        {
            public static void Prefix(Player __instance, ref float ___m_drownDamageTimer)
            {
                if (!modEnabled.Value)
                    return;

                if (!preventDrowningNearTheShip.Value)
                    return;

                if (__instance != Player.m_localPlayer)
                    return;

                if (IsTouchingShip())
                    ___m_drownDamageTimer = 0f;
            }
        }

        [HarmonyPatch(typeof(WearNTear), nameof(WearNTear.Damage))]
        public static class WearNTear_Damage_DamageTakenMultiplier
        {
            private static void Prefix(WearNTear __instance, ref HitData hit, ZNetView ___m_nview, Piece ___m_piece)
            {
                if (!modEnabled.Value)
                    return;

                if (___m_nview == null || !___m_nview.IsValid())
                    return;

                if (impactDamageMultiplier.Value == 1.0f && emptyShipDamageMultiplier.Value == 1.0f && shipDamageToPlayerBuildings.Value == 1.0f)
                    return;

                if (__instance.TryGetComponent(out Ship ship))
                {
                    if (hit.m_hitType == HitData.HitType.Boat && (impactDamageMultiplier.Value != 1.0f || emptyShipDamageMultiplier.Value != 1.0f))
                    {
                        if (ship.HasPlayerOnboard())
                            ModifyHitDamage(ref hit, impactDamageMultiplier.Value);
                        else
                            ModifyHitDamage(ref hit, Mathf.Min(impactDamageMultiplier.Value, emptyShipDamageMultiplier.Value));
                    }
                    else if (hit.m_hitType != HitData.HitType.PlayerHit && emptyShipDamageMultiplier.Value != 1.0f && !ship.HasPlayerOnboard())
                    {
                        if (hit.HaveAttacker() && hit.GetAttacker().IsBoss())
                            return;

                        ModifyHitDamage(ref hit, emptyShipDamageMultiplier.Value);
                    }
                }
                else if (hit.m_hitType == HitData.HitType.Boat && shipDamageToPlayerBuildings.Value != 1.0f && ___m_piece != null && ___m_piece.IsPlacedByPlayer())
                {
                    ModifyHitDamage(ref hit, shipDamageToPlayerBuildings.Value);
                }
            }
        }

        [HarmonyPatch(typeof(Character), nameof(Character.Damage))]
        public static class Character_Damage_DamageMultipliers
        {
            private static void Prefix(Character __instance, ref HitData hit, ZNetView ___m_nview)
            {
                if (!modEnabled.Value)
                    return;

                if (Player.m_localPlayer == __instance && IsTouchingShip() && (!hit.HaveAttacker() || !hit.GetAttacker().IsBoss()))
                    ModifyHitDamage(ref hit, playerDamageTakenMultiplier.Value);
            }
        }
    }
}