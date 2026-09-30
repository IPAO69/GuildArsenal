using System;
using GuildArsenal.Content.Projectiles.Melee;
using GuildArsenal.Core.Combat;
using GuildArsenal.Primitive;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

internal static class Program
{
    private static int checks;

    private static void Main()
    {
        var keys = new[]
        {
            new SwingKeyframe(1f, 0.7f, SwingEasing.Linear),
            new SwingKeyframe(0f, 0.6f),
            new SwingKeyframe(0.5f, 1.4f, SwingEasing.Linear)
        };
        var curve = new SwingCurve(keys);
        keys[0] = new SwingKeyframe(1f, 99f);
        Equal(curve.Evaluate(0f), 0.6f, "start scale");
        Equal(curve.Evaluate(0.25f), 1f, "windup scale interpolation");
        Equal(curve.Evaluate(0.5f), 1.4f, "peak scale");
        Equal(curve.Evaluate(0.75f), 1.05f, "downsizing interpolation");
        Equal(curve.Evaluate(1f), 0.7f, "final scale / immutable inputs");
        Equal(curve.Evaluate(-1f), 0.6f, "hold before first keyframe");
        Equal(curve.Evaluate(2f), 0.7f, "hold after last keyframe");
        Equal(SwingCurve.Constant(1.2f).Evaluate(0.3f), 1.2f, "constant curve");

        foreach (SwingEasing easing in Enum.GetValues<SwingEasing>())
        {
            Equal(SwingCurve.Ease(0f, easing), 0f, $"{easing} start");
            Equal(SwingCurve.Ease(1f, easing), 1f, $"{easing} end");
            float last = 0f;
            for (int i = 0; i <= 100; i++)
            {
                float value = SwingCurve.Ease(i / 100f, easing);
                Check(value >= last - 0.00001f && value >= 0f && value <= 1f, $"{easing} monotonic");
                last = value;
            }
        }

        var step = new SwingStep { StartAngleDegrees = -140f, EndAngleDegrees = 140f, RotationEasing = SwingEasing.Linear, ScaleCurve = curve };
        step.Validate();
        Equal(step.EvaluateAngleDegrees(0f), -140f, "start angle");
        Equal(step.EvaluateAngleDegrees(0.5f), 0f, "280-degree sweep keeps intended arc");
        Equal(step.EvaluateAngleDegrees(1f), 140f, "end angle");
        var reverse = new SwingStep { StartAngleDegrees = 140f, EndAngleDegrees = -140f, RotationEasing = SwingEasing.Linear };
        Equal(reverse.EvaluateAngleDegrees(0.5f), 0f, "reverse combo direction");
        Equal(new SwingStep { ScaleCurve = SwingCurve.Constant(-1f) }.EvaluateScale(0.5f), 0f, "negative scale clamps to zero");
        var anticipation = new SwingStep
        {
            StartAngleDegrees = 0f, EndAngleDegrees = 100f,
            RotationCurve = new SwingCurve(new SwingKeyframe(0f, 0f), new SwingKeyframe(0.2f, -0.2f), new SwingKeyframe(1f, 1f))
        };
        Equal(anticipation.EvaluateAngleDegrees(0.2f), -20f, "custom angle anticipation");

        Check(!step.TryGetDamageWindow(0f, 0.1f, out _, out _), "windup cannot hit");
        Check(step.TryGetDamageWindow(0.1f, 0.2f, out float activeStart, out float activeEnd), "entering damage window hits");
        Equal(activeStart, 0.15f, "sweep begins at exact damage boundary");
        Equal(activeEnd, 0.2f, "sweep ends at current progress");
        Check(!step.TryGetDamageWindow(0.9f, 1f, out _, out _), "end pose cannot hit");
        var narrow = new SwingStep { DamageStart = 0.501f, DamageEnd = 0.502f };
        Check(narrow.TryGetDamageWindow(0.5f, 0.6f, out activeStart, out activeEnd), "narrow damage window between frames is sampled");
        Equal(activeStart, 0.501f, "narrow window start");
        Equal(activeEnd, 0.502f, "narrow window end");

        Throws<ArgumentException>(() => new SwingCurve(), "empty curve rejected");
        Throws<ArgumentException>(() => new SwingCurve(new SwingKeyframe(0f, 1f), new SwingKeyframe(0f, 2f)), "duplicate times rejected");
        Throws<ArgumentException>(() => new SwingCurve(new SwingKeyframe(float.NaN, 1f)), "NaN time rejected");
        Throws<ArgumentException>(() => new SwingCurve(new SwingKeyframe(0f, float.PositiveInfinity)), "infinite value rejected");
        Throws<ArgumentOutOfRangeException>(() => curve.Evaluate(float.NaN), "NaN progress rejected");
        Throws<InvalidOperationException>(() => new SwingStep { DamageStart = 0.9f, DamageEnd = 0.1f }.Validate(), "inverted damage window rejected");
        Throws<InvalidOperationException>(() => new SwingStep { DurationTicks = 0 }.Validate(), "zero duration rejected");

        var linear = ComboDefinition.CreateLinear(new[] { new SwingStep(), new SwingStep(), new SwingStep() }, 90);
        var combo = new ComboManager();
        Equal(combo.Begin(linear, 100), 0, "combo opens at step zero");
        Equal(combo.Begin(linear, 110), 1, "second combo");
        Equal(combo.Begin(linear, 120), 2, "finisher combo");
        Equal(combo.Begin(linear, 130), 0, "combo wraps");
        Equal(combo.Begin(linear, 140), 1, "next cycle");
        Equal(combo.Begin(linear, 231), 0, "timeout resets combo");
        combo.Touch(400);
        Equal(combo.Begin(linear, 490), 1, "timeout begins after active swing; inclusive boundary");
        Equal(combo.Begin(linear, 1), 0, "world clock reset clears combo");
        var otherPlayer = new ComboManager();
        Equal(otherPlayer.Begin(linear, 2), 0, "player state is independent");
        Equal(combo.Begin(ComboDefinition.CreateLinear(new[] { new SwingStep() }, 90), 2), 0, "one-step combo");
        Throws<ArgumentException>(() => ComboDefinition.CreateLinear(Array.Empty<SwingStep>(), 90), "empty combo rejected");

        ComboDefinition sword = LongswordMove.Combo;
        Check(sword.Moves.Count == 4, "Long Sword retains its wide slash outside the three-hit left-click loop");
        var longSwordCombo = new ComboManager();
        Equal(longSwordCombo.Begin(sword, 0), 0, "Long Sword first click");
        longSwordCombo.Touch(30);
        Equal(longSwordCombo.Begin(sword, 35), 1, "Long Sword second click is Thrust");
        longSwordCombo.Touch(61);
        Equal(longSwordCombo.Begin(sword, 66), 2, "Long Sword third click is RisingSlash");
        longSwordCombo.Touch(108);
        Equal(longSwordCombo.Begin(sword, 113), 0, "Long Sword wraps after RisingSlash");
        longSwordCombo.Touch(143);
        Equal(longSwordCombo.Begin(sword, 234), 0, "Long Sword resets after inactivity");
        Check(sword.Steps[0].EndAngleDegrees > sword.Steps[0].StartAngleDegrees, "first attack cuts down");
        Check(sword.Steps[2].EndAngleDegrees < sword.Steps[2].StartAngleDegrees, "third attack cuts up");
        Check(sword.CanFollow(LongswordMove.OverheadSlash, LongswordMove.Thrust), "overhead-to-thrust link");
        Check(sword.CanFollow(LongswordMove.Thrust, LongswordMove.RisingSlash), "thrust-to-rising link");
        Check(sword.CanFollow(LongswordMove.RisingSlash, LongswordMove.OverheadSlash), "rising-to-overhead link");
        Check(!sword.CanFollow(LongswordMove.OverheadSlash, LongswordMove.RisingSlash), "cannot skip Thrust");
        Check(!sword.CanFollow(LongswordMove.RisingSlash, LongswordMove.WideFinisher), "wide slash is outside left-click loop");
        SwingStep thrust = sword.GetMove(LongswordMove.Thrust).Swing;
        SwingStep overhead = sword.GetMove(LongswordMove.OverheadSlash).Swing;
        SwingStep rising = sword.GetMove(LongswordMove.RisingSlash).Swing;
        Check(thrust.DurationTicks + thrust.RecoveryTicks < overhead.DurationTicks + overhead.RecoveryTicks
            && thrust.DurationTicks + thrust.RecoveryTicks < rising.DurationTicks + rising.RecoveryTicks, "Thrust is faster than both slashes");
        Check(thrust.MotionValue < overhead.MotionValue && thrust.MotionValue < rising.MotionValue,
            "Thrust is weaker than both slashes");
        Equal(overhead.MotionValue, 100f, "Overhead motion value");
        Equal(thrust.MotionValue, 65f, "Thrust motion value");
        Equal(rising.MotionValue, 105f, "Rising motion value");
        Equal(overhead.CalculateMotionDamage(38f), 38f, "MV 100 preserves weapon damage");
        Equal(thrust.CalculateMotionDamage(38f), 24.7f, "MV 65 scales weapon damage once");
        Equal(rising.CalculateMotionDamage(38f), 39.9f, "MV 105 scales weapon damage once");
        Equal(new SwingStep { MotionValue = 0f }.CalculateMotionDamage(38f), 0f, "zero MV");
        Equal(new SwingStep { MotionValue = 150f }.CalculateMotionDamage(38f), 57f, "MV over 100");
        Throws<InvalidOperationException>(() => new SwingStep { MotionValue = -1f }.Validate(), "negative MV rejected");
        Throws<InvalidOperationException>(() => new SwingStep { MotionValue = float.NaN }.Validate(), "NaN MV rejected");
        Throws<InvalidOperationException>(() => new SwingStep { MotionValue = float.PositiveInfinity }.Validate(), "infinite MV rejected");
        Throws<InvalidOperationException>(() => new SwingStep { TrailEdgeSharpness = 0f }.Validate(), "zero sharpness rejected");
        Check(rising.ReverseBladeEdge && !overhead.ReverseBladeEdge, "rising slash reverses cutting edge only");
        Check(rising.TrailEdgeSharpness > overhead.TrailEdgeSharpness, "rising slash has a sharper trail edge");
        Equal(rising.RotationCurve.Evaluate(0.22f), 0.05f, "rising slash holds its windup");
        Equal(rising.RotationCurve.Evaluate(0.52f), 0.93f, "rising slash finishes its fast cut by 52 percent");
        Check(rising.EvaluateAngleDegrees(0.4f) < 0f, "rising slash already cuts above aim during the snap");
        Equal(thrust.EvaluateHoldOffset(0f), -18f, "Thrust begins pulled back");
        Equal(thrust.EvaluateHoldOffset(0.18f), -22f, "Thrust windup retracts a little farther");
        Equal(thrust.EvaluateHoldOffset(0.45f), 42f, "Thrust extends forward");
        Equal(thrust.EvaluateHoldOffset(1f), 0f, "Thrust returns to the hand");
        Equal(new SwingStep { HoldOffset = 10f }.EvaluateHoldOffset(0.5f), 10f, "old static offsets remain compatible");
        Equal(new SwingStep { HoldOffset = 10f, HoldOffsetCurve = SwingCurve.Constant(5f) }.EvaluateHoldOffset(0.5f),
            15f, "animated offsets add to the base offset");
        for (int i = 0; i <= 100; i++)
        {
            float progress = i / 100f;
            Equal(thrust.EvaluateAngleDegrees(progress), 0f, "Thrust stays aimed forward throughout the attack");
            Equal(thrust.EvaluateScale(progress), 1f, "Thrust moves instead of stretching the sprite");
            Check(float.IsFinite(thrust.EvaluateHoldOffset(progress)), "Thrust position stays finite");
        }
        foreach (SwingStep attack in sword.Steps)
        {
            attack.Validate();
            Equal(attack.EvaluateAngleDegrees(0f), attack.StartAngleDegrees, "Long Sword configured start angle");
            Equal(attack.EvaluateAngleDegrees(1f), attack.EndAngleDegrees, "Long Sword configured end angle");
            Check(!attack.TryGetDamageWindow(0f, 0f, out _, out _), "Long Sword initial windup has no damage");
            Check(!attack.TryGetDamageWindow(1f, 1f, out _, out _), "Long Sword final pose has no damage");
            for (int i = 0; i <= 100; i++)
            {
                float progress = i / 100f;
                Check(float.IsFinite(attack.EvaluateAngleDegrees(progress)), "Long Sword angle stays finite");
                Check(attack.EvaluateScale(progress) > 0f, "Long Sword size stays positive");
            }
        }
        TestGraphTransitions();
        TestThrustTrail();
        Console.WriteLine($"PASS: {checks} swing curve/combo checks. Swing parent classes compiled against installed tModLoader.");
    }

