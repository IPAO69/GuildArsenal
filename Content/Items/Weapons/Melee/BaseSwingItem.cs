using Microsoft.Xna.Framework;
using GuildArsenal.Core.Players;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace GuildArsenal.Content.Items.Weapons.Melee
{
    /// <summary>Optional item parent that spawns one BaseSwingProjectile per attack.</summary>
    public abstract class BaseSwingItem : ModItem
    {
        protected abstract int SwingProjectileType { get; }
        protected abstract void SetWeaponDefaults();

        public sealed override void SetDefaults()
        {
            Item.useStyle = ItemUseStyleID.Swing;
            Item.DamageType = DamageClass.Melee;
            Item.useTime = Item.useAnimation = 24;
            Item.autoReuse = true;
            SetWeaponDefaults();
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.shoot = SwingProjectileType;
            Item.shootSpeed = 1f;
            Item.UseSound = null; // The swing plays its own sound at SwingStep.SoundAt.
        }

        public sealed override bool CanUseItem(Player player) => player.ownedProjectileCounts[Item.shoot] == 0
            && player.GetModPlayer<SwingComboPlayer>().CanStartSwing(Item.type, Item.shoot, Main.GameUpdateCount)
            && CanStartSwing(player);

        protected virtual bool CanStartSwing(Player player) => true;

        public sealed override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        {
            if (player.whoAmI == Main.myPlayer && player.ownedProjectileCounts[type] == 0)
                Projectile.NewProjectile(source, player.MountedCenter,
                    velocity.SafeNormalize(new Vector2(player.direction, 0f)), type, damage, knockback, player.whoAmI);
            return false;
        }
    }
}
