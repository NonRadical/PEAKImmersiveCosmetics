//PEAK Immersive Cosmetics
//Distributed under the MIT License @ https://github.com/NonRadical/PEAKImmersiveCosmetics

using System;
using System.Collections.Generic;
using Peak.Afflictions;
using STATUS = CharacterAfflictions.STATUSTYPE;

namespace PEAKImmersiveCosmetics
{
    /**
     * Non-status effects whose strength scales with tiers via TierScale.
     * Status resistances use STATUSTYPE directly. Member names double as HUD icon keys.
     */
    public enum TieredEffectKey
    {
        Recovery,
        StaminaDrain,
        StaminaDrainInAir,
        StaminaRegen,
        MoveSpeed,
        ClimbSpeed,
        Grip,
        HeldWeight,
        SunHeat,
        JumpHeight,
        TrapDamage,
    }

    /**
     * Marker effects with fixed or conditional behavior baked into their lambdas.
     * They have no tier scale and their HUD icons show no pips.
     */
    public enum EffectKey
    {
        StormCold,
        CookedFood,
        UncookedFood,
        ShroomImmunity,
        RandomResist,
        IdolWeight,
        ThornPull,
        PitonRegen,
        SunRegen,
        GloomRegen,
        BeetleKnockback,
        LowGravity,
        EnergyDrinkBoost,
        ZombieHidden,
        GloomSight,
        CursedLuggage,
        SurvivorClimb,
        MedicFeed,
    }

    /**
     * Matching outfit and hat families. A player wearing two cosmetics that map to the
     * same set (see EffectRegistry.SetMembership) receives that set's bonus effect
     * (see EffectRegistry.SetEffects), on top of the individual item effects.
     */
    public enum OutfitSet
    {
        Astronaut,
        Aviator,
        Cowboy,
        Fairy,
        Gnome,
        Gothic,
        Jester,
        Knight,
        Mummy,
        PlagueDoctor,
        Sailor,
        Scoutmaster,
        SleepyGuy,
        Starboy,
    }

    public static class EffectKeys
    {
        /** String form used for HUD merging and icon filenames. */
        public static string ToKeyString(this TieredEffectKey key)
        {
            return key.ToString();
        }

        /** String form used for HUD merging and icon filenames. */
        public static string ToKeyString(this EffectKey key)
        {
            return key.ToString();
        }

        /** Case-insensitive parse of a tiered effect key string. */
        public static bool TryParse(string name, out TieredEffectKey key)
        {
            return Enum.TryParse(name, ignoreCase: true, out key);
        }

        /** Case-insensitive parse of a marker effect key string. */
        public static bool TryParse(string name, out EffectKey key)
        {
            return Enum.TryParse(name, ignoreCase: true, out key);
        }
    }

    /**
     * Defines what one tier is worth in percent for each effect.
     * Unlisted entries fall back to DefaultPercentPerTier.
     */
    public static class TierScale
    {
        public const int DefaultPercentPerTier = 10;

        /** Percent per tier for status resistances. */
        public static readonly Dictionary<STATUS, int> StatusPercentPerTier = new Dictionary<STATUS, int>
        {
            [STATUS.Web] = 15,
            [STATUS.Weight] = 5
        };

        /** Percent per tier for per-status recovery (the Heal builder). */
        public static readonly Dictionary<STATUS, int> StatusRecoveryPercentPerTier = new Dictionary<STATUS, int>
        {
        };

        /** Percent per tier for non-status effects. */
        public static readonly Dictionary<TieredEffectKey, int> EffectPercentPerTier = new Dictionary<TieredEffectKey, int>
        {
        };

        public static int Percent(STATUS status, int tier)
        {
            if (!StatusPercentPerTier.TryGetValue(status, out int perTier))
            {
                perTier = DefaultPercentPerTier;
            }
            return perTier * tier;
        }

        public static int RecoveryPercent(STATUS status, int tier)
        {
            if (!StatusRecoveryPercentPerTier.TryGetValue(status, out int perTier))
            {
                perTier = DefaultPercentPerTier;
            }
            return perTier * tier;
        }

        public static int Percent(TieredEffectKey effectKey, int tier)
        {
            if (!EffectPercentPerTier.TryGetValue(effectKey, out int perTier))
            {
                perTier = DefaultPercentPerTier;
            }
            return perTier * tier;
        }
    }

