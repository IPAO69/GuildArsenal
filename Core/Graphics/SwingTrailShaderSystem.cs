using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ModLoader;

namespace GuildArsenal.Core.Graphics
{
    public sealed class SwingTrailShaderSystem : ModSystem
    {
        private static Asset<Effect> trailAsset;
        public static Effect TrailEffect => trailAsset?.Value;

        public override void Load()
        {
            if (!Main.dedServ)
                trailAsset = ModContent.Request<Effect>("GuildArsenal/Assets/Effects/SwingTrail", AssetRequestMode.ImmediateLoad);
        }

        public override void Unload()
        {
            // The mod's asset repository owns/disposes the Effect; no client assets on dedicated servers.
            trailAsset = null;
        }
    }
}
