//PEAK Immersive Cosmetics
//Distributed under the MIT License @ https://github.com/NonRadical/PEAKImmersiveCosmetics

using System;
using System.Collections.Generic;
using Peak.Afflictions;
using STATUS = CharacterAfflictions.STATUSTYPE;

namespace PEAKImmersiveCosmetics
{
    /**
     * Per-item effect definitions, keyed by the cosmetic's exact asset name.
     * Unlisted items have no effect. Effects follow effectlist.txt, expressed in tiers:
     * very slight = 1, slight = 2, unqualified = 3, high = 4. TierScale maps tiers to percent.
     */
    internal static class EffectRegistry
    {
        private static ItemEffects NewItemEffect() => new ItemEffects();

        // Complex definitions with conditional or source-specific lambdas.

        /** Sailor hat: climb speed up, scaling with the fraction of the party that is dead. */
        private static ItemEffects SailorHat()
        {
            return new ItemEffects
            {
                ModifyClimbSpeed = (character, speedMod) =>
                    speedMod * (1.1f + 0.25f * EffectHelpers.DeadPartyFraction(character)),
                Description = "ClimbSpeed +10% (up to +35% as the party dies)"
            }.Mark(EffectKey.SurvivorClimb);
        }

        /** Aviator hat: less cold while an active storm is covering you. */
        private static ItemEffects AviatorHat()
        {
            return new ItemEffects
            {
                ModifyStatus = (character, t, amount) =>
                    t == STATUS.Cold && EffectHelpers.InActiveStorm(character) ? amount * 0.8f : amount,
                Description = "Cold from snowstorms -20%"
            }.Mark(EffectKey.StormCold);
        }

        /** Midsummer hat: uncooked food restores hunger better. */
        private static ItemEffects MidsummerHat()
        {
            return new ItemEffects
            {
                ModifyItemStatusChange = (character, item, t, delta) =>
                    t == STATUS.Hunger && delta < 0f && !EffectHelpers.IsCooked(item) ? delta * 1.2f : delta,
                Description = "Uncooked food +20% hunger restored"
            }.Mark(EffectKey.UncookedFood);
        }

        /** Mushroom hat: immune to shroomberry numbness and item-applied spores. */
        private static ItemEffects MushroomHat()
        {
            return new ItemEffects
            {
                BlockAffliction = (character, type) => type == Affliction.AfflictionType.Numb,
                ModifyItemStatusChange = (character, item, t, delta) =>
                    t == STATUS.Spores && delta > 0f ? 0f : delta,
                Description = "Immune to shroomberry numbness and spores"
            }.Mark(EffectKey.ShroomImmunity);
        }

        /** Ninja headband: stamina regen up while hanging on a piton or pickaxe. */
        private static ItemEffects NinjaHeadband()
        {
            return new ItemEffects
            {
                ModifyStaminaRegen = (character, regen) =>
                    character.data.currentClimbHandle != null ? regen * 1.5f : regen,
                Description = "StaminaRegen +35% while on a piton/pickaxe"
            }.Mark(EffectKey.PitonRegen);
        }

        /** Medic hat: feeding a teammate heals you for a quarter of the item's base healing. */
        private static ItemEffects MedicHat()
        {
            return new ItemEffects
            {
                OnFeedTeammate = (feeder, eater, item) =>
                    EffectHelpers.ApplyFractionOfItemHealing(feeder, item, 0.25f),
                Description = "Healing others heals you 25% of the item's healing"
            }.Mark(EffectKey.MedicFeed);
        }

        /** Chef hat: cooked food restores hunger better. */
        private static ItemEffects ChefHat()
        {
            return new ItemEffects
            {
                ModifyItemStatusChange = (character, item, t, delta) =>
                    t == STATUS.Hunger && delta < 0f && EffectHelpers.IsCooked(item) ? delta * 1.2f : delta,
                Description = "Cooked food +20% hunger restored"
            }.Mark(EffectKey.CookedFood);
        }

        /** Bing bong beanie: golden idol relics weigh less in your hands. */
        private static ItemEffects BingBongBeanie()
        {
            return new ItemEffects
            {
                ModifyItemWeight = (character, itemPrefab, weight) =>
                    itemPrefab.itemTags.HasFlag(Item.ItemTags.GoldenIdol) ? weight * 0.65f : weight,
                Description = "Idol weight -35%"
            }.Mark(EffectKey.IdolWeight);
        }