    /**
     * Effects for a single cosmetic item. Each hook is optional (null means no effect).
     * Hooks receive the wearing character and the game value, and return the modified value.
     */
    public sealed class ItemEffects
    {
        /** Status buildup: (character, status, amount) to new amount. Also covers Weight/Thorns/Arrow and gradual Petrify. */
        public Func<Character, STATUS, float, float> ModifyStatus;

        /** Status recovery, both item healing and natural decay. Bigger means faster healing. */
        public Func<Character, STATUS, float, float> ModifyStatusHeal;

        /** Stamina cost of climbing, jumping and sprinting. */
        public Func<Character, float, float> ModifyStaminaDrain;
        
        /** Stamina cost of climbing, jumping and sprinting. */
        public Func<Character, float, float> ModifyStaminaDrainInAir;

        /** Natural resting stamina regen per tick. */
        public Func<Character, float, float> ModifyStaminaRegen;

        /** Ground and air movement force. Force-based, so gains are not perfectly linear. */
        public Func<Character, float, float> ModifyMoveSpeed;

        /** Climbing speed for wall, rope and vine climbing. */
        public Func<Character, float, float> ModifyClimbSpeed;

        /** Wall slipperiness from rain (0..1). Lower means more grip. */
        public Func<Character, float, float> ModifySlipperiness;

        /** Weight of held items only (hotbar plus hands), not the backpack. Runs before ModifyStatus(Weight). */
        public Func<Character, float, float> ModifyHotbarWeight;

        /** Weight of a single held item. Runs per item, before ModifyHotbarWeight. */
        public Func<Character, Item, float, float> ModifyItemWeight;

        /** Heat from sun exposure only, before it enters the normal Hot pipeline. */
        public Func<Character, float, float> ModifySunHeat;

        /** Status change from consuming or using an item. Delta is signed; return 0 to block. */
        public Func<Character, Item, STATUS, float, float> ModifyItemStatusChange;

        /** Return true to prevent an affliction type from being applied. */
        public Func<Character, Affliction.AfflictionType, bool> BlockAffliction;

        /** Status damage taken when a thorn or arrow is pulled out. */
        public Func<Character, float, float> ModifyThornPullStatus;

        /** Knockback force from a beetle bonk, applied to both the forward and upward components. */
        public Func<Character, float, float> ModifyBeetleKnockback;

        /** Vertical gravity accel while airborne: (character, gravityY) to new gravityY.
         * Negative pulls down. Also shapes jump arcs, and slower falls reduce fall damage. */
        public Func<Character, float, float> ModifyGravity;

        /** Return true to make zombies ignore this character when acquiring or keeping a
         * target. Evaluated on the zombie's controlling client, which must run the mod. */
        public Func<Character, bool> HiddenFromZombies;

        /** Jump impulse: (character, impulse) to new impulse. Covers normal, double and
         * pal jumps, not super jumps. Height scales roughly with the square of impulse. */
        public Func<Character, float, float> ModifyJumpHeight;

        /** Energy drink boost: (character, boost) to new boost. Applied separately to the
         * drink's move speed and climb speed bonuses. */
        public Func<Character, float, float> ModifyEnergyDrinkBoost;

        /** Gloom fog density: (character, amount) to new amount. Purely visual and local
         * to the wearer's screen; applied only while in the Gloom biome. */
        public Func<Character, float, float> ModifyGloomFogDensity;

        /** Fires when the wearer feeds an item to a teammate: (feeder, eater, item).
         * Runs on the feeder's client at the moment the feed is dispatched, so it may
         * change the feeder freely but cannot know how much healing actually landed. */
        public Action<Character, Character, Item> OnFeedTeammate;

        /** Status buildup inflicted by traps (swinging axe, spike roller, saw blade):
         * (character, amount) to new amount. */
        public Func<Character, float, float> ModifyTrapDamage;

        /** Status buildup inflicted by opening cursed luggage, both the curse and the
         * injury: (character, amount) to new amount. */
        public Func<Character, float, float> ModifyCursedLuggageDamage;

        /** Human-readable summary, shown in logs and the cosmetics dump. */
        public string Description = "";