    private static void TestThrustTrail()
    {
        var mesh = new VertexPositionColorTexture[34];
        // Include repeated poses (hold) and a reversal (withdrawal). The new mesh must not fold.
        foreach (float rotation in new[] { 0f, MathF.PI, MathF.PI / 2f, -0.7f })
        {
            var direction = new Vector2(MathF.Cos(rotation), MathF.Sin(rotation));
            var normal = new Vector2(-direction.Y, direction.X);
            var current = new Vector2(300f, 200f);
            var history = new[]
            {
                new BladeTrailSample(current, rotation, 140f),
                new BladeTrailSample(current + direction * 30f, rotation, 140f),
                new BladeTrailSample(current + direction * 30f, rotation, 140f),
                new BladeTrailSample(current - direction * 40f, rotation, 140f)
            };
            var camera = new Vector2(80f, 55f);
            int count = SwingShaderTrail.BuildThrustRibbon(history, camera, mesh);
            Equal(count, 17f, "Thrust has a non-degenerate longitudinal mesh");
            Vector2 head = new(mesh[0].Position.X, mesh[0].Position.Y);
            Equal(Vector2.Dot(head + camera - current, direction), 181.2f,
                "Thrust glow reaches beyond furthest recent tip when retracting");
            Equal(Vector3.Distance(mesh[0].Position, mesh[1].Position), 0f, "Thrust head tapers to a point");
            Equal(Vector3.Distance(mesh[32].Position, mesh[33].Position), 0f, "Thrust tail tapers to a point");
            float previous = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                var left = mesh[i * 2];
                var right = mesh[i * 2 + 1];
                Check(float.IsFinite(left.Position.X) && float.IsFinite(left.Position.Y)
                    && float.IsFinite(right.Position.X) && float.IsFinite(right.Position.Y), "Thrust mesh stays finite");
                Vector2 center = new((left.Position.X + right.Position.X) / 2f, (left.Position.Y + right.Position.Y) / 2f);
                float along = Vector2.Dot(center - head, direction);
                Check(along < previous, "Thrust vertices progress backward without folding");
                Equal(Vector2.Dot(center - head, normal), 0f, "Thrust trail follows locked aim at any facing");
                Equal(left.TextureCoordinate.X, i / 16f, "Thrust fade coordinates progress by distance");
                previous = along;
            }
            Check(Vector3.Distance(mesh[16].Position, mesh[17].Position) > 28f,
                "Thrust body is wide enough to glow outside the sword sprite");
        }
        Equal(SwingShaderTrail.BuildThrustRibbon(Array.Empty<BladeTrailSample>(), Vector2.Zero, mesh), 0f,
            "Empty thrust history produces no mesh");
        Throws<ArgumentException>(() => SwingShaderTrail.BuildThrustRibbon(new[]
        {
            new BladeTrailSample(Vector2.Zero, 0f, 100f), new BladeTrailSample(Vector2.Zero, 0f, 100f)
        }, Vector2.Zero, new VertexPositionColorTexture[2]), "Thrust rejects an undersized destination");
    }

    private static void TestGraphTransitions()
    {
        // Deliberately reordered nodes: behavior must depend on links, not array order.
        var links = new[] { "B", "C" };
        var graph = new ComboDefinition("A", 90,
            new ComboMove("C", new SwingStep()),
            new ComboMove("A", new SwingStep(), links),
            new ComboMove("B", new SwingStep(), "B"));
        links[0] = "C";
        var manager = new ComboManager();
        Check(manager.GetAvailableMoves(graph, 0).Count == 1, "only entry is allowed before a combo starts");
        Check(!manager.TryBegin(graph, 0, out _, "C"), "cannot open with a non-entry move");
        Check(manager.CurrentMoveId == null, "rejected opener preserves empty state");
        Equal(manager.Begin(graph, 0), 1, "entry lookup works independently of array position");
        Check(manager.GetAvailableMoves(graph, 1).Count == 2, "both branches exposed");
        Check(graph.CanFollow("A", "B") && graph.CanFollow("A", "C"), "definition has two allowed branches");
        Check(!graph.CanFollow("B", "A"), "links are directed");
        Check(!manager.TryBegin(graph, 10, out int invalidIndex, "A") && invalidIndex == -1, "illegal repeated move rejected");
        Check(manager.CurrentMoveId == "A", "rejected transition preserves current move");
        Equal(manager.Begin(graph, 20), 2, "first declared branch is the default; input array copied");
        Equal(manager.Begin(graph, 21, "B"), 2, "self-loop is allowed when explicitly declared");
        Check(!manager.TryBegin(graph, 100, out _, "A"), "entry is not an implicit escape from a nonterminal move");
        Equal(manager.Begin(graph, 112), 1, "invalid request does not extend timeout");
        Equal(manager.Begin(graph, 113, "C"), 0, "explicit branch selection");
        Check(manager.CurrentMoveId == "C", "named move survives index conversion used for sync");
        Equal(manager.Begin(graph, 114), 1, "terminal move starts a fresh combo");
        Check(!manager.TryBegin(graph, 115, out _, "Unknown"), "unknown requested move rejected");
        manager.Reset();
        Check(manager.CurrentMoveId == null, "manual reset clears state");
        Equal(manager.Begin(graph, 116), 1, "manual reset returns to entry");
        var secondWeapon = new ComboManager();
        Equal(secondWeapon.Begin(graph, 117), 1, "independent weapon manager starts at entry");
        Throws<InvalidOperationException>(() => manager.Begin(graph, 117, "A"), "Begin reports disallowed transitions");
        Throws<ArgumentException>(() => new ComboDefinition("Missing", 90, new ComboMove("A", new SwingStep())), "missing entry rejected");
        Throws<ArgumentException>(() => new ComboDefinition("A", 90, new ComboMove("A", new SwingStep(), "Missing")), "broken next-move link rejected");
        Throws<ArgumentException>(() => new ComboDefinition("A", 90, new ComboMove("A", new SwingStep()), new ComboMove("A", new SwingStep())), "duplicate IDs rejected");
        Throws<ArgumentException>(() => new ComboMove("A", new SwingStep(), "B", "B"), "duplicate next moves rejected");
        Throws<ArgumentOutOfRangeException>(() => new ComboDefinition("A", -1, new ComboMove("A", new SwingStep())), "negative timeout rejected");
        var rebuilt = ComboDefinition.CreateLinear(new[] { new SwingStep(), new SwingStep(), new SwingStep() }, 90);
        var fallback = new ComboManager();
        Equal(fallback.Begin(rebuilt, 0), 0, "legacy sequential opener");
        Equal(fallback.Begin(ComboDefinition.CreateLinear(new[] { new SwingStep(), new SwingStep(), new SwingStep() }, 90), 1), 1,
            "rebuilding legacy definitions does not reset combo state");
    }

    private static void Equal(float actual, float expected, string name) => Check(MathF.Abs(actual - expected) < 0.0001f, name);
    private static void Check(bool value, string name)
    {
        checks++;
        if (!value) throw new Exception($"Failed: {name}");
    }
    private static void Throws<T>(Action action, string name) where T : Exception
    {
        try { action(); }
        catch (T) { checks++; return; }
        throw new Exception($"Failed: {name}");
    }
}
