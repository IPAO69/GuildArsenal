using System;
using System.Collections.Generic;
using GuildArsenal.Content.Projectiles.Melee;

namespace GuildArsenal.Core.Combat
{
    /// <summary>A named attack and the explicit list of attacks allowed to follow it.</summary>
    public sealed class ComboMove
    {
        public string Id { get; }
        public SwingStep Swing { get; }
        public IReadOnlyList<string> NextMoves { get; }
        public bool IsTerminal => NextMoves.Count == 0;

        /// <param name="nextMoves">The first entry is the default continuation; additional entries are allowed branches.</param>
        public ComboMove(string id, SwingStep swing, params string[] nextMoves)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("A move needs a non-empty ID.", nameof(id));
            ArgumentNullException.ThrowIfNull(swing);
            ArgumentNullException.ThrowIfNull(nextMoves);
            swing.Validate();
            var unique = new HashSet<string>(StringComparer.Ordinal);
            foreach (string next in nextMoves)
            {
                if (string.IsNullOrWhiteSpace(next) || !unique.Add(next))
                    throw new ArgumentException("NextMoves must contain distinct, non-empty IDs.", nameof(nextMoves));
            }
            Id = id;
            Swing = swing;
            NextMoves = Array.AsReadOnly((string[])nextMoves.Clone());
        }
    }

    /// <summary>Immutable weapon-specific moves and transitions, shared by all players.</summary>
    public sealed class ComboDefinition
    {
        private readonly Dictionary<string, int> indices = new(StringComparer.Ordinal);
        private readonly IReadOnlyList<string> entryMoves;
        public string StartMoveId { get; }
        public int ResetTicks { get; }
        private int endCooldownTicks = 30;
        /// <summary>Fixed game ticks after a terminal move finishes, not scaled by attack speed.</summary>
        public int EndCooldownTicks
        {
            get => endCooldownTicks;
            init
            {
                if (value < 0)
                    throw new ArgumentOutOfRangeException(nameof(EndCooldownTicks));
                endCooldownTicks = value;
            }
        }
        public IReadOnlyList<ComboMove> Moves { get; }
        public IReadOnlyList<SwingStep> Steps { get; }

        public ComboDefinition(string startMoveId, int resetTicks, params ComboMove[] moves)
        {
            ArgumentNullException.ThrowIfNull(moves);
            if (string.IsNullOrWhiteSpace(startMoveId) || moves.Length == 0)
                throw new ArgumentException("A combo needs a start move and at least one move.", nameof(moves));
            if (resetTicks < 0)
                throw new ArgumentOutOfRangeException(nameof(resetTicks));

            var copy = (ComboMove[])moves.Clone();
            var steps = new SwingStep[copy.Length];
            for (int i = 0; i < copy.Length; i++)
            {
                ComboMove move = copy[i] ?? throw new ArgumentException("Moves cannot contain null.", nameof(moves));
                if (!indices.TryAdd(move.Id, i))
                    throw new ArgumentException($"Duplicate move ID: {move.Id}.", nameof(moves));
                steps[i] = move.Swing;
            }
            if (!indices.ContainsKey(startMoveId))
                throw new ArgumentException($"Start move '{startMoveId}' does not exist.", nameof(startMoveId));
            foreach (ComboMove move in copy)
            {
                foreach (string next in move.NextMoves)
                {
                    if (!indices.ContainsKey(next))
                        throw new ArgumentException($"Move '{move.Id}' links to unknown move '{next}'.", nameof(moves));
                }
            }

            StartMoveId = startMoveId;
            ResetTicks = resetTicks;
            Moves = Array.AsReadOnly(copy);
            Steps = Array.AsReadOnly(steps);
            entryMoves = Array.AsReadOnly(new[] { startMoveId });
        }

        public bool TryGetIndex(string moveId, out int index)
        {
            index = -1;
            return moveId != null && indices.TryGetValue(moveId, out index);
        }

        public ComboMove GetMove(string moveId) => TryGetIndex(moveId, out int index)
            ? Moves[index] : throw new ArgumentException($"Unknown move '{moveId}'.", nameof(moveId));

        public bool CanFollow(string previousMoveId, string nextMoveId)
        {
            if (!TryGetIndex(previousMoveId, out int index))
                return false;
            foreach (string next in Moves[index].NextMoves)
            {
                if (next == nextMoveId)
                    return true;
            }
            return false;
        }

        internal IReadOnlyList<string> EntryMoves => entryMoves;

        /// <summary>Compatibility for weapons that only supply the original sequential ComboSteps list.</summary>
        public static ComboDefinition CreateLinear(IReadOnlyList<SwingStep> steps, int resetTicks)
        {
            ArgumentNullException.ThrowIfNull(steps);
            if (steps.Count == 0)
                throw new ArgumentException("A combo needs at least one step.", nameof(steps));
            var moves = new ComboMove[steps.Count];
            for (int i = 0; i < steps.Count; i++)
                moves[i] = new ComboMove($"Step{i}", steps[i], $"Step{(i + 1) % steps.Count}");
            return new ComboDefinition("Step0", resetTicks, moves);
        }
    }

    /// <summary>One player's combo state. Only allowed transitions advance it; failed requests leave it unchanged.</summary>
    public sealed class ComboManager
    {
        public string CurrentMoveId { get; private set; }
        private bool hasActivity;
        private ulong lastActivity;
        private bool currentMoveFinished;
        private bool hasCooldown;
        private ulong cooldownStartedAt;
        private int cooldownTicks;

        public int GetRemainingCooldownTicks(ulong tick)
        {
            if (!hasCooldown)
                return 0;
            if (tick < cooldownStartedAt || tick - cooldownStartedAt >= (ulong)cooldownTicks)
            {
                hasCooldown = false;
                return 0;
            }
            return cooldownTicks - (int)(tick - cooldownStartedAt);
        }

        /// <summary>Call once after the attack AND its normal recovery complete. Interrupted moves do not start a cooldown.</summary>
        public bool CompleteMove(ComboDefinition definition, string moveId, ulong tick)
        {
            ArgumentNullException.ThrowIfNull(definition);
            if (currentMoveFinished || moveId != CurrentMoveId || !definition.TryGetIndex(moveId, out int index))
                return false;
            currentMoveFinished = true;
            Touch(tick);
            if (definition.Moves[index].IsTerminal && definition.EndCooldownTicks > 0)
            {
                hasCooldown = true;
                cooldownStartedAt = tick;
                cooldownTicks = definition.EndCooldownTicks;
            }
            return true;
        }

        /// <summary>A timeout/world-clock reset starts at the entry move; a terminal move starts a new combo.</summary>
        public IReadOnlyList<string> GetAvailableMoves(ComboDefinition definition, ulong tick)
        {
            ArgumentNullException.ThrowIfNull(definition);
            if (GetRemainingCooldownTicks(tick) > 0)
                return Array.Empty<string>();
            if (!hasActivity || tick < lastActivity || tick - lastActivity > (ulong)definition.ResetTicks
                || !definition.TryGetIndex(CurrentMoveId, out int index))
                return definition.EntryMoves;
            IReadOnlyList<string> next = definition.Moves[index].NextMoves;
            return next.Count == 0 ? definition.EntryMoves : next;
        }

        public bool TryBegin(ComboDefinition definition, ulong tick, out int moveIndex, string requestedMoveId = null)
        {
            IReadOnlyList<string> available = GetAvailableMoves(definition, tick);
            moveIndex = -1;
            if (available.Count == 0)
                return false;
            string requested = requestedMoveId ?? available[0];
            bool allowed = false;
            foreach (string candidate in available)
            {
                if (candidate == requested)
                {
                    allowed = true;
                    break;
                }
            }
            if (!allowed || !definition.TryGetIndex(requested, out moveIndex))
                return false;

            CurrentMoveId = requested;
            currentMoveFinished = false;
            hasCooldown = false;
            Touch(tick);
            return true;
        }

        public int Begin(ComboDefinition definition, ulong tick, string requestedMoveId = null)
        {
            if (!TryBegin(definition, tick, out int index, requestedMoveId))
                throw new InvalidOperationException($"Move '{requestedMoveId}' cannot follow '{CurrentMoveId}'.");
            return index;
        }

        public void Touch(ulong tick)
        {
            lastActivity = tick;
            hasActivity = true;
        }

        public void Reset()
        {
            CurrentMoveId = null;
            hasActivity = false;
            lastActivity = 0;
            currentMoveFinished = false;
            hasCooldown = false;
            cooldownStartedAt = 0;
            cooldownTicks = 0;
        }
    }
}