        /**
         * Signed effect tiers by key, used by the HUD icons. The HUD sums outfit and hat
         * tiers per key; a net of 0 shows no icon. Builders fill this automatically.
         * Lambda effects declare theirs with Tier().
         */
        public readonly Dictionary<string, int> Tiers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        /** Marker effects this item grants. Shown by the HUD as icons without pips. */
        public readonly HashSet<string> Markers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /** Declares (or adds to) this item's tier for a non-status effect. */
        public ItemEffects Tier(TieredEffectKey effectKey, int tier)
        {
            return TierByName(effectKey.ToKeyString(), tier);
        }

        /** Declares an untiered marker effect on this item. */
        public ItemEffects Mark(EffectKey effectKey)
        {
            Markers.Add(effectKey.ToKeyString());
            return this;
        }

        /** Declares (or adds to) this item's tier for a status resistance. */
        public ItemEffects Tier(STATUS status, int tier)
        {
            return TierByName(status.ToString(), tier);
        }

        private ItemEffects TierByName(string name, int tier)
        {
            Tiers.TryGetValue(name, out int existing);
            Tiers[name] = existing + tier;
            return this;
        }

        // Builders take tiers (1..4, negative for penalties). TierScale defines percent per tier.

        /** Reduces buildup of one status. */
        public ItemEffects Status(STATUS type, int tier)
        {
            int percent = TierScale.Percent(type, tier);
            float mult = 1f - percent / 100f;
            var previous = ModifyStatus;
            ModifyStatus = (character, t, amount) =>
            {
                if (previous != null)
                {
                    amount = previous(character, t, amount);
                }
                return t == type ? amount * mult : amount;
            };
            AppendDescription($"{type} {-percent:+0;-0}%");
            return Tier(type, tier);
        }

        /** Speeds up recovery of one status. */
        public ItemEffects Heal(STATUS type, int tier)
        {
            int percent = TierScale.RecoveryPercent(type, tier);
            float mult = 1f + percent / 100f;
            var previous = ModifyStatusHeal;
            ModifyStatusHeal = (character, t, amount) =>
            {
                if (previous != null)
                {
                    amount = previous(character, t, amount);
                }
                return t == type ? amount * mult : amount;
            };
            AppendDescription($"{type} recovery {percent:+0;-0}%");
            return TierByName($"{type}Recovery", tier);
        }

        /** Speeds up recovery of all statuses. */
        public ItemEffects HealAll(int tier)
        {
            int percent = TierScale.Percent(TieredEffectKey.Recovery, tier);
            float mult = 1f + percent / 100f;
            var previous = ModifyStatusHeal;
            ModifyStatusHeal = (character, t, amount) =>
            {
                if (previous != null)
                {
                    amount = previous(character, t, amount);
                }
                return amount * mult;
            };
            AppendDescription($"Recovery {percent:+0;-0}%");
            return Tier(TieredEffectKey.Recovery, tier);
        }

        /** Reduces stamina drain. */
        public ItemEffects StaminaDrain(int tier)
        {
            int percent = TierScale.Percent(TieredEffectKey.StaminaDrain, tier);
            float mult = 1f - percent / 100f;
            var previous = ModifyStaminaDrain;
            ModifyStaminaDrain = (character, usage) => (previous != null ? previous(character, usage) : usage) * mult;
            AppendDescription($"StaminaDrain {-percent:+0;-0}%");
            return Tier(TieredEffectKey.StaminaDrain, tier);
        }
        
        /** Reduces in-air stamina drain. */
        public ItemEffects StaminaDrainInAir(int tier)
        {
            int percent = TierScale.Percent(TieredEffectKey.StaminaDrainInAir, tier);
            float mult = 1f - percent / 100f;
            var previous = ModifyStaminaDrainInAir;
            ModifyStaminaDrainInAir = (character, usage) => (previous != null ? previous(character, usage) : usage) * mult;
            AppendDescription($"StaminaDrainInAir {-percent:+0;-0}%");
            return Tier(TieredEffectKey.StaminaDrainInAir, tier);
        }

