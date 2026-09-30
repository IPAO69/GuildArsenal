using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace GuildArsenal.Primitive
{
    /// <summary>Newest sample first. Length includes the size at the time the pose was recorded.</summary>
    public readonly record struct BladeTrailSample(Vector2 Center, float Rotation, float Length);

    /// <summary>Cache the weapon pose relative to its owner so walking does not stretch the trail.</summary>
    public readonly record struct BladeTrailPose(Vector2 RelativeCenter, float Rotation, float Scale)
    {
        public static BladeTrailPose Capture(Vector2 worldCenter, Vector2 ownerCenter, float rotation, float scale)
            => new(worldCenter - ownerCenter, rotation, scale);

        public Vector2 GetWorldCenter(Vector2 ownerCenter) => ownerCenter + RelativeCenter;
        public BladeTrailSample ToSample(Vector2 ownerCenter, float bladeLength)
            => new(GetWorldCenter(ownerCenter), Rotation, bladeLength * Scale);
    }

    /// <summary>A shader-lit blade ribbon. Only CPU arrays are retained; there are no disposable GPU buffers.</summary>
    public sealed class SwingShaderTrail
    {
        private const int MaxSamples = 256;
        internal const int ThrustSamplesPerLane = 17;
        internal const float ThrustHeadFraction = 3f / 16f;
        private readonly VertexPositionColorTexture[] vertices = new VertexPositionColorTexture[MaxSamples * 2];
        private readonly short[] indices = new short[(MaxSamples - 1) * 6];

        public SwingShaderTrail()
        {
            for (int i = 0; i < MaxSamples - 1; i++)
            {
                int v = i * 2;
                int k = i * 6;
                indices[k] = (short)v;
                indices[k + 1] = (short)(v + 1);
                indices[k + 2] = (short)(v + 2);
                indices[k + 3] = (short)(v + 1);
                indices[k + 4] = (short)(v + 3);
                indices[k + 5] = (short)(v + 2);
            }
        }

        internal static void GetSlashEdges(BladeTrailSample sample, Vector2 screenPosition, float startRatio,
            out Vector2 inner, out Vector2 outer)
        {
            if (!float.IsFinite(startRatio) || startRatio < 0f || startRatio >= 1f)
                throw new ArgumentOutOfRangeException(nameof(startRatio));
            Vector2 direction = new(MathF.Cos(sample.Rotation), MathF.Sin(sample.Rotation));
            inner = sample.Center + direction * sample.Length * startRatio - screenPosition;
            outer = sample.Center + direction * sample.Length - screenPosition;
        }

        private void WriteSample(BladeTrailSample sample, float age, int index, float startRatio)
        {
            GetSlashEdges(sample, Main.screenPosition, startRatio, out Vector2 inner, out Vector2 outer);
            vertices[index * 2] = new VertexPositionColorTexture(new Vector3(inner, 0f),
                Color.White, new Vector2(age, 0f));
            vertices[index * 2 + 1] = new VertexPositionColorTexture(new Vector3(outer, 0f),
                Color.White, new Vector2(age, 1f));
        }

        internal static int BuildThrustRibbon(IReadOnlyList<BladeTrailSample> samples, Vector2 screenPosition,
            VertexPositionColorTexture[] destination)
        {
            if (samples.Count < 2)
                return 0;
            const int segments = ThrustSamplesPerLane - 1;
            if (destination.Length < ThrustSamplesPerLane * 2)
                throw new ArgumentException("Thrust ribbon needs at least 34 vertices.", nameof(destination));

            BladeTrailSample current = samples[0];
            Vector2 direction = new(MathF.Cos(current.Rotation), MathF.Sin(current.Rotation));
            Vector2 normal = new(-direction.Y, direction.X);
            Vector2 tip = current.Center + direction * current.Length;
            float farthest = 0f;
            float nearest = 0f;
            foreach (BladeTrailSample pose in samples)
            {
                Vector2 oldTip = pose.Center + new Vector2(MathF.Cos(pose.Rotation), MathF.Sin(pose.Rotation)) * pose.Length;
                float distance = Vector2.Dot(oldTip - tip, direction);
                farthest = Math.Max(farthest, distance);
                nearest = Math.Min(nearest, distance);
            }

            // Keep the furthest recent tip during retraction, instead of folding the ribbon over itself.
            // A single centered spear follows the blade axis, with its bright triangle beyond the tip.
            Vector2 head = tip + direction * (farthest + current.Length * 0.14f);
            float length = Math.Max(current.Length * 0.9f, farthest - nearest);
            float halfWidth = Math.Max(3f, current.Length * 0.09f);
            for (int i = 0; i <= segments; i++)
            {
                float age = i / (float)segments;
                Vector2 center = head - direction * (length * age) - screenPosition;
                // Linear tip-to-shoulder edges form an actual triangle, not a rounded spindle.
                float width = age <= ThrustHeadFraction ? halfWidth * age / ThrustHeadFraction
                    : halfWidth * MathF.Pow((1f - age) / (1f - ThrustHeadFraction), 1.2f);
                int vertex = i * 2;
                destination[vertex] = new VertexPositionColorTexture(new Vector3(center - normal * width, 0f),
                    Color.White, new Vector2(age, 0f));
                destination[vertex + 1] = new VertexPositionColorTexture(new Vector3(center + normal * width, 0f),
                    Color.White, new Vector2(age, 1f));
            }
            return segments + 1;
        }

        public bool Draw(IReadOnlyList<BladeTrailSample> samples, Effect effect, Color color, float opacity,
            float edgeSharpness, bool thrust, float startRatio = 0.16f)
        {
            if (Main.dedServ || samples.Count < 2)
                return false;
            if (opacity <= 0f)
                return true;

            int count = thrust ? BuildThrustRibbon(samples, Main.screenPosition, vertices) : 0;
            if (!thrust)
                WriteSample(samples[0], 0f, count++, startRatio);
            for (int i = 1; !thrust && i < samples.Count && count < MaxSamples; i++)
            {
                BladeTrailSample from = samples[i - 1];
                BladeTrailSample to = samples[i];
                // Subdivide fast upward cuts so the trail does not become a coarse polygon.
                int subdivisions = Math.Clamp((int)MathF.Ceiling(MathF.Abs(to.Rotation - from.Rotation)
                    / MathHelper.ToRadians(4f)), 1, 64);
                for (int j = 1; j <= subdivisions && count < MaxSamples; j++)
                {
                    float t = j / (float)subdivisions;
                    var pose = new BladeTrailSample(Vector2.Lerp(from.Center, to.Center, t),
                        MathHelper.Lerp(from.Rotation, to.Rotation, t), MathHelper.Lerp(from.Length, to.Length, t));
                    WriteSample(pose, (i - 1f + t) / (samples.Count - 1f), count++, startRatio);
                }
            }

            GraphicsDevice device = Main.instance.GraphicsDevice;
            Main.spriteBatch.End();
            BlendState previousBlend = device.BlendState;
            DepthStencilState previousDepth = device.DepthStencilState;
            RasterizerState previousRasterizer = device.RasterizerState;
            try
            {
                device.BlendState = BlendState.Additive;
                device.DepthStencilState = DepthStencilState.None;
                device.RasterizerState = RasterizerState.CullNone;
                Viewport viewport = device.Viewport;
                Matrix projection = Matrix.CreateOrthographicOffCenter(0f, viewport.Width, viewport.Height, 0f, -1f, 1f);
                effect.Parameters["uWorldViewProjection"].SetValue(Main.GameViewMatrix.TransformationMatrix * projection);
                effect.Parameters["uColor"].SetValue(color.ToVector3());
                effect.Parameters["uOpacity"].SetValue(Math.Clamp(opacity, 0f, 1f));
                effect.Parameters["uTime"].SetValue(Main.GlobalTimeWrappedHourly);
                effect.Parameters["uEdgeSharpness"].SetValue(edgeSharpness);
                effect.Parameters["uThrust"].SetValue(thrust ? 1f : 0f);
                foreach (EffectPass pass in effect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, vertices, 0,
                        count * 2, indices, 0, (count - 1) * 2);
                }
            }
            finally
            {
                device.BlendState = previousBlend;
                device.DepthStencilState = previousDepth;
                device.RasterizerState = previousRasterizer;
                Main.pixelShader.CurrentTechnique.Passes[0].Apply();
                Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                    DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            }
            return true;
        }
    }
}
