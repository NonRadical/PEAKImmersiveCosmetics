//PEAK Immersive Cosmetics
//Distributed under the MIT License @ https://github.com/NonRadical/PEAKImmersiveCosmetics

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Peak.Afflictions;
using UnityEngine;
using Zorro.Core;

namespace PEAKImmersiveCosmetics
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.nonradical.peak.immersivecosmetics";
        public const string PluginName = "PEAKImmersiveCosmetics";
        public const string PluginVersion = "0.1";

        internal static ManualLogSource Log;
        internal static ConfigEntry<bool> LogActiveEffects;
        internal static ConfigEntry<bool> ShowEffectIcons;

        private bool _dumpedCosmetics;

        private void Awake()
        {
            Log = Logger;
            LogActiveEffects = Config.Bind("General", "LogActiveEffects", true,
                "Log a player's active costume effects whenever their equipped cosmetics change.");
            ShowEffectIcons = Config.Bind("General", "ShowEffectIcons", true,
                "Show icons above the stamina bar for your active costume effects. " +
                "Icons are built into the plugin; PNG files in BepInEx\\config\\PEAKImmersiveCosmetics.icons\\ override them.");
            EffectHud.EnsureIconFolder();
            gameObject.AddComponent<EffectHud>();
            gameObject.AddComponent<GloomFogShadow>();
            var harmony = new Harmony(PluginGuid);
            harmony.PatchAll(typeof(AddStatusPatch));
            harmony.PatchAll(typeof(SubtractStatusPatch));
            harmony.PatchAll(typeof(SetStatusPatch));
            harmony.PatchAll(typeof(HotbarWeightPatch));
            harmony.PatchAll(typeof(UseStaminaPatch));
            harmony.PatchAll(typeof(RegenStaminaPatch));
            harmony.PatchAll(typeof(SunHeatPatch));
            harmony.PatchAll(typeof(ActionModifyStatusPatch));
            harmony.PatchAll(typeof(ActionRestoreHungerPatch));
            harmony.PatchAll(typeof(AfflictionBlockPatch));
            harmony.PatchAll(typeof(ThornPullPatch));
            harmony.PatchAll(typeof(BeetleBonkPatch));
            harmony.PatchAll(typeof(GravityPatch));
            harmony.PatchAll(typeof(ZombieTargetPatch));
            harmony.PatchAll(typeof(JumpHeightPatch));
            harmony.PatchAll(typeof(EnergyDrinkPatch));
            harmony.PatchAll(typeof(TrapContextPatch));
            harmony.PatchAll(typeof(CursedLuggageContextPatch));
            harmony.PatchAll(typeof(AntlionAttackContextPatch));
            harmony.PatchAll(typeof(ScoutmasterThrowContextPatch));
            harmony.PatchAll(typeof(FeedItemPatch));
            harmony.PatchAll(typeof(MoveSpeedPatch));
            harmony.PatchAll(typeof(ClimbSpeedPatch_Wall));
            harmony.PatchAll(typeof(ClimbSpeedPatch_Rope));
            harmony.PatchAll(typeof(ClimbSpeedPatch_Vine));
            Log.LogInfo($"{PluginName} {PluginVersion} loaded. {EffectRegistry.Items.Count} cosmetics have effects.");
        }

        // Once the Customization singleton exists, dump every cosmetic asset name with its
        // registry effects.
        private void Update()
        {
            if (_dumpedCosmetics)
            {
                return;
            }
            Customization c = Singleton<Customization>.Instance;
            if (c == null || c.fits == null || c.fits.Length == 0)
            {
                return;
            }
            _dumpedCosmetics = true;
            try
            {
                string path = Path.Combine(Paths.ConfigPath, "PEAKImmersiveCosmetics.cosmetics.txt");
                var sb = new StringBuilder();
                sb.AppendLine("All cosmetic asset names, with the effects defined in EffectRegistry.cs.");
                sb.AppendLine();
                sb.AppendLine("Outfits:");
                for (int i = 0; i < c.fits.Length; i++)
                {
                    sb.AppendLine($"[{i}] {c.fits[i].name}  {Describe(c.fits[i].name)}");
                }
                sb.AppendLine();
                sb.AppendLine("Hats:");
                for (int i = 0; i < c.hats.Length; i++)
                {
                    sb.AppendLine($"[{i}] {c.hats[i].name}  {Describe(c.hats[i].name)}");
                }
                File.WriteAllText(path, sb.ToString());
                Log.LogInfo($"Dumped {c.fits.Length} outfits and {c.hats.Length} hats to {path}");
            }
            catch (Exception e)
            {
                Log.LogWarning($"Failed to dump cosmetic names: {e}");
            }
        }

        private static string Describe(string assetName)
        {
            ItemEffects effects = EffectRegistry.Get(assetName);
            return effects == null ? "" : $"-> {effects.Description}";
        }
    }

    /**
     * Resolves which ItemEffects apply to a character's equipped outfit and hat, and
     * applies the hooks. Results are cached per player until their cosmetics change.
     */
    internal static class EffectResolver
    {
        private class CacheEntry
        {
            public int Fit = -1;
            public int Hat = -1;
            public ItemEffects OutfitEffects;
            public ItemEffects HatEffects;
            public ItemEffects SetBonusEffects;
        }

        private static readonly Dictionary<int, CacheEntry> Cache = new Dictionary<int, CacheEntry>();

        public static float ModifyStatus(Character character, CharacterAfflictions.STATUSTYPE statusType, float amount)
        {
            CacheEntry entry = Resolve(character);
            if (entry == null)
            {
                return amount;
            }
            if (entry.OutfitEffects?.ModifyStatus != null)
            {
                amount = entry.OutfitEffects.ModifyStatus(character, statusType, amount);
            }
            if (entry.HatEffects?.ModifyStatus != null)
            {
                amount = entry.HatEffects.ModifyStatus(character, statusType, amount);
            }
            if (entry.SetBonusEffects?.ModifyStatus != null)
            {
                amount = entry.SetBonusEffects.ModifyStatus(character, statusType, amount);
            }
            return amount;
        }

        public static float ModifyStatusHeal(Character character, CharacterAfflictions.STATUSTYPE statusType, float amount)
        {
            CacheEntry entry = Resolve(character);
            if (entry == null)
            {
                return amount;
            }
            if (entry.OutfitEffects?.ModifyStatusHeal != null)
            {
                amount = entry.OutfitEffects.ModifyStatusHeal(character, statusType, amount);
            }
            if (entry.HatEffects?.ModifyStatusHeal != null)
            {
                amount = entry.HatEffects.ModifyStatusHeal(character, statusType, amount);
            }
            if (entry.SetBonusEffects?.ModifyStatusHeal != null)
            {
                amount = entry.SetBonusEffects.ModifyStatusHeal(character, statusType, amount);
            }
            return amount;
        }

        public static float ModifyStaminaDrain(Character character, float usage)
        {
            CacheEntry entry = Resolve(character);
            if (entry == null)
            {
                return usage;
            }
            
            if (entry.OutfitEffects?.ModifyStaminaDrain != null)
            {
                usage = entry.OutfitEffects.ModifyStaminaDrain(character, usage);
            }
            if (entry.HatEffects?.ModifyStaminaDrain != null)
            {
                usage = entry.HatEffects.ModifyStaminaDrain(character, usage);
            }
            if (entry.SetBonusEffects?.ModifyStaminaDrain != null)
            {
                usage = entry.SetBonusEffects.ModifyStaminaDrain(character, usage);
            }

            if (!character.data.isGrounded)
            {
                if (entry.OutfitEffects?.ModifyStaminaDrainInAir != null)
                {
                    usage = entry.OutfitEffects.ModifyStaminaDrainInAir(character, usage);
                }
                if (entry.HatEffects?.ModifyStaminaDrainInAir != null)
                {
                    usage = entry.HatEffects.ModifyStaminaDrainInAir(character, usage);
                }
                if (entry.SetBonusEffects?.ModifyStaminaDrainInAir != null)
                {
                    usage = entry.SetBonusEffects.ModifyStaminaDrainInAir(character, usage);
                }
            }
            
            return usage;
        }

        public static float ModifyStaminaRegen(Character character, float regen)
        {
            CacheEntry entry = Resolve(character);
            if (entry == null)
            {
                return regen;
            }
            if (entry.OutfitEffects?.ModifyStaminaRegen != null)
            {
                regen = entry.OutfitEffects.ModifyStaminaRegen(character, regen);
            }
            if (entry.HatEffects?.ModifyStaminaRegen != null)
            {
                regen = entry.HatEffects.ModifyStaminaRegen(character, regen);
            }
            if (entry.SetBonusEffects?.ModifyStaminaRegen != null)
            {
                regen = entry.SetBonusEffects.ModifyStaminaRegen(character, regen);
            }
            return regen;
        }

        public static float ModifyMoveSpeed(Character character, float force)
        {
            CacheEntry entry = Resolve(character);
            if (entry == null)
            {
                return force;
            }
            if (entry.OutfitEffects?.ModifyMoveSpeed != null)
            {
                force = entry.OutfitEffects.ModifyMoveSpeed(character, force);
            }
            if (entry.HatEffects?.ModifyMoveSpeed != null)
            {
                force = entry.HatEffects.ModifyMoveSpeed(character, force);
            }
            if (entry.SetBonusEffects?.ModifyMoveSpeed != null)
            {
                force = entry.SetBonusEffects.ModifyMoveSpeed(character, force);
            }
            return force;
        }

        public static float ModifyClimbSpeed(Character character, float speedMod)
        {
            CacheEntry entry = Resolve(character);
            if (entry == null)
            {
                return speedMod;
            }
            if (entry.OutfitEffects?.ModifyClimbSpeed != null)
            {
                speedMod = entry.OutfitEffects.ModifyClimbSpeed(character, speedMod);
            }
            if (entry.HatEffects?.ModifyClimbSpeed != null)
            {
                speedMod = entry.HatEffects.ModifyClimbSpeed(character, speedMod);
            }
            if (entry.SetBonusEffects?.ModifyClimbSpeed != null)
            {
                speedMod = entry.SetBonusEffects.ModifyClimbSpeed(character, speedMod);
            }
            return speedMod;
        }

        public static float ModifyHotbarWeight(Character character, float weight)
        {
            CacheEntry entry = Resolve(character);
            if (entry == null)
            {
                return weight;
            }
            if (entry.OutfitEffects?.ModifyHotbarWeight != null)
            {
                weight = entry.OutfitEffects.ModifyHotbarWeight(character, weight);
            }
            if (entry.HatEffects?.ModifyHotbarWeight != null)
            {
                weight = entry.HatEffects.ModifyHotbarWeight(character, weight);
            }
            if (entry.SetBonusEffects?.ModifyHotbarWeight != null)
            {
                weight = entry.SetBonusEffects.ModifyHotbarWeight(character, weight);
            }
            return weight;
        }

        /** Exposes the resolved outfit, hat and set effects for a character. Used by the HUD. */
        public static bool TryGetActiveEffects(Character character, out int fitIndex, out int hatIndex, out ItemEffects outfitEffects, out ItemEffects hatEffects, out ItemEffects setBonusEffects)
        {
            CacheEntry entry = Resolve(character);
            if (entry == null)
            {
                fitIndex = -1;
                hatIndex = -1;
                outfitEffects = null;
                hatEffects = null;
                setBonusEffects = null;
                return false;
            }
            fitIndex = entry.Fit;
            hatIndex = entry.Hat;
            outfitEffects = entry.OutfitEffects;
            hatEffects = entry.HatEffects;
            setBonusEffects = entry.SetBonusEffects;
            return true;
        }

        public static float ModifySunHeat(Character character, float amount)
        {
            CacheEntry entry = Resolve(character);
            if (entry == null)
            {
                return amount;
            }
            if (entry.OutfitEffects?.ModifySunHeat != null)
            {
                amount = entry.OutfitEffects.ModifySunHeat(character, amount);
            }
            if (entry.HatEffects?.ModifySunHeat != null)
            {
                amount = entry.HatEffects.ModifySunHeat(character, amount);
            }
            if (entry.SetBonusEffects?.ModifySunHeat != null)
            {
                amount = entry.SetBonusEffects.ModifySunHeat(character, amount);
            }
            return amount;
        }

        public static float ModifyItemStatusChange(Character character, Item item, CharacterAfflictions.STATUSTYPE statusType, float delta)
        {
            CacheEntry entry = Resolve(character);
            if (entry == null)
            {
                return delta;
            }
            if (entry.OutfitEffects?.ModifyItemStatusChange != null)
            {
                delta = entry.OutfitEffects.ModifyItemStatusChange(character, item, statusType, delta);
            }
            if (entry.HatEffects?.ModifyItemStatusChange != null)
            {
                delta = entry.HatEffects.ModifyItemStatusChange(character, item, statusType, delta);
            }
            if (entry.SetBonusEffects?.ModifyItemStatusChange != null)
            {
                delta = entry.SetBonusEffects.ModifyItemStatusChange(character, item, statusType, delta);
            }
            return delta;
        }

        public static bool BlockAffliction(Character character, Affliction.AfflictionType type)
        {
            CacheEntry entry = Resolve(character);
            if (entry == null)
            {
                return false;
            }
            if (entry.OutfitEffects?.BlockAffliction != null && entry.OutfitEffects.BlockAffliction(character, type))
            {
                return true;
            }
            if (entry.HatEffects?.BlockAffliction != null && entry.HatEffects.BlockAffliction(character, type))
            {
                return true;
            }
            return entry.SetBonusEffects?.BlockAffliction != null && entry.SetBonusEffects.BlockAffliction(character, type);
        }

        public static float ModifyItemWeight(Character character, Item itemPrefab, float weight)
        {
            CacheEntry entry = Resolve(character);
            if (entry == null)
            {
                return weight;
            }
            if (entry.OutfitEffects?.ModifyItemWeight != null)
            {
                weight = entry.OutfitEffects.ModifyItemWeight(character, itemPrefab, weight);
            }
            if (entry.HatEffects?.ModifyItemWeight != null)
            {
                weight = entry.HatEffects.ModifyItemWeight(character, itemPrefab, weight);
            }
            if (entry.SetBonusEffects?.ModifyItemWeight != null)
            {
                weight = entry.SetBonusEffects.ModifyItemWeight(character, itemPrefab, weight);
            }
            return weight;
        }

        public static float ModifyThornPullStatus(Character character, float amount)
        {
            CacheEntry entry = Resolve(character);
            if (entry == null)
            {
                return amount;
            }
            if (entry.OutfitEffects?.ModifyThornPullStatus != null)
            {
                amount = entry.OutfitEffects.ModifyThornPullStatus(character, amount);
            }
            if (entry.HatEffects?.ModifyThornPullStatus != null)
            {
                amount = entry.HatEffects.ModifyThornPullStatus(character, amount);
            }
            if (entry.SetBonusEffects?.ModifyThornPullStatus != null)
            {
                amount = entry.SetBonusEffects.ModifyThornPullStatus(character, amount);
            }
            return amount;
        }

        public static float ModifyBeetleKnockback(Character character, float force)
        {
            CacheEntry entry = Resolve(character);
            if (entry == null)
            {
                return force;
            }
            if (entry.OutfitEffects?.ModifyBeetleKnockback != null)
            {
                force = entry.OutfitEffects.ModifyBeetleKnockback(character, force);
            }
            if (entry.HatEffects?.ModifyBeetleKnockback != null)
            {
                force = entry.HatEffects.ModifyBeetleKnockback(character, force);
            }
            if (entry.SetBonusEffects?.ModifyBeetleKnockback != null)
            {
                force = entry.SetBonusEffects.ModifyBeetleKnockback(character, force);
            }
            return force;
        }

        public static bool IsHiddenFromZombies(Character character)
        {
            CacheEntry entry = Resolve(character);
            if (entry == null)
            {
                return false;
            }
            if (entry.OutfitEffects?.HiddenFromZombies != null && entry.OutfitEffects.HiddenFromZombies(character))
            {
                return true;
            }
            if (entry.HatEffects?.HiddenFromZombies != null && entry.HatEffects.HiddenFromZombies(character))
            {
                return true;
            }
            return entry.SetBonusEffects?.HiddenFromZombies != null && entry.SetBonusEffects.HiddenFromZombies(character);
        }

        public static float ModifyEnergyDrinkBoost(Character character, float boost)
        {
            CacheEntry entry = Resolve(character);
            if (entry == null)
            {
                return boost;
            }
            if (entry.OutfitEffects?.ModifyEnergyDrinkBoost != null)
            {
                 boost = entry.OutfitEffects.ModifyEnergyDrinkBoost(character, boost);
            }
            if (entry.HatEffects?.ModifyEnergyDrinkBoost != null)
            {
                boost = entry.HatEffects.ModifyEnergyDrinkBoost(character, boost);
            }
            if (entry.SetBonusEffects?.ModifyEnergyDrinkBoost != null)
            {
                boost = entry.SetBonusEffects.ModifyEnergyDrinkBoost(character, boost);
            }
            return boost;
        }

        /** Invokes the feed hooks for the FEEDER's cosmetics. */
        public static void OnFeedTeammate(Character feeder, Character eater, Item item)
        {
            CacheEntry entry = Resolve(feeder);
            if (entry == null)
            {
                return;
            }
            entry.OutfitEffects?.OnFeedTeammate?.Invoke(feeder, eater, item);
            entry.HatEffects?.OnFeedTeammate?.Invoke(feeder, eater, item);
            entry.SetBonusEffects?.OnFeedTeammate?.Invoke(feeder, eater, item);
        }

        public static float ModifyTrapDamage(Character character, float amount)
        {
            CacheEntry entry = Resolve(character);
            if (entry == null)
            {
                return amount;
            }
            if (entry.OutfitEffects?.ModifyTrapDamage != null)
            {
                amount = entry.OutfitEffects.ModifyTrapDamage(character, amount);
            }
            if (entry.HatEffects?.ModifyTrapDamage != null)
            {
                amount = entry.HatEffects.ModifyTrapDamage(character, amount);
            }
            if (entry.SetBonusEffects?.ModifyTrapDamage != null)
            {
                amount = entry.SetBonusEffects.ModifyTrapDamage(character, amount);
            }
            return amount;
        }

        public static float ModifyCursedLuggageDamage(Character character, float amount)
        {
            CacheEntry entry = Resolve(character);
            if (entry == null)
            {
                return amount;
            }
            if (entry.OutfitEffects?.ModifyCursedLuggageDamage != null)
            {
                amount = entry.OutfitEffects.ModifyCursedLuggageDamage(character, amount);
            }
            if (entry.HatEffects?.ModifyCursedLuggageDamage != null)
            {
                amount = entry.HatEffects.ModifyCursedLuggageDamage(character, amount);
            }
            if (entry.SetBonusEffects?.ModifyCursedLuggageDamage != null)
            {
                amount = entry.SetBonusEffects.ModifyCursedLuggageDamage(character, amount);
            }
            return amount;
        }

        public static float ModifyGloomFogDensity(Character character, float amount)
        {
            CacheEntry entry = Resolve(character);
            if (entry == null)
            {
                return amount;
            }
            if (entry.OutfitEffects?.ModifyGloomFogDensity != null)
            {
                amount = entry.OutfitEffects.ModifyGloomFogDensity(character, amount);
            }
            if (entry.HatEffects?.ModifyGloomFogDensity != null)
            {
                amount = entry.HatEffects.ModifyGloomFogDensity(character, amount);
            }
            if (entry.SetBonusEffects?.ModifyGloomFogDensity != null)
            {
                amount = entry.SetBonusEffects.ModifyGloomFogDensity(character, amount);
            }
            return amount;
        }

        public static float ModifyJumpHeight(Character character, float impulse)
        {
            CacheEntry entry = Resolve(character);
            if (entry == null)
            {
                return impulse;
            }
            if (entry.OutfitEffects?.ModifyJumpHeight != null)
            {
                impulse = entry.OutfitEffects.ModifyJumpHeight(character, impulse);
            }
            if (entry.HatEffects?.ModifyJumpHeight != null)
            {
                impulse = entry.HatEffects.ModifyJumpHeight(character, impulse);
            }
            if (entry.SetBonusEffects?.ModifyJumpHeight != null)
            {
                impulse = entry.SetBonusEffects.ModifyJumpHeight(character, impulse);
            }
            return impulse;
        }

        public static float ModifyGravity(Character character, float gravityY)
        {
            CacheEntry entry = Resolve(character);
            if (entry == null)
            {
                return gravityY;
            }
            if (entry.OutfitEffects?.ModifyGravity != null)
            {
                gravityY = entry.OutfitEffects.ModifyGravity(character, gravityY);
            }
            if (entry.HatEffects?.ModifyGravity != null)
            {
                gravityY = entry.HatEffects.ModifyGravity(character, gravityY);
            }
            if (entry.SetBonusEffects?.ModifyGravity != null)
            {
                gravityY = entry.SetBonusEffects.ModifyGravity(character, gravityY);
            }
            return gravityY;
        }

        public static float ModifySlipperiness(Character character, float slippy)
        {
            CacheEntry entry = Resolve(character);
            if (entry == null)
            {
                return slippy;
            }
            if (entry.OutfitEffects?.ModifySlipperiness != null)
            {
                slippy = entry.OutfitEffects.ModifySlipperiness(character, slippy);
            }
            if (entry.HatEffects?.ModifySlipperiness != null)
            {
                slippy = entry.HatEffects.ModifySlipperiness(character, slippy);
            }
            if (entry.SetBonusEffects?.ModifySlipperiness != null)
            {
                slippy = entry.SetBonusEffects.ModifySlipperiness(character, slippy);
            }
            return slippy;
        }

        private static CacheEntry Resolve(Character character)
        {
            Customization customization = Singleton<Customization>.Instance;
            if (customization == null || customization.fits == null || customization.fits.Length == 0)
            {
                return null;
            }
            var service = GameHandler.GetService<PersistentPlayerDataService>();
            var owner = character.photonView != null ? character.photonView.Owner : null;
            if (service == null || owner == null)
            {
                return null;
            }

            PersistentPlayerData data = service.GetPlayerData(owner);
            int fitIndex = CharacterCustomization.GetFitIndex(data);
            int hatIndex = data.customizationData.currentHat;
            // Some outfits force a specific hat. Use what is actually on the head.
            if (customization.fits[fitIndex].overrideHat)
            {
                hatIndex = customization.fits[fitIndex].overrideHatIndex;
            }
            if (hatIndex < 0 || hatIndex >= customization.hats.Length)
            {
                hatIndex = 0;
            }

            int actor = owner.ActorNumber;
            if (!Cache.TryGetValue(actor, out CacheEntry entry) || entry.Fit != fitIndex || entry.Hat != hatIndex)
            {
                string fitName = customization.fits[fitIndex].name;
                string hatName = customization.hats[hatIndex].name;
                entry = new CacheEntry
                {
                    Fit = fitIndex,
                    Hat = hatIndex,
                    OutfitEffects = EffectRegistry.Get(fitName),
                    HatEffects = EffectRegistry.Get(hatName),
                    SetBonusEffects = EffectRegistry.GetSetBonus(fitName, hatName)
                };
                Cache[actor] = entry;
                if (Plugin.LogActiveEffects.Value)
                {
                    LogEffects(character, customization, entry);
                }
            }
            return entry;
        }

        private static void LogEffects(Character character, Customization customization, CacheEntry entry)
        {
            string outfitName = customization.fits[entry.Fit].name;
            string hatName = customization.hats[entry.Hat].name;
            var parts = new List<string>();
            if (entry.OutfitEffects != null)
            {
                parts.Add($"{outfitName}: {entry.OutfitEffects.Description}");
            }
            if (entry.HatEffects != null)
            {
                parts.Add($"{hatName}: {entry.HatEffects.Description}");
            }
            if (entry.SetBonusEffects != null)
            {
                parts.Add($"set bonus: {entry.SetBonusEffects.Description}");
            }
            Plugin.Log.LogInfo(parts.Count > 0
                ? $"{character.characterName} [{outfitName} + {hatName}] -> {string.Join(" | ", parts)}"
                : $"{character.characterName} [{outfitName} + {hatName}] -> no costume effects");
        }
    }

    /**
     * Tracks which damage source is currently inflicting a status. Set by small
     * prefix/finalizer patches on the source methods and consulted by AddStatusPatch,
     * because the game has no central damage function carrying an instigator.
     */
    internal static class DamageContext
    {
        public static bool InTrap;
        public static bool InCursedLuggage;

        /** True while an attack that inflicts damage cross-client with fromRPC (antlion
         * bite, scoutmaster throw) is executing. AddStatusPatch lets those calls through
         * to the hooks; every client computes the same result from synced cosmetics. */
        public static bool InDirectRpcAttack;
    }

    // The antlion bite applies its damage inside a coroutine started by Antlion.Attack,
    // so the flag wraps the iterator's MoveNext rather than Attack itself.
    [HarmonyPatch]
    internal static class AntlionAttackContextPatch
    {
        [HarmonyTargetMethod]
        private static System.Reflection.MethodBase TargetMethod()
        {
            Type iterator = FindIteratorType(typeof(Antlion), "IAttack");
            return iterator != null ? AccessTools.Method(iterator, "MoveNext") : null;
        }

        internal static Type FindIteratorType(Type root, string name)
        {
            foreach (Type nested in root.GetNestedTypes(AccessTools.all))
            {
                if (nested.Name.Contains(name))
                {
                    return nested;
                }
                Type deeper = FindIteratorType(nested, name);
                if (deeper != null)
                {
                    return deeper;
                }
            }
            return null;
        }

        private static void Prefix()
        {
            DamageContext.InDirectRpcAttack = true;
        }

        private static void Finalizer()
        {
            DamageContext.InDirectRpcAttack = false;
        }
    }

    // Character.FeedItem runs on the FEEDER's client at the moment an item is fed to a
    // teammate, before the eater's client applies it. The item still resolves its true
    // holder to the feeder here, which is what the feed hooks key on.
    [HarmonyPatch(typeof(Character), nameof(Character.FeedItem))]
    internal static class FeedItemPatch
    {
        private static void Postfix(Character __instance, Item item)
        {
            try
            {
                Character feeder = item != null ? item.trueHolderCharacter : null;
                if (feeder == null || !feeder.IsLocal || feeder.isBot || feeder == __instance)
                {
                    return;
                }
                EffectResolver.OnFeedTeammate(feeder, __instance, item);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Costume feed effect failed: {e}");
            }
        }
    }

    // The scoutmaster's throw damage lives in the IThrow iterator.
    [HarmonyPatch]
    internal static class ScoutmasterThrowContextPatch
    {
        [HarmonyTargetMethod]
        private static System.Reflection.MethodBase TargetMethod()
        {
            return AccessTools.EnumeratorMoveNext(AccessTools.Method(typeof(Scoutmaster), "IThrow"));
        }

        private static void Prefix()
        {
            DamageContext.InDirectRpcAttack = true;
        }

        private static void Finalizer()
        {
            DamageContext.InDirectRpcAttack = false;
        }
    }

    // Trap contact handlers. Everything they inflict while running counts as trap damage.
    [HarmonyPatch]
    internal static class TrapContextPatch
    {
        [HarmonyTargetMethods]
        private static IEnumerable<System.Reflection.MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(SwingingAxe), "OnCollisionEnter");
            yield return AccessTools.Method(typeof(Peak.SpikeRoller), "OnCollisionEnter");
            yield return AccessTools.Method(typeof(Peak.MovingSawBlade), "OnTriggerStay");
        }

        private static void Prefix()
        {
            DamageContext.InTrap = true;
        }

        private static void Finalizer()
        {
            DamageContext.InTrap = false;
        }
    }

    // Cursed luggage inflicts curse and injury from its open handler.
    [HarmonyPatch(typeof(LuggageCursed), nameof(LuggageCursed.Interact_CastFinished))]
    internal static class CursedLuggageContextPatch
    {
        private static void Prefix()
        {
            DamageContext.InCursedLuggage = true;
        }

        private static void Finalizer()
        {
            DamageContext.InCursedLuggage = false;
        }
    }

    // Every status buildup source funnels through CharacterAfflictions.AddStatus.
    // fromRPC calls are network sync of already-applied values, never rescale those.
    [HarmonyPatch(typeof(CharacterAfflictions), nameof(CharacterAfflictions.AddStatus),
        typeof(CharacterAfflictions.STATUSTYPE), typeof(float), typeof(bool), typeof(bool), typeof(bool))]
    internal static class AddStatusPatch
    {
        private static void Prefix(CharacterAfflictions __instance, CharacterAfflictions.STATUSTYPE statusType, ref float amount, bool fromRPC)
        {
            if (amount <= 0f)
            {
                return;
            }
            // fromRPC is normally a network replay of already-modified values, except for
            // direct cross-client attacks, which are original damage and must be hookable.
            if (fromRPC && !DamageContext.InDirectRpcAttack)
            {
                return;
            }
            try
            {
                Character character = __instance.character;
                if (character == null || character.isBot)
                {
                    return;
                }
                if (DamageContext.InTrap)
                {
                    amount = EffectResolver.ModifyTrapDamage(character, amount);
                }
                if (DamageContext.InCursedLuggage)
                {
                    amount = EffectResolver.ModifyCursedLuggageDamage(character, amount);
                }
                amount = EffectResolver.ModifyStatus(character, statusType, amount);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Costume status effect failed: {e}");
            }
        }
    }

    // Status recovery (item healing and natural decay) funnels through SubtractStatus.
    // Scaling the amount up makes matching costumes heal faster. Never rescale fromRPC calls.
    [HarmonyPatch(typeof(CharacterAfflictions), nameof(CharacterAfflictions.SubtractStatus))]
    internal static class SubtractStatusPatch
    {
        private static void Prefix(CharacterAfflictions __instance, CharacterAfflictions.STATUSTYPE statusType, ref float amount, bool fromRPC)
        {
            if (fromRPC || amount <= 0f)
            {
                return;
            }
            try
            {
                Character character = __instance.character;
                if (character == null || character.isBot)
                {
                    return;
                }
                amount = EffectResolver.ModifyStatusHeal(character, statusType, amount);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Costume heal effect failed: {e}");
            }
        }
    }

    // Thorns and Arrow never go through AddStatus. The game recomputes and sets them in
    // UpdateWeight, so apply ModifyStatus here for those types only. Weight is handled by
    // HotbarWeightPatch to avoid double-scaling.
    [HarmonyPatch(typeof(CharacterAfflictions), nameof(CharacterAfflictions.SetStatus))]
    internal static class SetStatusPatch
    {
        private static void Prefix(CharacterAfflictions __instance, CharacterAfflictions.STATUSTYPE statusType, ref float amount)
        {
            if (statusType != CharacterAfflictions.STATUSTYPE.Thorns
                && statusType != CharacterAfflictions.STATUSTYPE.Arrow)
            {
                return;
            }
            if (amount <= 0f)
            {
                return;
            }
            try
            {
                Character character = __instance.character;
                if (character == null || character.isBot)
                {
                    return;
                }
                amount = EffectResolver.ModifyStatus(character, statusType, amount);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Costume derived-status effect failed: {e}");
            }
        }
    }

    // Sun heat enters through AddSunHeat before joining the Hot pipeline. A prefix here
    // affects sun heat only, not campfire or lava heat.
    [HarmonyPatch(typeof(CharacterAfflictions), nameof(CharacterAfflictions.AddSunHeat))]
    internal static class SunHeatPatch
    {
        private static void Prefix(CharacterAfflictions __instance, ref float amount)
        {
            if (amount <= 0f)
            {
                return;
            }
            try
            {
                Character character = __instance.character;
                if (character == null || character.isBot)
                {
                    return;
                }
                amount = EffectResolver.ModifySunHeat(character, amount);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Costume sun heat effect failed: {e}");
            }
        }
    }

    // Status changes from consuming items run through the Action_* components, which know
    // both the item and the consumer. Scale the configured amount for the duration of
    // RunAction, then restore it.
    [HarmonyPatch(typeof(Action_ModifyStatus), nameof(Action_ModifyStatus.RunAction))]
    internal static class ActionModifyStatusPatch
    {
        private static readonly AccessTools.FieldRef<ItemActionBase, Item> ItemField =
            AccessTools.FieldRefAccess<ItemActionBase, Item>("item");

        private static void Prefix(Action_ModifyStatus __instance, ref float __state)
        {
            __state = __instance.changeAmount;
            try
            {
                Item item = ItemField(__instance);
                Character character = item != null ? item.holderCharacter : null;
                if (character == null || character.isBot)
                {
                    return;
                }
                __instance.changeAmount = EffectResolver.ModifyItemStatusChange(character, item, __instance.statusType, __state);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Costume item status effect failed: {e}");
            }
        }

        private static void Finalizer(Action_ModifyStatus __instance, float __state)
        {
            __instance.changeAmount = __state;
        }
    }

    [HarmonyPatch(typeof(Action_RestoreHunger), nameof(Action_RestoreHunger.RunAction))]
    internal static class ActionRestoreHungerPatch
    {
        private static readonly AccessTools.FieldRef<ItemActionBase, Item> ItemField =
            AccessTools.FieldRefAccess<ItemActionBase, Item>("item");

        private static void Prefix(Action_RestoreHunger __instance, ref float __state)
        {
            __state = __instance.restorationAmount;
            try
            {
                Item item = ItemField(__instance);
                Character character = item != null ? item.holderCharacter : null;
                if (character == null || character.isBot)
                {
                    return;
                }
                // Present it to the hook as a signed delta, negative means heal.
                float newDelta = EffectResolver.ModifyItemStatusChange(character, item, CharacterAfflictions.STATUSTYPE.Hunger, -__state);
                __instance.restorationAmount = -newDelta;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Costume hunger restore effect failed: {e}");
            }
        }

        private static void Finalizer(Action_RestoreHunger __instance, float __state)
        {
            __instance.restorationAmount = __state;
        }
    }

    // Affliction immunity. A bool prefix on AddAffliction skips the application when a
    // worn item blocks the affliction type.
    [HarmonyPatch(typeof(CharacterAfflictions), nameof(CharacterAfflictions.AddAffliction))]
    internal static class AfflictionBlockPatch
    {
        private static bool Prefix(CharacterAfflictions __instance, Affliction affliction)
        {
            try
            {
                Character character = __instance.character;
                if (affliction == null || character == null || character.isBot)
                {
                    return true;
                }
                if (EffectResolver.BlockAffliction(character, affliction.GetAfflictionType()))
                {
                    return false;
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Costume affliction block failed: {e}");
            }
            return true;
        }
    }

    // The beetle's bonk (Beetle.InflictAttack) ragdolls the player and launches them with
    // its public bonkForce/bonkForceUp fields. Scale both fields for the duration of the
    // call, then restore them. The method already runs only on the hit player's client.
    [HarmonyPatch(typeof(Beetle), "InflictAttack")]
    internal static class BeetleBonkPatch
    {
        internal struct CallState
        {
            public float Force;
            public float ForceUp;
        }

        private static void Prefix(Beetle __instance, Character character, ref CallState __state)
        {
            __state.Force = __instance.bonkForce;
            __state.ForceUp = __instance.bonkForceUp;
            try
            {
                if (character == null || character.isBot)
                {
                    return;
                }
                __instance.bonkForce = EffectResolver.ModifyBeetleKnockback(character, __state.Force);
                __instance.bonkForceUp = EffectResolver.ModifyBeetleKnockback(character, __state.ForceUp);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Costume beetle knockback effect failed: {e}");
            }
        }

        private static void Finalizer(Beetle __instance, CallState __state)
        {
            __instance.bonkForce = __state.Force;
            __instance.bonkForceUp = __state.ForceUp;
        }
    }

    // Pulling a thorn or arrow out adds a configured status in ThornOnMe.OnPulledOut.
    // Scale the amount for the duration of the call, then restore it.
    [HarmonyPatch(typeof(ThornOnMe), nameof(ThornOnMe.OnPulledOut))]
    internal static class ThornPullPatch
    {
        private static void Prefix(ThornOnMe __instance, ref float __state)
        {
            __state = __instance.statusToAddOnRemoveAmt;
            try
            {
                Character character = __instance.character;
                if (character == null || character.isBot || !__instance.addStatusOnRemove)
                {
                    return;
                }
                __instance.statusToAddOnRemoveAmt = EffectResolver.ModifyThornPullStatus(character, __state);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Costume thorn pull effect failed: {e}");
            }
        }

        private static void Finalizer(ThornOnMe __instance, float __state)
        {
            __instance.statusToAddOnRemoveAmt = __state;
        }
    }

    // UpdateWeight sums every source into one number and sets the Weight status. This
    // postfix recomputes the held-items portion (hotbar slots plus temp slot), applies
    // ModifyItemWeight and ModifyHotbarWeight to that slice, then ModifyStatus(Weight)
    // to the total, and writes it back.
    [HarmonyPatch]
    internal static class HotbarWeightPatch
    {
        [HarmonyTargetMethod]
        private static System.Reflection.MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(CharacterAfflictions), "UpdateWeight");
        }

        [HarmonyPostfix]
        private static void Postfix(CharacterAfflictions __instance)
        {
            try
            {
                Character character = __instance.character;
                if (character == null || character.isBot || character.player == null)
                {
                    return;
                }
                float held = 0f;
                float heldAdjusted = 0f;
                void AddHeldItem(Item prefab)
                {
                    if (prefab == null)
                    {
                        return;
                    }
                    float w = 0.025f * prefab.CarryWeight;
                    held += w;
                    heldAdjusted += EffectResolver.ModifyItemWeight(character, prefab, w);
                }
                ItemSlot[] itemSlots = character.player.itemSlots;
                for (int i = 0; i < itemSlots.Length; i++)
                {
                    AddHeldItem(itemSlots[i].prefab);
                }
                ItemSlot tempSlot = character.player.tempFullSlot;
                if (tempSlot != null && !tempSlot.IsEmpty())
                {
                    AddHeldItem(tempSlot.prefab);
                }

                float current = __instance.GetCurrentStatus(CharacterAfflictions.STATUSTYPE.Weight);
                float rest = Mathf.Max(current - held, 0f);
                float newHeld = EffectResolver.ModifyHotbarWeight(character, heldAdjusted);
                float newTotal = EffectResolver.ModifyStatus(character, CharacterAfflictions.STATUSTYPE.Weight, rest + newHeld);
                if (Mathf.Abs(newTotal - current) > 0.0001f)
                {
                    __instance.SetStatus(CharacterAfflictions.STATUSTYPE.Weight, newTotal);
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Costume hotbar weight effect failed: {e}");
            }
        }
    }

    // Character.UseStamina is the single funnel for stamina spending, the same place the
    // game applies Ascents.climbStaminaMultiplier.
    [HarmonyPatch]
    internal static class UseStaminaPatch
    {
        [HarmonyTargetMethod]
        private static System.Reflection.MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(Character), "UseStamina");
        }

        [HarmonyPrefix]
        private static void Prefix(Character __instance, ref float usage)
        {
            if (usage <= 0f)
            {
                return;
            }
            try
            {
                if (__instance == null || __instance.isBot)
                {
                    return;
                }
                usage = EffectResolver.ModifyStaminaDrain(__instance, usage);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Costume stamina drain effect failed: {e}");
            }
        }
    }

    // The energy drink (Affliction_FasterBoi) ADDS its boosts to movementModifier and the
    // climbSpeedMod fields on apply and subtracts them on removal, so apply and remove
    // must stay symmetric. These postfixes add only the hook's extra delta, and record it
    // per affliction instance so removal subtracts exactly what was added, even if the
    // wearer changes cosmetics mid-buff.
    [HarmonyPatch(typeof(Affliction_FasterBoi))]
    internal static class EnergyDrinkPatch
    {
        private struct Extras
        {
            public float Move;
            public float Climb;
        }

        private static readonly Dictionary<Affliction, Extras> Applied = new Dictionary<Affliction, Extras>();

        [HarmonyPostfix]
        [HarmonyPatch("OnApplied")]
        private static void OnAppliedPostfix(Affliction_FasterBoi __instance)
        {
            try
            {
                Character character = __instance.character;
                if (character == null || character.isBot)
                {
                    return;
                }
                var extras = new Extras
                {
                    Move = EffectResolver.ModifyEnergyDrinkBoost(character, __instance.moveSpeedMod) - __instance.moveSpeedMod,
                    Climb = EffectResolver.ModifyEnergyDrinkBoost(character, __instance.climbSpeedMod) - __instance.climbSpeedMod
                };
                if (extras.Move == 0f && extras.Climb == 0f)
                {
                    return;
                }
                character.refs.movement.movementModifier += extras.Move;
                character.refs.climbing.climbSpeedMod += extras.Climb;
                character.refs.ropeHandling.climbSpeedMod += extras.Climb;
                character.refs.vineClimbing.climbSpeedMod += extras.Climb;
                Applied[__instance] = extras;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Costume energy drink effect failed: {e}");
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch("OnRemoved")]
        private static void OnRemovedPostfix(Affliction_FasterBoi __instance)
        {
            try
            {
                if (!Applied.TryGetValue(__instance, out Extras extras))
                {
                    return;
                }
                Applied.Remove(__instance);
                Character character = __instance.character;
                if (character == null)
                {
                    return;
                }
                character.refs.movement.movementModifier -= extras.Move;
                character.refs.climbing.climbSpeedMod -= extras.Climb;
                character.refs.ropeHandling.climbSpeedMod -= extras.Climb;
                character.refs.vineClimbing.climbSpeedMod -= extras.Climb;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Costume energy drink removal failed: {e}");
            }
        }
    }

    // The jump force reads the public jumpImpulse field inside a coroutine that runs a
    // beat after JumpRpc, so a scale-and-restore around the call would restore too early.
    // Instead, capture the field's baseline once per instance and re-derive it from
    // baseline times the hook on every jump. Runs on every client for the jumping
    // character; cosmetics are synced, so all clients compute the same impulse.
    [HarmonyPatch(typeof(CharacterMovement), nameof(CharacterMovement.JumpRpc))]
    internal static class JumpHeightPatch
    {
        private static readonly Dictionary<CharacterMovement, float> Baselines = new Dictionary<CharacterMovement, float>();

        private static readonly AccessTools.FieldRef<CharacterMovement, Character> CharacterField =
            AccessTools.FieldRefAccess<CharacterMovement, Character>("character");

        private static void Prefix(CharacterMovement __instance)
        {
            try
            {
                if (!Baselines.TryGetValue(__instance, out float baseline))
                {
                    baseline = __instance.jumpImpulse;
                    Baselines[__instance] = baseline;
                }
                Character character = CharacterField(__instance);
                if (character == null || character.isBot)
                {
                    __instance.jumpImpulse = baseline;
                    return;
                }
                __instance.jumpImpulse = EffectResolver.ModifyJumpHeight(character, baseline);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Costume jump height effect failed: {e}");
            }
        }
    }

    // Zombie targeting funnels through MushroomZombie.TargetIsValid, used both when
    // acquiring a target and when validating the current one, so hidden characters are
    // never picked and get dropped mid-chase. Runs on the zombie's controlling client;
    // the target's cosmetics are network-synced, so the check works for remote players.
    [HarmonyPatch(typeof(MushroomZombie), "TargetIsValid")]
    internal static class ZombieTargetPatch
    {
        private static void Postfix(Character target, ref bool __result)
        {
            if (!__result)
            {
                return;
            }
            try
            {
                if (target == null || target.isBot)
                {
                    return;
                }
                if (EffectResolver.IsHiddenFromZombies(target))
                {
                    __result = false;
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Costume zombie aggro effect failed: {e}");
            }
        }
    }

    // CharacterMovement.GetGravityForce computes the airborne gravity accel every
    // FixedUpdate (zero while grounded), applied to the ragdoll and held items. Scaling
    // its result changes fall speed and jump arcs. The balloon and low-gravity
    // multipliers apply afterwards, so this composes with them.
    [HarmonyPatch]
    internal static class GravityPatch
    {
        private static readonly AccessTools.FieldRef<CharacterMovement, Character> CharacterField =
            AccessTools.FieldRefAccess<CharacterMovement, Character>("character");

        [HarmonyTargetMethod]
        private static System.Reflection.MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(CharacterMovement), "GetGravityForce");
        }

        [HarmonyPostfix]
        private static void Postfix(CharacterMovement __instance, ref Vector3 __result)
        {
            if (__result == Vector3.zero)
            {
                return;
            }
            try
            {
                Character character = CharacterField(__instance);
                if (character == null || character.isBot)
                {
                    return;
                }
                __result.y = EffectResolver.ModifyGravity(character, __result.y);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Costume gravity effect failed: {e}");
            }
        }
    }

    // CharacterMovement.GetMovementForce is the single scalar driving ground and air
    // movement, recomputed every FixedUpdate. A postfix is stateless and cannot drift,
    // unlike mutating the movementModifier field.
    [HarmonyPatch]
    internal static class MoveSpeedPatch
    {
        private static readonly AccessTools.FieldRef<CharacterMovement, Character> CharacterField =
            AccessTools.FieldRefAccess<CharacterMovement, Character>("character");

        [HarmonyTargetMethod]
        private static System.Reflection.MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(CharacterMovement), "GetMovementForce");
        }

        [HarmonyPostfix]
        private static void Postfix(CharacterMovement __instance, ref float __result)
        {
            if (__result <= 0f)
            {
                return;
            }
            try
            {
                Character character = CharacterField(__instance);
                if (character == null || character.isBot)
                {
                    return;
                }
                __result = EffectResolver.ModifyMoveSpeed(character, __result);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Costume move speed effect failed: {e}");
            }
        }
    }

    // Each climbing class reads its climbSpeedMod field in one hot method. Multiply the
    // field for the duration of that call and restore it in a finalizer, so it composes
    // with the game's own additive changes. Wall climbing also reads data.slippy inside
    // GetRequestedPostition, so this patch scales both.
    [HarmonyPatch]
    internal static class ClimbSpeedPatch_Wall
    {
        internal struct CallState
        {
            public float SpeedMod;
            public float Slippy;
            public Character Character;
        }

        private static readonly AccessTools.FieldRef<CharacterClimbing, Character> CharacterField =
            AccessTools.FieldRefAccess<CharacterClimbing, Character>("character");

        [HarmonyTargetMethod]
        private static System.Reflection.MethodBase TargetMethod()
        {
            // Sic, the game misspells "GetRequestedPostition".
            return AccessTools.Method(typeof(CharacterClimbing), "GetRequestedPostition");
        }

        [HarmonyPrefix]
        private static void Prefix(CharacterClimbing __instance, ref CallState __state)
        {
            __state.SpeedMod = __instance.climbSpeedMod;
            __state.Character = null;
            try
            {
                Character character = CharacterField(__instance);
                if (character == null || character.isBot)
                {
                    return;
                }
                __state.Character = character;
                __state.Slippy = character.data.slippy;
                __instance.climbSpeedMod = EffectResolver.ModifyClimbSpeed(character, __state.SpeedMod);
                if (__state.Slippy > 0f)
                {
                    character.data.slippy = EffectResolver.ModifySlipperiness(character, __state.Slippy);
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Costume climb speed effect failed: {e}");
            }
        }

        [HarmonyFinalizer]
        private static void Finalizer(CharacterClimbing __instance, CallState __state)
        {
            __instance.climbSpeedMod = __state.SpeedMod;
            if (__state.Character != null)
            {
                __state.Character.data.slippy = __state.Slippy;
            }
        }
    }

    [HarmonyPatch]
    internal static class ClimbSpeedPatch_Rope
    {
        private static readonly AccessTools.FieldRef<CharacterRopeHandling, Character> CharacterField =
            AccessTools.FieldRefAccess<CharacterRopeHandling, Character>("character");

        [HarmonyTargetMethod]
        private static System.Reflection.MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(CharacterRopeHandling), "Update");
        }

        [HarmonyPrefix]
        private static void Prefix(CharacterRopeHandling __instance, ref float __state)
        {
            __state = __instance.climbSpeedMod;
            try
            {
                Character character = CharacterField(__instance);
                if (character == null || character.isBot)
                {
                    return;
                }
                __instance.climbSpeedMod = EffectResolver.ModifyClimbSpeed(character, __state);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Costume climb speed effect failed: {e}");
            }
        }

        [HarmonyFinalizer]
        private static void Finalizer(CharacterRopeHandling __instance, float __state)
        {
            __instance.climbSpeedMod = __state;
        }
    }

    [HarmonyPatch]
    internal static class ClimbSpeedPatch_Vine
    {
        private static readonly AccessTools.FieldRef<CharacterVineClimbing, Character> CharacterField =
            AccessTools.FieldRefAccess<CharacterVineClimbing, Character>("character");

        [HarmonyTargetMethod]
        private static System.Reflection.MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(CharacterVineClimbing), "FixedUpdate");
        }

        [HarmonyPrefix]
        private static void Prefix(CharacterVineClimbing __instance, ref float __state)
        {
            __state = __instance.climbSpeedMod;
            try
            {
                Character character = CharacterField(__instance);
                if (character == null || character.isBot)
                {
                    return;
                }
                __instance.climbSpeedMod = EffectResolver.ModifyClimbSpeed(character, __state);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Costume climb speed effect failed: {e}");
            }
        }

        [HarmonyFinalizer]
        private static void Finalizer(CharacterVineClimbing __instance, float __state)
        {
            __instance.climbSpeedMod = __state;
        }
    }

    // Natural resting regen is a single AddStamina call at the end of
    // Character.UpdateVariablesFixed, gated by CanRegenStamina. Piggyback on the same gate
    // and add the difference. Item and morale stamina gains are untouched.
    [HarmonyPatch]
    internal static class RegenStaminaPatch
    {
        private const float BaseRegenPerSecond = 0.2f;

        private static readonly Func<Character, bool> CanRegenStamina =
            AccessTools.MethodDelegate<Func<Character, bool>>(
                AccessTools.Method(typeof(Character), "CanRegenStamina"));

        [HarmonyTargetMethod]
        private static System.Reflection.MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(Character), "UpdateVariablesFixed");
        }

        [HarmonyPostfix]
        private static void Postfix(Character __instance)
        {
            try
            {
                if (__instance == null || __instance.isBot || !CanRegenStamina(__instance))
                {
                    return;
                }
                float baseRegen = Time.fixedDeltaTime * BaseRegenPerSecond;
                float modified = EffectResolver.ModifyStaminaRegen(__instance, baseRegen);
                if (modified != baseRegen)
                {
                    // AddStamina self-guards on ownership and clamps, so a negative delta is safe.
                    __instance.AddStamina(modified - baseRegen);
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Costume stamina regen effect failed: {e}");
            }
        }
    }
}
