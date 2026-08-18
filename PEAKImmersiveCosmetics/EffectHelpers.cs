//PEAK Immersive Cosmetics
//Distributed under the MIT License @ https://github.com/NonRadical/PEAKImmersiveCosmetics

using System.Collections.Generic;
using UnityEngine;
using Zorro.Core;
using STATUS = CharacterAfflictions.STATUSTYPE;

namespace PEAKImmersiveCosmetics
{
    /** Gameplay condition helpers shared by effect lambdas in the registry. */
    internal static class EffectHelpers
    {
        /** True if this item has been cooked at least once. */
        internal static bool IsCooked(Item item)
        {
            return item != null && item.data != null
                && item.data.TryGetDataEntry<IntItemData>(DataEntryKey.CookedAmount, out IntItemData cooked)
                && cooked.Value >= 1;
        }

        /** Fraction (0..1) of the other non-bot party members who are dead. */
        internal static float DeadPartyFraction(Character self)
        {
            int total = 0;
            int dead = 0;
            List<Character> all = Character.AllCharacters;
            for (int i = 0; i < all.Count; i++)
            {
                Character c = all[i];
                if (c == null || c.isBot || c == self)
                {
                    continue;
                }
                total++;
                if (c.data.dead)
                {
                    dead++;
                }
            }
            return total > 0 ? (float)dead / total : 0f;
        }

        /** True if a storm (rain or snow) is active and covering this character. */
        internal static bool InActiveStorm(Character character)
        {
            WindChillZone storm = WindChillZone.instance;
            return storm != null && storm.windActive && storm.windZoneBounds.Contains(character.Center);
        }

        /** True if the current biome is the Gloom (Swamp). */
        internal static bool InGloom()
        {
            MapHandler map = Singleton<MapHandler>.Instance;
            return map != null && map.GetCurrentBiome() == Biome.BiomeType.Swamp;
        }

        /** True in daytime sunshine: day, no orb fog, no storm, and not in a dark or indoor biome. */
        internal static bool InSunshine(Character character)
        {
            if (DayNightManager.instance == null || DayNightManager.instance.isDay < 0.5f)
            {
                return false;
            }
            if (character.data.isInFog || InActiveStorm(character))
            {
                return false;
            }
            MapHandler map = Singleton<MapHandler>.Instance;
            if (map != null)
            {
                Biome.BiomeType biome = map.GetCurrentBiome();
                if (biome == Biome.BiomeType.Alpine || biome == Biome.BiomeType.Swamp
                    || biome == Biome.BiomeType.Volcano || biome == Biome.BiomeType.Temple)
                {
                    return false;
                }
            }
            return true;
        }

        /** Applies a fraction of the item's configured status healing to the character,
         * mirroring each healing action the item would run when consumed or used. */
        internal static void ApplyFractionOfItemHealing(Character character, Item item, float fraction)
        {
            if (character == null || item == null)
            {
                return;
            }
            foreach (Action_ModifyStatus action in item.GetComponents<Action_ModifyStatus>())
            {
                if (action.changeAmount < 0f
                    && (action.OnConsumed || action.OnCastFinished)
                    && (!action.ifSkeleton || character.data.isSkeleton))
                {
                    character.refs.afflictions.SubtractStatus(action.statusType, -action.changeAmount * fraction);
                }
            }
            foreach (Action_RestoreHunger action in item.GetComponents<Action_RestoreHunger>())
            {
                if (action.OnConsumed || action.OnCastFinished)
                {
                    character.refs.afflictions.SubtractStatus(STATUS.Hunger, action.restorationAmount * fraction);
                }
            }
        }

        private static readonly STATUS[] JesterOptions =
        {
            STATUS.Injury, STATUS.Hunger, STATUS.Cold, STATUS.Poison,
            STATUS.Curse, STATUS.Drowsy, STATUS.Hot, STATUS.Spores
        };

        /** The jester's current rotating buff. Changes every 60 seconds. */
        internal static STATUS CurrentJesterBuff()
        {
            int bucket = (int)(Time.time / 60f);
            uint hash = (uint)bucket * 2654435761u;
            return JesterOptions[(int)(hash % (uint)JesterOptions.Length)];
        }
    }
}