        /** Sprout hat: stamina regen up while standing in daytime sunshine. */
        private static ItemEffects SproutHat()
        {
            return new ItemEffects
            {
                ModifyStaminaRegen = (character, regen) =>
                    EffectHelpers.InSunshine(character) ? regen * 1.2f : regen,
                Description = "StaminaRegen +20% in sunshine"
            }.Mark(EffectKey.SunRegen);
        }

        /** Arrow hat: arrow resistance, and pulling arrows or thorns out hurts less. */
        private static ItemEffects ArrowHat()
        {
            ItemEffects effect = NewItemEffect().Status(STATUS.Arrow, 2);
            effect.ModifyThornPullStatus = (character, amount) => amount * 0.6f;
            effect.AppendDescription("ThornPullDamage -40%");
            return effect.Mark(EffectKey.ThornPull);
        }

        /** Fairy fit: reduced stamina drain while airborne. */
        private static ItemEffects FairyFit()
        {
            return NewItemEffect().StaminaDrainInAir(2);
        }

        /** Jester fit: a rotating random resistance, changing every 20 seconds. */
        private static ItemEffects JesterFit()
        {
            return new ItemEffects
            {
                ModifyStatus = (character, t, amount) =>
                    t == EffectHelpers.CurrentJesterBuff() ? amount * 0.65f : amount,
                Description = "Random resistance +35%, changes every 20s"
            }.Mark(EffectKey.RandomResist);
        }

        /** Gothic fit: stamina regen up while in the Gloom. */
        private static ItemEffects GothicFit()
        {
            return new ItemEffects
            {
                ModifyStaminaRegen = (character, regen) =>
                    EffectHelpers.InGloom() ? regen * 1.35f : regen,
                Description = "StaminaRegen +35% while in the Gloom"
            }.Mark(EffectKey.GloomRegen);
        }
        
        /** Goat hat: beetle bonks knock you around half as hard. */
        private static ItemEffects GoatHorn()
        {
            return new ItemEffects
            {
                ModifyBeetleKnockback = (character, force) => force * 0.25f,
                Description = "Beetle knockback -75%"
            }.Mark(EffectKey.BeetleKnockback);
        }
        
        /** Drinks hat: increases effect of energy drink. */
        private static ItemEffects DrinksHat()
        {
            return new ItemEffects
            {
                ModifyEnergyDrinkBoost = (character, boost) => boost * 1.30f,
                Description = "Energy drink climbing speed +30%"
            }.Mark(EffectKey.EnergyDrinkBoost);
        }
        
        /** Pumpkin: Makes it so that zombies don't target you. */
        private static ItemEffects Pumpkin()
        {
            return new ItemEffects
            {
                HiddenFromZombies = (character) => true,
                Description = "Zombies do not target you."
            }.Mark(EffectKey.ZombieHidden);
        }

