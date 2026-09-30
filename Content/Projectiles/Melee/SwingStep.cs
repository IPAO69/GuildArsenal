using System;

namespace GuildArsenal.Content.Projectiles.Melee
{
    public enum SwingEasing
    {
        Linear,
        SmoothStep,
        SmootherStep,
        SineInOut,
        CubicIn,
        CubicOut
    }

    /// <summary>Time is normalized (0..1). Easing controls the transition INTO this keyframe.</summary>
    public readonly record struct SwingKeyframe(float Time, float Value, SwingEasing Easing = SwingEasing.SmoothStep);

    /// <summary>An immutable curve. Values outside the first/last keyframe hold their nearest value.</summary>
    public sealed class SwingCurve
    {
        private readonly SwingKeyframe[] keyframes;

        public static SwingCurve Constant(float value) => new(new SwingKeyframe(0f, value));

        public SwingCurve(params SwingKeyframe[] keyframes)
        {
            ArgumentNullException.ThrowIfNull(keyframes);
            if (keyframes.Length == 0)
                throw new ArgumentException("A swing curve needs at least one keyframe.", nameof(keyframes));

            this.keyframes = (SwingKeyframe[])keyframes.Clone();
            Array.Sort(this.keyframes, (left, right) => left.Time.CompareTo(right.Time));
            for (int i = 0; i < this.keyframes.Length; i++)
            {
                SwingKeyframe frame = this.keyframes[i];
                if (!float.IsFinite(frame.Time) || frame.Time < 0f || frame.Time > 1f || !float.IsFinite(frame.Value))
                    throw new ArgumentException("Keyframe times must be within 0..1, and values must be finite.", nameof(keyframes));
                if (!Enum.IsDefined(frame.Easing))
                    throw new ArgumentException("Unknown keyframe easing.", nameof(keyframes));
                if (i > 0 && frame.Time == this.keyframes[i - 1].Time)
                    throw new ArgumentException("Keyframe times must be unique.", nameof(keyframes));
            }
        }

        public float Evaluate(float progress)
        {
            if (!float.IsFinite(progress))
                throw new ArgumentOutOfRangeException(nameof(progress));
            if (progress <= keyframes[0].Time)
                return keyframes[0].Value;

            for (int i = 1; i < keyframes.Length; i++)
            {
                SwingKeyframe next = keyframes[i];
                if (progress > next.Time)
                    continue;

                SwingKeyframe previous = keyframes[i - 1];
                float t = Ease((progress - previous.Time) / (next.Time - previous.Time), next.Easing);
                return previous.Value + (next.Value - previous.Value) * t;
            }
            return keyframes[^1].Value;
        }

        public static float Ease(float progress, SwingEasing easing)
        {
            float t = Math.Clamp(progress, 0f, 1f);
            return easing switch
            {
                SwingEasing.Linear => t,
                SwingEasing.SmoothStep => t * t * (3f - 2f * t),
                SwingEasing.SmootherStep => t * t * t * (t * (6f * t - 15f) + 10f),
                SwingEasing.SineInOut => (1f - MathF.Cos(MathF.PI * t)) * 0.5f,
                SwingEasing.CubicIn => t * t * t,
                SwingEasing.CubicOut => 1f - MathF.Pow(1f - t, 3f),
                _ => throw new ArgumentOutOfRangeException(nameof(easing))
            };
        }
    }

    /// <summary>The terminal hold overlaps the combo cooldown; it is not another recovery delay.</summary>
    internal readonly record struct SwingTiming(int SwingTicks, int RecoveryTicks, int ComboRecoveryTicks)
    {
        public float NormalEnd => (float)SwingTicks + RecoveryTicks;
        public float TotalEnd => NormalEnd + ComboRecoveryTicks;
        public float SwingProgress(float elapsed) => Math.Clamp(elapsed / SwingTicks, 0f, 1f);
        public bool IsSwinging(float elapsed) => elapsed <= SwingTicks;
        public bool IsComboRecovery(float elapsed) => ComboRecoveryTicks > 0 && elapsed > NormalEnd && elapsed <= TotalEnd;
        public bool HasExpired(float elapsed) => elapsed > TotalEnd;
        public float ComboRecoveryProgress(float elapsed) => Math.Clamp((elapsed - NormalEnd) / Math.Max(1, ComboRecoveryTicks), 0f, 1f);
    }

