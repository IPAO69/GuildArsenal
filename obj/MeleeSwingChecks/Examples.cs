using System.Collections.Generic;
using GuildArsenal.Content.Items.Weapons.Melee;
using GuildArsenal.Content.Projectiles.Melee;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

// Compile the documented integration without registering example weapons in the user's mod.
namespace GuildArsenal.Content.Items.Weapons.Melee
{
    public class GuildComboSword : BaseSwingItem
    {
        public override string Texture => $"Terraria/Images/Item_{ItemID.WoodenSword}";
        protected override int SwingProjectileType => ModContent.ProjectileType<GuildComboSwing>();

        protected override void SetWeaponDefaults()
        {
            Item.width = Item.height = 40;
            Item.damage = 30;
            Item.knockBack = 5f;
            Item.useTime = Item.useAnimation = 24;
            Item.scale = 1.5f;
            Item.rare = ItemRarityID.Green;
            Item.value = Item.buyPrice(silver: 50);
        }
    }
}

namespace GuildArsenal.Content.Projectiles.Melee
{
    public class GuildComboSwing : BaseSwingProjectile
    {
        private static readonly SwingStep[] Steps =
        {
            new SwingStep
            {
                StartAngleDegrees = -110f, EndAngleDegrees = 90f,
                DurationTicks = 24, RecoveryTicks = 4, DamageStart = 0.25f, DamageEnd = 0.8f,
                ScaleCurve = new SwingCurve(new SwingKeyframe(0f, 0.65f), new SwingKeyframe(0.45f, 1.15f), new SwingKeyframe(1f, 0.8f))
            },
            new SwingStep
            {
                StartAngleDegrees = 95f, EndAngleDegrees = -110f, DurationTicks = 20, RecoveryTicks = 4,
                ScaleCurve = new SwingCurve(new SwingKeyframe(0f, 0.8f), new SwingKeyframe(0.5f, 1.25f), new SwingKeyframe(1f, 0.9f))
            },
            new SwingStep
            {
                StartAngleDegrees = -150f, EndAngleDegrees = 150f, DurationTicks = 32, RecoveryTicks = 10,
                MotionValue = 150f, DamageStart = 0.3f, DamageEnd = 0.85f,
                RotationCurve = new SwingCurve(new SwingKeyframe(0f, 0f), new SwingKeyframe(0.3f, 0.05f),
                    new SwingKeyframe(0.65f, 0.95f, SwingEasing.CubicOut), new SwingKeyframe(1f, 1f)),
                ScaleCurve = new SwingCurve(new SwingKeyframe(0f, 0.6f), new SwingKeyframe(0.55f, 1.7f), new SwingKeyframe(1f, 0.75f))
            }
        };
        protected override IReadOnlyList<SwingStep> ComboSteps => Steps;
        protected override int ComboResetTicks => 90;
        protected override int TrailLength => 10;
    }
}