        public static readonly Dictionary<string, ItemEffects> Items =
            new Dictionary<string, ItemEffects>(StringComparer.OrdinalIgnoreCase)
            {
                // Outfits.
                ["Fit_Seagull_Shorts"] = NewItemEffect().StaminaRegen(1),
                ["Fit_Seagull_Skirt"] = NewItemEffect().StaminaRegen(1),
                ["Fit_Turtle_Shorts"] = NewItemEffect().Status(STATUS.Injury, 1),
                ["Fit_Turtle_Skirt"] = NewItemEffect().Status(STATUS.Injury, 1),
                ["Fit_Sailor_Shorts"] = NewItemEffect().HealAll(3),
                ["Fit_Sailor_Skirt"] = NewItemEffect().HealAll(3),
                ["Fit_Castaway_Shorts"] = NewItemEffect().Status(STATUS.Hunger, 2),
                ["Fit_Castaway_Skirt"] = NewItemEffect().Status(STATUS.Hunger, 2),
                ["Fit_Tropical_Shorts"] = NewItemEffect().Status(STATUS.Hot, 3).Status(STATUS.Cold, -2),
                ["Fit_Tropical_Skirt"] = NewItemEffect().Status(STATUS.Hot, 3).Status(STATUS.Cold, -2),
                ["Fit_Cookie_Shorts"] = NewItemEffect().Status(STATUS.Petrify, 2),
                ["Fit_Cookie_Skirt"] = NewItemEffect().Status(STATUS.Petrify, 2),
                ["Fit_Balloon_Shorts"] = NewItemEffect().Status(STATUS.Weight, 1),
                ["Fit_Balloon_Skirt"] = NewItemEffect().Status(STATUS.Weight, 1),
                ["Fit_Scoutmaster_Shorts"] = NewItemEffect().StaminaRegen(2).Status(STATUS.Injury, -1),
                ["Fit_Scoutmaster_Skirt"] = NewItemEffect().StaminaRegen(2).Status(STATUS.Injury, -1),
                ["Fit_Cowboy"] = NewItemEffect().Status(STATUS.Thorns, 3).Status(STATUS.Hot, 1),
                ["Fit_Aviator"] = NewItemEffect().Status(STATUS.Cold, 3).Status(STATUS.Injury, 1),
                ["Fit_Astronaut"] = NewItemEffect().Status(STATUS.Cold, 2).Status(STATUS.Hot, 2).Status(STATUS.Poison, 3).StaminaRegen(-2).StaminaDrain(-1),
                ["Fit_Bundled_Blue"] = NewItemEffect().Status(STATUS.Cold, 3).Status(STATUS.Hot, -2),
                ["Fit_Bundled_Pink"] = NewItemEffect().Status(STATUS.Cold, 3).Status(STATUS.Hot, -2),
                ["Fit_Gnome"] = NewItemEffect().Status(STATUS.Spores, 3).Status(STATUS.Poison, 2),
                ["Fit_PlagueDoctor"] = NewItemEffect().Status(STATUS.Spores, 2).Status(STATUS.Poison, 2).Status(STATUS.Curse, 2).Status(STATUS.Petrify, 2),
                ["Fit_Mummy"] = NewItemEffect().Status(STATUS.Web, 4).Status(STATUS.Spores, 2).Status(STATUS.Poison, 2),
                ["Fit_Climber"] = NewItemEffect().Status(STATUS.Weight, 2).StaminaRegen(-1),
                ["Fit_SkeletonOnesie"] = NewItemEffect().Status(STATUS.Injury, -4).Status(STATUS.Hunger, 3)
                    .Status(STATUS.Poison, 2).Status(STATUS.Spores, 2)
                    .Status(STATUS.Hot, 2).Status(STATUS.Cold, 2),
                ["Fit_Fairy"] = FairyFit(),
                ["Fit_SleepyGuy"] = NewItemEffect().Heal(STATUS.Drowsy, 3),
                ["Fit_Knight"] = NewItemEffect().Status(STATUS.Injury, 3).StaminaDrain(-3),
                ["Fit_Jester"] = JesterFit(),
                ["Fit_Toga"] = NewItemEffect().ClimbSpeed(3).StaminaDrain(-3),
                ["Fit_Gothic"] = GothicFit(),
                ["Fit_GreenTeeCargo"] = NewItemEffect().ClimbSpeed(-3).MoveSpeed(-3),
                ["Fit_Starboy"] = NewItemEffect().Status(STATUS.Petrify, 3),

                // Hats. Unlisted hats have no effect.
                ["Hat_2_Fedora"] = NewItemEffect().Slipperiness(1),
                ["Hat_3_Propeller"] = NewItemEffect().HotbarWeight(3),
                ["Hat_05_Aviator"] = AviatorHat(),
                ["Hat_06_Sailor"] = SailorHat(),
                ["Hat_07_Medic"] = MedicHat(),
                ["Hat_08_Midsummer"] = MidsummerHat(),
                ["Hat_09_Mushroom"] = MushroomHat(),
                ["Hat_11_Courier"] = NewItemEffect().ClimbSpeed(2).StaminaDrain(-1),
                ["Hat_12_Scoutmaster"] = NewItemEffect().StaminaRegen(1),
                ["Hat_14_NinjaHeadband"] = NinjaHeadband(),
                ["Hat_15_ChefHat"] = ChefHat(),
                ["Hat_17_WolfEars"] = NewItemEffect().Tier(TieredEffectKey.MoveSpeed, 1),
                ["Hat_19_Goat"] = GoatHorn(),
                ["Hat_20_DesertHat"] = NewItemEffect().SunHeat(2),
                ["Hat_22_SunHat"] = NewItemEffect().Status(STATUS.Thorns, 3),
                ["Hat_23_Cowboy"] = NewItemEffect().Status(STATUS.Hot, 1),
                ["Hat_24_BingBong"] = BingBongBeanie(),
                ["Hat_26_Astronaut"] = NewItemEffect().Status(STATUS.Spores, 4),
                ["Hat_27_Pumpkin"] = Pumpkin(),
                ["Hat_33_Drinks"] = DrinksHat(),
                ["Hat_35_Sprout"] = SproutHat(),
                ["Hat_39_Arrow"] = ArrowHat(),
            };

