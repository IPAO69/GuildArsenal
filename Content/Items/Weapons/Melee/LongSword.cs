using GuildArsenal.Content.Projectiles.Melee;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace GuildArsenal.Content.Items.Weapons.Melee
{
    /// <summary>The first Monster Hunter-inspired weapon, with one attack per left click.</summary>
    public sealed class LongSword : BaseSwingItem
    {
        public override string Texture => $"Terraria/Images/Item_{ItemID.TerraBlade}";
        protected override int SwingProjectileType => ModContent.ProjectileType<LongSwordSwing>();

        protected override void SetWeaponDefaults()
        {
            Item.width = Item.height = 48;
            Item.damage = 38;
            Item.knockBack = 4.5f;
            Item.crit = 6;
            Item.useTime = Item.useAnimation = 26;
            Item.scale = 1.4f;
            Item.autoReuse = false;
            Item.rare = ItemRarityID.Green;
            Item.value = Item.buyPrice(gold: 2);
        }

        // Preserve click-by-click combos even with accessories that normally grant autoswing.
        public override bool? CanAutoReuseItem(Player player) => false;

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ItemID.Wood, 20)
                .AddRecipeGroup(RecipeGroupID.IronBar, 10)
                .AddTile(TileID.Anvils)
                .Register();
        }
    }
}
