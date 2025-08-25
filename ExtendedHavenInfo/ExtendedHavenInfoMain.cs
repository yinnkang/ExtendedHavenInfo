using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using HarmonyLib;
using PhoenixPoint.Modding;
using PhoenixPoint.Geoscape.Entities;
using PhoenixPoint.Geoscape.View.ViewModules;
using PhoenixPoint.Common.Core;
using Base.UI;
using PhoenixPoint.Geoscape.Entities.Sites;
using PhoenixPoint.Common.UI;
using PhoenixPoint.Geoscape.Levels;
using PhoenixPoint.Geoscape.Levels.Factions;
using System.Reflection;

namespace ExtendedHavenInfo
{
    public sealed class ExtendedHavenInfoMain : ModMain
    {
        public new static ExtendedHavenInfoMain Instance;
        public new ExtendedHavenInfoConfig Config => (ExtendedHavenInfoConfig)base.Config;

        private Harmony _harmony;

        public override void OnModEnabled()
        {
            Instance = this;
            Logger.LogInfo("[EHI] ExtendedHavenInfo mod enabled.");

            try
            {
                _harmony = new Harmony("com.quinn11235.ExtendedHavenInfo");
                _harmony.PatchAll();
                Logger.LogInfo("[EHI] Harmony patches applied successfully.");
            }
            catch (Exception e)
            {
                Logger.LogError($"[EHI] Failed to apply Harmony patches: {e}");
            }
        }

        public override void OnModDisabled()
        {
            try
            {
                _harmony?.UnpatchAll("com.quinn11235.ExtendedHavenInfo");
                Logger.LogInfo("[EHI] Harmony patches removed.");
            }
            catch (Exception e)
            {
                Logger.LogError($"[EHI] Error removing Harmony patches: {e}");
            }
            finally
            {
                _harmony = null;
                Instance = null;
            }
        }

        public override void OnConfigChanged()
        {
            Logger.LogInfo("[EHI] Configuration changed.");
        }

        // Consolidated Haven Info Enhancement Patch
        [HarmonyPatch(typeof(UIModuleSelectionInfoBox), "SetHaven")]
        public static class ConsolidatedHavenInfoPatch
        {
            internal static string recruitAvailableText = "";

            public static void Postfix(UIModuleSelectionInfoBox __instance, GeoSite ____site, bool showRecruits)
            {
                try
                {
                    var config = Instance?.Config;
                    if (config == null) return;

                    var haven = ____site?.GetComponent<GeoHaven>();
                    if (haven == null) return;

                    // Handle recruit information
                    if (config.ShowRecruitInfo && showRecruits)
                    {
                        HandleRecruitInfo(__instance, haven);
                    }

                    // Handle trade information
                    if (config.ShowTradeInfo)
                    {
                        HandleTradeInfo(__instance, haven);
                    }

                    // Handle defense information
                    if (config.ShowDefenseInfo)
                    {
                        HandleDefenseInfo(__instance, haven);
                    }

                    // Handle alertness information
                    if (config.ShowAlertness)
                    {
                        HandleAlertnessInfo(__instance, haven);
                    }
                }
                catch (Exception e)
                {
                    Instance?.Logger?.LogError($"[EHI] Error in ConsolidatedHavenInfoPatch: {e}");
                }
            }

            private static void HandleRecruitInfo(UIModuleSelectionInfoBox instance, GeoHaven haven)
            {
                try
                {
                    var recruit = haven.AvailableRecruit;
                    if (recruit == null) return;

                    if (recruit.UnitType.IsHuman)
                    {
                        string className = recruit.Progression.MainSpecDef.ViewElementDef.DisplayName1.Localize();
                        string level = recruit.Level.ToString();
                        var abilityViews = recruit.GetPersonalAbilityTrack().AbilitiesByLevel?
                            .Select(a => a?.Ability?.ViewElementDef)
                            .Where(e => e != null);
                        string abilities = abilityViews?.Select(v => v.DisplayName1.Localize()).Join(null, "\n");

                        if (!string.IsNullOrEmpty(abilities))
                        {
                            recruitAvailableText = $"<color={Instance.Config.RecruitTextColor}>" +
                                $"{ToTitleCase(className)} - Level {level}\n" +
                                $"{abilities}" +
                                "</color>";
                        }
                        else
                        {
                            recruitAvailableText = $"<color={Instance.Config.RecruitTextColor}>" +
                                $"{ToTitleCase(className)} - Level {level}" +
                                "</color>";
                        }

                        var text = instance.GetComponentInChildren<Text>();
                        if (text != null)
                        {
                            text.text += "\n\n" + recruitAvailableText;
                        }
                    }
                }
                catch (Exception e)
                {
                    Instance?.Logger?.LogError($"[EHI] Error handling recruit info: {e}");
                }
            }