        /** Which set, if any, each cosmetic belongs to. */
        public static readonly Dictionary<string, OutfitSet> SetMembership =
            new Dictionary<string, OutfitSet>(StringComparer.OrdinalIgnoreCase)
            {
                ["Fit_Astronaut"] = OutfitSet.Astronaut,
                ["Hat_26_Astronaut"] = OutfitSet.Astronaut,
                ["Fit_Aviator"] = OutfitSet.Aviator,
                ["Hat_05_Aviator"] = OutfitSet.Aviator,
                ["Fit_Cowboy"] = OutfitSet.Cowboy,
                ["Hat_23_Cowboy"] = OutfitSet.Cowboy,
                ["Fit_Fairy"] = OutfitSet.Fairy,
                ["Hat_34_Fairy"] = OutfitSet.Fairy,
                ["Fit_Gnome"] = OutfitSet.Gnome,
                ["Hat_30_Gnome"] = OutfitSet.Gnome,
                ["Fit_Gothic"] = OutfitSet.Gothic,
                ["Hat_43_Gothic"] = OutfitSet.Gothic,
                ["Fit_Jester"] = OutfitSet.Jester,
                ["Hat_37_Jester"] = OutfitSet.Jester,
                ["Fit_Knight"] = OutfitSet.Knight,
                ["Hat_41_Knight"] = OutfitSet.Knight,
                ["Fit_Mummy"] = OutfitSet.Mummy,
                ["Hat_28_Mummy"] = OutfitSet.Mummy,
                ["Fit_PlagueDoctor"] = OutfitSet.PlagueDoctor,
                ["Hat_31_PlagueDoctor"] = OutfitSet.PlagueDoctor,
                ["Fit_Sailor_Shorts"] = OutfitSet.Sailor,
                ["Fit_Sailor_Skirt"] = OutfitSet.Sailor,
                ["Hat_06_Sailor"] = OutfitSet.Sailor,
                ["Fit_Scoutmaster_Shorts"] = OutfitSet.Scoutmaster,
                ["Fit_Scoutmaster_Skirt"] = OutfitSet.Scoutmaster,
                ["Hat_12_Scoutmaster"] = OutfitSet.Scoutmaster,
                ["Fit_SleepyGuy"] = OutfitSet.SleepyGuy,
                ["Hat_38_SleepyGuy"] = OutfitSet.SleepyGuy,
                ["Fit_Starboy"] = OutfitSet.Starboy,
                ["Hat_44_Starboy"] = OutfitSet.Starboy,
            };

        /**
         * Bonus effects granted while wearing a full set. Sets without an entry grant
         * nothing extra. Example:
         *   [OutfitSet.Scoutmaster] = NewItemEffect().StaminaRegen(1),
         */
        public static readonly Dictionary<OutfitSet, ItemEffects> SetEffects =
            new Dictionary<OutfitSet, ItemEffects>
            {
                // Astronaut set: the full suit lightens you.
                [OutfitSet.Astronaut] = new ItemEffects
                {
                    ModifyGravity = (character, gravityY) => gravityY * 0.70f,
                    ModifyJumpHeight = (character, impulse) => impulse * 1.15f,
                    Description = "Gravity -30%, JumpImpulse +15%"
                }.Mark(EffectKey.LowGravity),
                // Gothic set: the wearer sees through the Gloom.
                [OutfitSet.Gothic] = new ItemEffects
                {
                    ModifyGloomFogDensity = (character, amount) => amount * 0.90f,
                    Description = "Gloom fog -10%"
                }.Mark(EffectKey.GloomSight),
            };

        /** The set bonus for this outfit and hat pair, or null when they are not a set. */
        public static ItemEffects GetSetBonus(string outfitName, string hatName)
        {
            if (outfitName != null && hatName != null
                && SetMembership.TryGetValue(outfitName, out OutfitSet outfitSet)
                && SetMembership.TryGetValue(hatName, out OutfitSet hatSet)
                && outfitSet == hatSet
                && SetEffects.TryGetValue(outfitSet, out ItemEffects bonus))
            {
                return bonus;
            }
            return null;
        }

        public static ItemEffects Get(string assetName)
        {
            return assetName != null && Items.TryGetValue(assetName, out ItemEffects effects) ? effects : null;
        }
    }
}
