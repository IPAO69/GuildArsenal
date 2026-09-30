using GuildArsenal.Core.Combat;

namespace GuildArsenal.Content.Projectiles.Melee
{
    /// <summary>Long Sword attacks and the explicit moves allowed after each one.</summary>
    public static class LongswordMove
    {
        public const int ResetTicks = 90;
        public const string OverheadSlash = nameof(OverheadSlash);
        public const string Thrust = nameof(Thrust);
        public const string RisingSlash = nameof(RisingSlash);
        public const string WideFinisher = nameof(WideFinisher);

        public static ComboDefinition Combo { get; } = new ComboDefinition(OverheadSlash, ResetTicks,
            new ComboMove(OverheadSlash, new SwingStep
            {
                // First click: wind up above the shoulder and cut downward.
                StartAngleDegrees = -115f,
                EndAngleDegrees = 65f,
                DurationTicks = 26,
                RecoveryTicks = 4,
                DamageStart = 0.28f,
                DamageEnd = 0.82f,
                MotionValue = 100f,
                CollisionWidth = 18f,
                CollisionStartRatio = 0.16f,
                SoundAt = 0.3f,
                RotationCurve = new SwingCurve(
                    new SwingKeyframe(0f, 0f),
                    new SwingKeyframe(0.25f, 0.06f),
                    new SwingKeyframe(0.72f, 0.96f, SwingEasing.SmootherStep),
                    new SwingKeyframe(1f, 1f)),
                ScaleCurve = new SwingCurve(
                    new SwingKeyframe(0f, 0.85f),
                    new SwingKeyframe(0.5f, 1.08f),
                    new SwingKeyframe(1f, 0.95f))
            }, Thrust),
            new ComboMove(Thrust, new SwingStep
            {
                // Second click: keep the tip aimed forward, pull back, thrust quickly, then retract.
                StartAngleDegrees = 0f,
                EndAngleDegrees = 0f,
                DurationTicks = 12,
                RecoveryTicks = 3,
                DamageStart = 0.2f,
                DamageEnd = 0.62f,
                MotionValue = 65f,
                CollisionWidth = 12f,
                CollisionStartRatio = 0.2f,
                SoundAt = 0.22f,
                ScaleCurve = SwingCurve.Constant(1f),
                HoldOffsetCurve = new SwingCurve(
                    new SwingKeyframe(0f, -18f),
                    new SwingKeyframe(0.18f, -22f),
                    new SwingKeyframe(0.45f, 42f, SwingEasing.CubicOut),
                    new SwingKeyframe(0.58f, 42f),
                    new SwingKeyframe(1f, 0f))
            }, RisingSlash),
            new ComboMove(RisingSlash, new SwingStep
            {
                // Third click: lead with the cutting edge and snap upward, then follow through.
                StartAngleDegrees = 75f,
                EndAngleDegrees = -115f,
                DurationTicks = 20,
                RecoveryTicks = 4,
                DamageStart = 0.22f,
                DamageEnd = 0.62f,
                MotionValue = 105f,
                ReverseBladeEdge = true,
                TrailEdgeSharpness = 110f,
                CollisionWidth = 18f,
                CollisionStartRatio = 0.16f,
                SoundAt = 0.24f,
                RotationCurve = new SwingCurve(
                    new SwingKeyframe(0f, 0f),
                    new SwingKeyframe(0.22f, 0.05f),
                    new SwingKeyframe(0.52f, 0.93f, SwingEasing.CubicOut),
                    new SwingKeyframe(1f, 1f)),
                ScaleCurve = new SwingCurve(
                    new SwingKeyframe(0f, 0.95f),
                    new SwingKeyframe(0.38f, 1.12f),
                    new SwingKeyframe(1f, 0.95f))
            }), // No continuations: this ends the combo and triggers EndCooldownTicks.
            new ComboMove(WideFinisher, new SwingStep
            {
                // Retained move for future branches; not part of the standard left-click combo.
                StartAngleDegrees = -140f,
                EndAngleDegrees = 135f,
                DurationTicks = 32,
                RecoveryTicks = 10,
                DamageStart = 0.32f,
                DamageEnd = 0.86f,
                MotionValue = 135f,
                CollisionWidth = 22f,
                CollisionStartRatio = 0.16f,
                SoundAt = 0.35f,
                RotationCurve = new SwingCurve(
                    new SwingKeyframe(0f, 0f),
                    new SwingKeyframe(0.3f, 0.06f),
                    new SwingKeyframe(0.76f, 0.97f, SwingEasing.SmootherStep),
                    new SwingKeyframe(1f, 1f)),
                ScaleCurve = new SwingCurve(
                    new SwingKeyframe(0f, 0.8f),
                    new SwingKeyframe(0.55f, 1.3f),
                    new SwingKeyframe(1f, 0.9f))
            }, OverheadSlash));
    }
}