    /// <summary>One attack in a combo. Angles are degrees relative to the locked aim direction.</summary>
    public sealed class SwingStep
    {
        public float StartAngleDegrees { get; init; } = -110f;
        public float EndAngleDegrees { get; init; } = 110f;

        /// <summary>Null uses the weapon's useTime. Both durations are divided by melee attack speed.</summary>
        public int? DurationTicks { get; init; }
        public int RecoveryTicks { get; init; } = 6;
        public bool ScaleTimingWithAttackSpeed { get; init; } = true;
        public SwingEasing RotationEasing { get; init; } = SwingEasing.SmootherStep;

        /// <summary>Optional angle interpolation curve: 0 is StartAngle, 1 is EndAngle; overshoot is allowed.</summary>
        public SwingCurve RotationCurve { get; init; }
        public SwingCurve ScaleCurve { get; init; } = SwingCurve.Constant(1f);

        /// <summary>Normalized damage window. Windup/recovery can be harmless while the weapon is visible.</summary>
        public float DamageStart { get; init; } = 0.15f;
        public float DamageEnd { get; init; } = 0.85f;
        /// <summary>Attack damage = weapon damage * (MotionValue / 100), before defense/critical hits.</summary>
        public float MotionValue { get; init; } = 100f;
        public float DamageFactor => MotionValue / 100f;
        public float CalculateMotionDamage(float weaponDamage) => weaponDamage * DamageFactor;

        /// <summary>Mirror the blade across its length so the cutting edge leads a reversed slash.</summary>
        public bool ReverseBladeEdge { get; init; }
        /// <summary>Higher values make the shader's bright outer edge thinner and sharper.</summary>
        public float TrailEdgeSharpness { get; init; } = 48f;
        /// <summary>Radial start of the slash shader: 0.16 leaves the hilt clear and covers most of the blade.</summary>
        public float TrailStartRatio { get; init; } = 0.16f;

        /// <summary>Blade thickness in pixels before scaling; origin-to-tip length is set on the projectile.</summary>
        public float CollisionWidth { get; init; } = 18f;
        public float CollisionStartRatio { get; init; } = 0.1f;
        public float HoldOffset { get; init; }
        /// <summary>Optional animated displacement along the blade direction, added to HoldOffset before scaling.</summary>
        public SwingCurve HoldOffsetCurve { get; init; }
        public float SoundAt { get; init; } = 0.2f;

        public float EvaluateAngleDegrees(float progress)
        {
            float t = RotationCurve?.Evaluate(progress) ?? SwingCurve.Ease(progress, RotationEasing);
            // Do not use AngleLerp: angles may deliberately sweep farther than 180 degrees.
            return StartAngleDegrees + (EndAngleDegrees - StartAngleDegrees) * t;
        }

        public float EvaluateScale(float progress) => Math.Max(0f, ScaleCurve.Evaluate(progress));
        public float EvaluateHoldOffset(float progress) => HoldOffset + (HoldOffsetCurve?.Evaluate(progress) ?? 0f);

        /// <summary>Clips a frame's progress interval to the damage window, including very narrow windows.</summary>
        public bool TryGetDamageWindow(float previousProgress, float progress, out float start, out float end)
        {
            start = Math.Max(previousProgress, DamageStart);
            end = Math.Min(progress, DamageEnd);
            return start <= end;
        }

        internal void Validate()
        {
            if (!float.IsFinite(StartAngleDegrees) || !float.IsFinite(EndAngleDegrees)
                || DurationTicks is <= 0 || RecoveryTicks < 0 || ScaleCurve == null
                || !Enum.IsDefined(RotationEasing)
                || !float.IsFinite(DamageStart) || !float.IsFinite(DamageEnd)
                || DamageStart < 0f || DamageEnd > 1f || DamageStart > DamageEnd
                || !float.IsFinite(MotionValue) || MotionValue < 0f
                || !float.IsFinite(TrailEdgeSharpness) || TrailEdgeSharpness <= 0f
                || !float.IsFinite(TrailStartRatio) || TrailStartRatio < 0f || TrailStartRatio >= 1f
                || !float.IsFinite(CollisionWidth) || CollisionWidth <= 0f
                || !float.IsFinite(CollisionStartRatio) || CollisionStartRatio < 0f || CollisionStartRatio > 1f
                || !float.IsFinite(HoldOffset) || !float.IsFinite(SoundAt) || SoundAt < 0f || SoundAt > 1f)
                throw new InvalidOperationException("Invalid SwingStep: check angles, durations, curves, damage window and collision settings.");
        }
    }
}
