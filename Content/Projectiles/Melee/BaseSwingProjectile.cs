using System;
using System.Collections.Generic;
using System.IO;
using GuildArsenal.Core.Combat;
using GuildArsenal.Core.Players;
using GuildArsenal.Core.Graphics;
using GuildArsenal.Primitive;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace GuildArsenal.Content.Projectiles.Melee
{
    /// <summary>
    /// Inherit this class and supply a named Combo graph (or sequential ComboSteps).
    /// Spawn once per item use with an aim velocity.
    /// The owner selects the combo; ExtraAI carries the locked swing state to other peers.
    /// </summary>
    public abstract class BaseSwingProjectile : ModProjectile
    {
        protected virtual ComboDefinition Combo => null;
        protected virtual IReadOnlyList<SwingStep> ComboSteps => Combo?.Steps
            ?? throw new InvalidOperationException($"{GetType().Name} must supply Combo or ComboSteps.");
        protected virtual int ComboResetTicks => Combo?.ResetTicks ?? 90;
        /// <summary>Null selects the first allowed continuation. Override to request a specific branch.</summary>
        protected virtual string RequestedMoveId => null;
        protected virtual float? BladeLength => null;
        protected virtual int CollisionSubSteps => 3;
        protected virtual int TrailLength => 8;
        protected virtual Color TrailColor => new Color(150, 210, 255);
        protected virtual float TrailOpacity => 0.25f;
        protected virtual bool UseShaderTrail => false;
        protected virtual float ShaderTrailOpacity => 0.85f;
        protected virtual SoundStyle? SwingSound => SoundID.Item1;

        // A valid vanilla fallback means derived projectiles do not require a separate sprite asset.
        public override string Texture => $"Terraria/Images/Item_{ItemID.WoodenSword}";
        protected Player Owner => Main.player[Projectile.owner];
        public int ComboIndex { get; private set; }
        public string CurrentMoveId { get; private set; }
        public int WeaponItemType { get; private set; }
        public int SwingDuration { get; private set; }
        public int RecoveryDuration { get; private set; }
        public float SwingProgress => initialized ? Math.Clamp(elapsed / SwingDuration, 0f, 1f) : 0f;
        public bool IsSwinging => initialized && elapsed <= SwingDuration;
        public bool IsRecovering => initialized && elapsed > SwingDuration;
        public float BladeRotation { get; private set; }
        protected SwingStep CurrentStep { get; private set; }
        protected float AimRotation { get; private set; }
        protected int SwingDirection { get; private set; }
        protected float BaseWeaponScale { get; private set; }

        private bool initialized;
        private bool soundPlayed;
        private bool swingFinished;
        private float elapsed;
        private ComboDefinition comboDefinition;
        private float previousProgress;
        private float previousRotation;
        private float baseBladeLength;
        private Vector2 bladeAnchor;
        private Vector2 previousAnchor;
        private readonly List<SwingPose> trail = new();
        private SwingShaderTrail shaderTrail;
        private readonly List<BladeTrailSample> shaderSamples = new();
        private readonly record struct SwingPose(Vector2 Center, float Rotation, float Scale);

        public sealed override void SetStaticDefaults()
        {
            ProjectileID.Sets.HeldProjDoesNotUsePlayerGfxOffY[Type] = true;
            ConfigureStaticDefaults();
        }

        public sealed override void SetDefaults()
        {
            Projectile.width = Projectile.height = 16;
            Projectile.aiStyle = -1;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.ownerHitCheck = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1; // At most one hit per NPC per combo step.
            Projectile.timeLeft = 2;
            DrawHeldProjInFrontOfHeldItemAndArms = true;
            ConfigureProjectileDefaults();
        }

        protected virtual void ConfigureStaticDefaults() { }
        protected virtual void ConfigureProjectileDefaults() { }
        public sealed override bool ShouldUpdatePosition() => false;

        public sealed override void OnSpawn(IEntitySource source)
        {
            if (Projectile.owner != Main.myPlayer)
                return;

            Item weapon = source is EntitySource_ItemUse itemSource ? itemSource.Item : Owner.HeldItem;
            WeaponItemType = weapon.type;
            comboDefinition = Combo ?? ComboDefinition.CreateLinear(ComboSteps, ComboResetTicks);
            if (!Owner.GetModPlayer<SwingComboPlayer>().TryBeginSwing(WeaponItemType, Type,
                comboDefinition, Main.GameUpdateCount, out int moveIndex, RequestedMoveId))
            {
                Projectile.Kill();
                return;
            }
            ComboIndex = moveIndex;
            CurrentMoveId = comboDefinition.Moves[ComboIndex].Id;
            CurrentStep = comboDefinition.Moves[ComboIndex].Swing;

            Vector2 aim = Projectile.velocity.SafeNormalize(new Vector2(Owner.direction, 0f));
            AimRotation = aim.ToRotation();
            SwingDirection = aim.X == 0f ? Owner.direction : (aim.X > 0f ? 1 : -1);
            BaseWeaponScale = Math.Max(0.01f, Owner.GetAdjustedItemScale(weapon));
            float speed = CurrentStep.ScaleTimingWithAttackSpeed
                ? Math.Max(0.01f, Owner.GetTotalAttackSpeed(Projectile.DamageType)) : 1f;
            SwingDuration = Math.Max(1, (int)MathF.Round((CurrentStep.DurationTicks ?? Math.Max(1, weapon.useTime)) / speed));
            RecoveryDuration = CurrentStep.RecoveryTicks == 0 ? 0 : Math.Max(1, (int)MathF.Round(CurrentStep.RecoveryTicks / speed));

            Texture2D texture = GetDrawTexture();
            baseBladeLength = BladeLength ?? new Vector2(texture.Width, texture.Height).Length();
            if (!float.IsFinite(baseBladeLength) || baseBladeLength <= 0f)
                throw new InvalidOperationException("BladeLength must be positive and finite.");

            initialized = true;
            ApplyPose();
            RememberPreviousPose();
            OnSwingStarted();
            Projectile.netUpdate = true;
        }

        public sealed override void SendExtraAI(BinaryWriter writer)
        {
            writer.Write(initialized);
            if (!initialized)
                return;
            writer.Write(WeaponItemType);
            writer.Write(ComboIndex);
            writer.Write(SwingDirection);
            writer.Write(AimRotation);
            writer.Write(BaseWeaponScale);
            writer.Write(baseBladeLength);
            writer.Write(SwingDuration);
            writer.Write(RecoveryDuration);
            writer.Write(elapsed);
            WriteSwingExtraAI(writer);
        }

        public sealed override void ReceiveExtraAI(BinaryReader reader)
        {
            if (!reader.ReadBoolean())
                return;
            bool firstSync = !initialized;
            WeaponItemType = reader.ReadInt32();
            ComboIndex = reader.ReadInt32();
            SwingDirection = reader.ReadInt32();
            AimRotation = reader.ReadSingle();
            BaseWeaponScale = reader.ReadSingle();
            baseBladeLength = reader.ReadSingle();
            SwingDuration = reader.ReadInt32();
            RecoveryDuration = reader.ReadInt32();
            elapsed = reader.ReadSingle();
            comboDefinition = Combo ?? ComboDefinition.CreateLinear(ComboSteps, ComboResetTicks);
            if (ComboIndex < 0 || ComboIndex >= comboDefinition.Moves.Count)
            {
                Projectile.Kill();
                return;
            }
            CurrentMoveId = comboDefinition.Moves[ComboIndex].Id;
            CurrentStep = comboDefinition.Moves[ComboIndex].Swing;
            initialized = true;
            ReadSwingExtraAI(reader);
            ApplyPose();
            RememberPreviousPose(); // A correction must not create a giant collision sweep.
            if (!firstSync)
                soundPlayed |= SwingProgress >= CurrentStep.SoundAt;
            if (firstSync)
                OnSwingStarted();
        }

        protected virtual void WriteSwingExtraAI(BinaryWriter writer) { }
        protected virtual void ReadSwingExtraAI(BinaryReader reader) { }

        public sealed override void AI()
        {
            if (!Owner.active || Owner.dead || Owner.noItems || Owner.CCed
                || (initialized && Projectile.owner == Main.myPlayer && Owner.HeldItem.type != WeaponItemType))
            {
                Projectile.Kill();
                return;
            }

            Projectile.timeLeft = 2;
            if (!initialized)
                return; // Remote peers wait for the owning client's snapshot.

            RememberPreviousPose();
            elapsed += 1f / (Projectile.extraUpdates + 1);
            if (elapsed > SwingDuration + RecoveryDuration)
            {
                Projectile.Kill();
                return;
            }

            ApplyPose();
            if (Projectile.owner == Main.myPlayer)
                Owner.GetModPlayer<SwingComboPlayer>().KeepAlive(WeaponItemType, Type, Main.GameUpdateCount);

            if (!soundPlayed && SwingProgress >= CurrentStep.SoundAt)
            {
                soundPlayed = true;
                if (!Main.dedServ && SwingSound is SoundStyle sound)
                    SoundEngine.PlaySound(sound, Projectile.Center);
            }

            if (IsSwinging)
            {
                if (!Main.dedServ && TrailLength > 0)
                {
                    trail.Insert(0, new SwingPose(Projectile.Center, BladeRotation, Projectile.scale));
                    while (trail.Count > TrailLength)
                        trail.RemoveAt(trail.Count - 1);
                }
                OnSwingUpdate(SwingProgress);
            }
            else if (trail.Count > 0)
                trail.RemoveAt(trail.Count - 1);

            if (!swingFinished && elapsed >= SwingDuration)
            {
                swingFinished = true;
                OnSwingFinished();
            }
            if (IsRecovering)
                OnRecoveryUpdate((elapsed - SwingDuration) / Math.Max(1, RecoveryDuration));
        }

        private void RememberPreviousPose()
        {
            previousProgress = SwingProgress;
            previousRotation = BladeRotation;
            previousAnchor = bladeAnchor;
        }

        protected virtual float EvaluateRotation(float progress) => AimRotation
            + MathHelper.ToRadians(CurrentStep.EvaluateAngleDegrees(progress)) * SwingDirection * Owner.gravDir;

        protected virtual float EvaluateScale(float progress) => BaseWeaponScale * CurrentStep.EvaluateScale(progress);
        protected virtual float EvaluateHoldOffset(float progress) => CurrentStep.EvaluateHoldOffset(progress);

        private void ApplyPose()
        {
            BladeRotation = EvaluateRotation(SwingProgress);
            Projectile.rotation = BladeRotation;
            Projectile.scale = EvaluateScale(SwingProgress);
            Projectile.direction = Projectile.spriteDirection = SwingDirection;
            Owner.ChangeDir(SwingDirection);
            Owner.heldProj = Projectile.whoAmI;
            Owner.SetDummyItemTime(2);
            float armRotation = BladeRotation - MathHelper.PiOver2;
            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, armRotation);
            bladeAnchor = Owner.GetFrontHandPosition(Player.CompositeArmStretchAmount.Full, armRotation)
                + Vector2.UnitY * Owner.gfxOffY;
            Projectile.Center = bladeAnchor
                + BladeRotation.ToRotationVector2() * EvaluateHoldOffset(SwingProgress) * Projectile.scale;
            Owner.itemRotation = MathHelper.WrapAngle(BladeRotation - (SwingDirection < 0 ? MathHelper.Pi : 0f));
            Owner.itemLocation = Projectile.Center;
        }

        protected virtual void OnSwingStarted() { }
        protected virtual void OnSwingUpdate(float progress) { }
        protected virtual void OnSwingFinished() { }
        protected virtual void OnRecoveryUpdate(float progress) { }

        public override bool? CanDamage() => IsSwinging && CurrentStep.MotionValue > 0f
            && CurrentStep.TryGetDamageWindow(previousProgress, SwingProgress, out _, out _) ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            if (CanDamage() == false)
                return false;

            // Linear unwrapped angles preserve >180-degree swings. Sample scale at the same instant as rotation.
            int samples = Math.Clamp(Math.Max(CollisionSubSteps,
                (int)MathF.Ceiling(MathF.Abs(BladeRotation - previousRotation) / MathHelper.ToRadians(5f))), 1, 128);
            CurrentStep.TryGetDamageWindow(previousProgress, SwingProgress, out float windowStart, out float windowEnd);
            for (int i = 0; i <= samples; i++)
            {
                float progress = MathHelper.Lerp(windowStart, windowEnd, i / (float)samples);
                float t = SwingProgress > previousProgress
                    ? (progress - previousProgress) / (SwingProgress - previousProgress) : 1f;
                float scale = EvaluateScale(progress);
                if (scale <= 0f)
                    continue;
                float rotation = EvaluateRotation(progress);
                Vector2 pivot = Vector2.Lerp(previousAnchor, bladeAnchor, t)
                    + rotation.ToRotationVector2() * EvaluateHoldOffset(progress) * scale;
                Vector2 blade = rotation.ToRotationVector2() * baseBladeLength * scale;
                Vector2 start = pivot + blade * CurrentStep.CollisionStartRatio;
                float collisionPoint = 0f;
                if (Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                    start, pivot + blade, CurrentStep.CollisionWidth * scale, ref collisionPoint))
                    return true;
            }
            return false;
        }

        public override void CutTiles()
        {
            if (!IsSwinging || SwingProgress < CurrentStep.DamageStart || SwingProgress > CurrentStep.DamageEnd)
                return;
            Vector2 blade = BladeRotation.ToRotationVector2() * baseBladeLength * Projectile.scale;
            DelegateMethods.tilecut_0 = TileCuttingContext.AttackProjectile;
            Utils.PlotTileLine(Projectile.Center + blade * CurrentStep.CollisionStartRatio,
                Projectile.Center + blade, CurrentStep.CollisionWidth * Projectile.scale, DelegateMethods.CutTiles);
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
        {
            // Projectile.damage is the weapon damage already adjusted for player bonuses.
            // Apply MV exactly once here; Terraria still handles defense, variance and critical hits.
            modifiers.SourceDamage *= CurrentStep.DamageFactor;
            modifiers.HitDirectionOverride = target.Center.X >= Owner.MountedCenter.X ? 1 : -1;
            ModifySwingHitNPC(target, ref modifiers);
        }

        public override void ModifyHitPlayer(Player target, ref Player.HurtModifiers modifiers)
        {
            modifiers.SourceDamage *= CurrentStep.DamageFactor;
        }

        protected virtual void ModifySwingHitNPC(NPC target, ref NPC.HitModifiers modifiers) { }

        protected virtual Texture2D GetDrawTexture()
        {
            Main.instance.LoadItem(WeaponItemType);
            return TextureAssets.Item[WeaponItemType].Value;
        }

        protected virtual Vector2 GetDrawOrigin(Texture2D texture) => SwingDirection > 0
            ? new Vector2(0f, CurrentStep.ReverseBladeEdge ? 0f : texture.Height)
            : new Vector2(texture.Width, CurrentStep.ReverseBladeEdge ? 0f : texture.Height);
        protected virtual float SpriteRotationOffset => (SwingDirection > 0 ? MathHelper.PiOver4 : 3f * MathHelper.PiOver4)
            * (CurrentStep.ReverseBladeEdge ? -1f : 1f);
        protected virtual SpriteEffects DrawEffects => (SwingDirection > 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally)
            | (CurrentStep.ReverseBladeEdge ? SpriteEffects.FlipVertically : SpriteEffects.None);

        public sealed override bool PreDraw(ref Color lightColor)
        {
            if (!initialized)
                return false;
            Texture2D texture = GetDrawTexture();
            Vector2 origin = GetDrawOrigin(texture);
            DrawSwingTrail(texture, origin);
            Vector2 drawPosition = Projectile.Center - Main.screenPosition;
            float drawRotation = BladeRotation + SpriteRotationOffset;
            Main.EntitySpriteDraw(texture, drawPosition, null, lightColor, drawRotation,
                origin, Projectile.scale, DrawEffects);
            PostDrawSwing(texture, drawPosition, drawRotation, origin, DrawEffects, lightColor);
            return false;
        }

        protected virtual void DrawSwingTrail(Texture2D texture, Vector2 origin)
        {
            if (UseShaderTrail && SwingTrailShaderSystem.TrailEffect is Effect effect)
            {
                shaderTrail ??= new SwingShaderTrail(); // Allocate ribbon arrays only for clients that draw this trail.
                shaderSamples.Clear();
                foreach (SwingPose pose in trail)
                    shaderSamples.Add(new BladeTrailSample(pose.Center, pose.Rotation, baseBladeLength * pose.Scale));
                bool thrust = MathF.Abs(CurrentStep.EndAngleDegrees - CurrentStep.StartAngleDegrees) < 0.01f;
                // Fade out during follow-through/recovery instead of leaving a stationary bright fan.
                float fade = Math.Clamp((1f - SwingProgress) / Math.Max(0.01f, 1f - CurrentStep.DamageEnd), 0f, 1f);
                if (thrust)
                    fade *= Math.Clamp((SwingProgress - CurrentStep.DamageStart) / 0.08f, 0f, 1f);
                if (shaderTrail.Draw(shaderSamples, effect, TrailColor, ShaderTrailOpacity * fade,
                    CurrentStep.TrailEdgeSharpness, thrust))
                    return;
            }
            for (int i = trail.Count - 1; i >= 0; i--)
            {
                SwingPose pose = trail[i];
                float opacity = TrailOpacity * (1f - (i + 1f) / (trail.Count + 1f));
                Main.EntitySpriteDraw(texture, pose.Center - Main.screenPosition, null, TrailColor * opacity,
                    pose.Rotation + SpriteRotationOffset, origin, pose.Scale, DrawEffects);
            }
        }

        protected virtual void PostDrawSwing(Texture2D texture, Vector2 drawPosition, float drawRotation,
            Vector2 origin, SpriteEffects effects, Color lightColor) { }
    }
}
