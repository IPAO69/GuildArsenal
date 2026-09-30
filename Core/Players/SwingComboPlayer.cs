using System.Collections.Generic;
using GuildArsenal.Core.Combat;
using Terraria.ModLoader;

namespace GuildArsenal.Core.Players
{
    /// <summary>Combo state belongs to a player and weapon/projectile pair, never to a static global cache.</summary>
    public sealed class SwingComboPlayer : ModPlayer
    {
        private readonly Dictionary<(int ItemType, int ProjectileType), ComboManager> combos = new();

        public override void Initialize() => combos.Clear();
        public override void OnEnterWorld() => combos.Clear();
        public override void UpdateDead() => combos.Clear();

        internal bool TryBeginSwing(int itemType, int projectileType, ComboDefinition definition, ulong tick,
            out int moveIndex, string requestedMoveId = null)
        {
            var key = (itemType, projectileType);
            if (!combos.TryGetValue(key, out ComboManager manager))
                combos[key] = manager = new ComboManager();
            return manager.TryBegin(definition, tick, out moveIndex, requestedMoveId);
        }

        internal void KeepAlive(int itemType, int projectileType, ulong tick)
        {
            if (combos.TryGetValue((itemType, projectileType), out ComboManager manager))
                manager.Touch(tick);
        }

        public void ResetCombo(int itemType, int projectileType) => combos.Remove((itemType, projectileType));
    }
}