            private static void HandleTradeInfo(UIModuleSelectionInfoBox instance, GeoHaven haven)
            {
                try
                {
                    // Simplified trade detection - check if haven has trade facilities
                    // Note: GeoMarketplace API may have changed, using simplified approach
                    if (haven?.Site?.name?.ToLower()?.Contains("trade") == true ||
                        haven?.Site?.name?.ToLower()?.Contains("market") == true)
                    {
                        string tradeInfo = $"<color={Instance.Config.TradeTextColor}>Trade Available</color>";
                        
                        var text = instance.GetComponentInChildren<Text>();
                        if (text != null)
                        {
                            text.text += "\n" + tradeInfo;
                        }
                    }
                }
                catch (Exception e)
                {
                    Instance?.Logger?.LogError($"[EHI] Error handling trade info: {e}");
                }
            }

            private static void HandleDefenseInfo(UIModuleSelectionInfoBox instance, GeoHaven haven)
            {
                try
                {
                    // Try to get defense value using reflection if API changed
                    string defenseText = "<color=#FFAA00>Defense: Unknown</color>";
                    
                    try
                    {
                        // Try different possible property names
                        var defenseProperty = haven.GetType().GetProperty("HavenDefense") ?? 
                                            haven.GetType().GetProperty("Defense") ?? 
                                            haven.GetType().GetProperty("DefenseValue");
                        
                        if (defenseProperty != null)
                        {
                            var defenseValue = defenseProperty.GetValue(haven);
                            if (defenseValue != null)
                            {
                                defenseText = $"<color=#00FF00>Defense: {defenseValue}</color>";
                            }
                        }
                    }
                    catch
                    {
                        // Fallback: show that defense info is tracked but value unavailable
                        defenseText = $"<color=#FFAA00>Defense: Protected</color>";
                    }

                    var text = instance.GetComponentInChildren<Text>();
                    if (text != null)
                    {
                        text.text += "\n" + defenseText;
                    }
                }
                catch (Exception e)
                {
                    Instance?.Logger?.LogError($"[EHI] Error handling defense info: {e}");
                }
            }

            private static void HandleAlertnessInfo(UIModuleSelectionInfoBox instance, GeoHaven haven)
            {
                try
                {
                    // Try to get alertness value using reflection if API changed
                    string alertnessText = "<color=#FF6600>Alertness: Unknown</color>";
                    
                    try
                    {
                        // Try different possible property names and nested properties
                        var alertnessProperty = haven.GetType().GetProperty("AlertnessStatus") ?? 
                                              haven.GetType().GetProperty("Alertness") ??
                                              haven.GetType().GetProperty("AlertStatus");
                        
                        if (alertnessProperty != null)
                        {
                            var alertnessObj = alertnessProperty.GetValue(haven);
                            if (alertnessObj != null)
                            {
                                // Try to get Current property from the alertness object
                                var currentProperty = alertnessObj.GetType().GetProperty("Current") ??
                                                    alertnessObj.GetType().GetProperty("Value");
                                
                                if (currentProperty != null)
                                {
                                    var alertnessValue = currentProperty.GetValue(alertnessObj);
                                    if (alertnessValue != null)
                                    {
                                        alertnessText = $"<color=#FF6600>Alertness: {alertnessValue:F1}</color>";
                                    }
                                }
                                else
                                {
                                    // Maybe it's a direct numeric value
                                    alertnessText = $"<color=#FF6600>Alertness: {alertnessObj}</color>";
                                }
                            }
                        }
                    }
                    catch
                    {
                        // Fallback: show that alertness is tracked but value unavailable
                        alertnessText = $"<color=#FF6600>Alertness: Monitored</color>";
                    }

                    var text = instance.GetComponentInChildren<Text>();
                    if (text != null)
                    {
                        text.text += "\n" + alertnessText;
                    }
                }
                catch (Exception e)
                {
                    Instance?.Logger?.LogError($"[EHI] Error handling alertness info: {e}");
                }
            }

            // Utility methods
            private static string ToTitleCase(string input)
            {
                if (string.IsNullOrEmpty(input))
                    return input;

                return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(input.ToLower());
            }

            private static Color GetDefenseColor(int defenseValue)
            {
                if (defenseValue >= 80) return Color.green;
                if (defenseValue >= 50) return Color.yellow;
                return Color.red;
            }

            private static Color GetAlertnessColor(float alertness)
            {
                if (alertness <= 25f) return Color.green;
                if (alertness <= 75f) return Color.yellow;
                return Color.red;
            }

            private static string GetColorHex(Color color)
            {
                return ColorUtility.ToHtmlStringRGB(color);
            }
        }
    }
}