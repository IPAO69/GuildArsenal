using GuildArsenal.Core.Combat;
using Microsoft.Xna.Framework;
using Terraria.ID;

namespace GuildArsenal.Content.Projectiles.Melee
{
    public sealed class LongSwordSwing : BaseSwingProjectile
    {
        public override string Texture => $"Terraria/Images/Item_{ItemID.TerraBlade}";
        protected override ComboDefinition Combo => LongswordMove.Combo;
        protected override int TrailLength => 10;
        protected override bool UseShaderTrail => true;
        protected override Color TrailColor => new Color(100, 255, 170);
        protected override float TrailOpacity => 0.3f;
    }
}