        /** Reduces status buildup inflicted by traps. */
        public ItemEffects TrapDamage(int tier)
        {
            int percent = TierScale.Percent(TieredEffectKey.TrapDamage, tier);
            float mult = 1f - percent / 100f;
            var previous = ModifyTrapDamage;
            ModifyTrapDamage = (character, amount) => (previous != null ? previous(character, amount) : amount) * mult;
            AppendDescription($"TrapDamage {-percent:+0;-0}%");
            return Tier(TieredEffectKey.TrapDamage, tier);
        }

        /** Boosts jump impulse. Height scales roughly with the square of impulse. */
        public ItemEffects JumpHeight(int tier)
        {
            int percent = TierScale.Percent(TieredEffectKey.JumpHeight, tier);
            float mult = 1f + percent / 100f;
            var previous = ModifyJumpHeight;
            ModifyJumpHeight = (character, impulse) => (previous != null ? previous(character, impulse) : impulse) * mult;
            AppendDescription($"JumpImpulse {percent:+0;-0}%");
            return Tier(TieredEffectKey.JumpHeight, tier);
        }

        /** Boosts natural resting regen. */
        public ItemEffects StaminaRegen(int tier)
        {
            int percent = TierScale.Percent(TieredEffectKey.StaminaRegen, tier);
            float mult = 1f + percent / 100f;
            var previous = ModifyStaminaRegen;
            ModifyStaminaRegen = (character, regen) => (previous != null ? previous(character, regen) : regen) * mult;
            AppendDescription($"StaminaRegen {percent:+0;-0}%");
            return Tier(TieredEffectKey.StaminaRegen, tier);
        }

        /** Boosts movement speed. */
        public ItemEffects MoveSpeed(int tier)
        {
            int percent = TierScale.Percent(TieredEffectKey.MoveSpeed, tier);
            float mult = 1f + percent / 100f;
            var previous = ModifyMoveSpeed;
            ModifyMoveSpeed = (character, force) => (previous != null ? previous(character, force) : force) * mult;
            AppendDescription($"MoveSpeed {percent:+0;-0}%");
            return Tier(TieredEffectKey.MoveSpeed, tier);
        }

        /** Boosts climbing speed for wall, rope and vine climbing. */
        public ItemEffects ClimbSpeed(int tier)
        {
            int percent = TierScale.Percent(TieredEffectKey.ClimbSpeed, tier);
            float mult = 1f + percent / 100f;
            var previous = ModifyClimbSpeed;
            ModifyClimbSpeed = (character, speedMod) => (previous != null ? previous(character, speedMod) : speedMod) * mult;
            AppendDescription($"ClimbSpeed {percent:+0;-0}%");
            return Tier(TieredEffectKey.ClimbSpeed, tier);
        }

        /** Reduces the weight of held items, leaving backpack weight untouched. */
        public ItemEffects HotbarWeight(int tier)
        {
            int percent = TierScale.Percent(TieredEffectKey.HeldWeight, tier);
            float mult = 1f - percent / 100f;
            var previous = ModifyHotbarWeight;
            ModifyHotbarWeight = (character, weight) => (previous != null ? previous(character, weight) : weight) * mult;
            AppendDescription($"HeldItemWeight {-percent:+0;-0}%");
            return Tier(TieredEffectKey.HeldWeight, tier);
        }

        /** Reduces wall slipperiness in the rain. */
        public ItemEffects Slipperiness(int tier)
        {
            int percent = TierScale.Percent(TieredEffectKey.Grip, tier);
            float mult = 1f - percent / 100f;
            var previous = ModifySlipperiness;
            ModifySlipperiness = (character, slippy) => (previous != null ? previous(character, slippy) : slippy) * mult;
            AppendDescription($"Slipperiness {-percent:+0;-0}%");
            return Tier(TieredEffectKey.Grip, tier);
        }

        /** Reduces heat gained from the sun. */
        public ItemEffects SunHeat(int tier)
        {
            int percent = TierScale.Percent(TieredEffectKey.SunHeat, tier);
            float mult = 1f - percent / 100f;
            var previous = ModifySunHeat;
            ModifySunHeat = (character, amount) => (previous != null ? previous(character, amount) : amount) * mult;
            AppendDescription($"SunHeat {-percent:+0;-0}%");
            return Tier(TieredEffectKey.SunHeat, tier);
        }

        internal void AppendDescription(string part)
        {
            Description = Description.Length == 0 ? part : Description + ", " + part;
        }
    }
}
