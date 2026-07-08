using System;
using System.Collections.Generic;
using Faolline.GraphCore;
using Faolline.GraphStandard;
using Faolline.GraphLocalization;

namespace Faolline.GraphQuest
{
    /// <summary>
    /// Derives a <see cref="QuestGraph"/>'s objective and quest <see cref="QuestState"/>s from a shared
    /// <see cref="BaseContext"/>. Wraps graphstandard's <see cref="ReactiveEvaluator"/> for prerequisite gating
    /// (Locked/Active/Completed) and overlays the quest domain: completion is driven by each objective's
    /// <see cref="ObjectiveNodeData.CompletionCondition"/>; an optional <see cref="ObjectiveNodeData.FailCondition"/>
    /// gives the fourth <see cref="QuestState.Failed"/> state (fail precedes complete); rewards fire exactly once;
    /// the quest aggregates per <see cref="QuestCompletionRule"/>.
    /// <para>
    /// Call <see cref="Evaluate"/> after mutating the context (the same contract as
    /// <see cref="ReactiveEvaluator.MarkCompleted"/>). All state lives in context collections, so it persists and
    /// restores through a graphsave context snapshot, and the context may be a host's (e.g. a GameFlowContext).
    /// </para>
    /// </summary>
    public sealed class QuestEvaluator
    {
        private readonly QuestGraph _quest;
        private readonly BaseContext _context;
        private readonly ReactiveEvaluator _reactive;
        private readonly string _questId;
        private readonly string _completedKey;
        private readonly string _failedKey;
        private readonly string _rewardedKey;
        private readonly string _abandonedKey;

        private readonly Dictionary<string, QuestState> _lastObjectiveStates = new Dictionary<string, QuestState>(StringComparer.Ordinal);
        private QuestState _lastQuestState;
        private bool _questStateKnown;
        private float _lastNow;
        private ILocalizationProvider _localization;

        /// <summary>Raised when an objective changes state: <c>(objectiveId, newState)</c>.</summary>
        public event Action<string, QuestState> OnObjectiveStateChanged;

        /// <summary>Raised when the aggregated quest state changes.</summary>
        public event Action<QuestState> OnQuestStateChanged;

        /// <summary>Raised when a reward fires: the objective id, or the quest id for the quest completion reward.</summary>
        public event Action<string> OnRewardFired;

        /// <summary>Builds an evaluator over <paramref name="quest"/> against <paramref name="context"/> (may be a host's).
        /// Localization is auto-wired from <see cref="LocalizationContext.Current"/> when available;
        /// call <see cref="UseLocalization"/> to override or pass null to disable.</summary>
        public QuestEvaluator(QuestGraph quest, BaseContext context)
        {
            _quest = quest;
            _context = context;
            var ctx = LocalizationContext.Current;
            if (ctx?.Provider != null) _localization = ctx.Provider;
            // Scope by the effective quest id (explicit QuestId, else the graph's stable GraphId) — the SAME
            // resolution the localization adapter uses, so emitted and queried keys never drift.
            _questId = quest == null ? string.Empty : quest.ResolveQuestId();
            _completedKey = QuestContextKeys.CompletedSet(_questId);
            _failedKey = QuestContextKeys.FailedSet(_questId);
            _rewardedKey = QuestContextKeys.RewardedSet(_questId);
            _abandonedKey = QuestContextKeys.AbandonedSet(_questId);

            // Surface each objective's k-of-N prerequisite gate to the engine (unlisted ⇒ all-of-N / AND).
            Dictionary<string, int> requiredCounts = null;
            if (quest != null)
                foreach (var node in quest.Nodes)
                    if (node is ObjectiveNodeData obj && !string.IsNullOrEmpty(obj.Id) && obj.RequiredPrerequisiteCount >= 0)
                    {
                        (requiredCounts ??= new Dictionary<string, int>(StringComparer.Ordinal))[obj.Id] = obj.RequiredPrerequisiteCount;

                        // k-of-N footgun: a count larger than the number of prerequisites can NEVER be reached, so
                        // the objective stays Locked forever. Warn instead of failing silently (dogfood finding).
                        int prereqCount = 0;
                        foreach (var e in quest.Edges) if (e != null && e.ToNodeId == obj.Id) prereqCount++;
                        if (obj.RequiredPrerequisiteCount > prereqCount)
                            UnityEngine.Debug.LogWarning(
                                $"[GraphQuest] Objective '{obj.Id}' requires at least {obj.RequiredPrerequisiteCount} of " +
                                $"only {prereqCount} prerequisite(s) — it can never unlock (stays Locked). Lower the " +
                                $"count or add prerequisites.");
                    }

            _reactive = new ReactiveEvaluator(quest, context, _completedKey, requiredCounts);
        }

        /// <summary>
        /// One derivation pass: records newly-failed/-completed objectives into the context, fires one-shot
        /// rewards on completed transitions, and raises change events. Idempotent — a pass with an unchanged
        /// context produces no transitions and no duplicate events. Objective time limits are NOT checked
        /// (use <see cref="Evaluate(float)"/> with a clock for that).
        /// </summary>
        public void Evaluate() => Run(0f, useTime: false);

        /// <summary>
        /// Like <see cref="Evaluate()"/>, but with a game-time clock <paramref name="now"/> so time-limited
        /// objectives are enforced: an Active objective with a <see cref="ObjectiveNodeData.TimeLimitSeconds"/>
        /// records its deadline the first time it is seen, and Fails once <paramref name="now"/> reaches it (call
        /// each tick with your running game time, e.g. <c>Time.time</c>). Completing before the deadline still wins.
        /// Time limits are enforced ONLY on this overload — a host that only calls <see cref="Evaluate()"/> never
        /// times anything out.
        /// <para>
        /// The deadline is stored as an ABSOLUTE time (<c>now + limit</c>) in the context, so it persists through a
        /// save snapshot — but it is tied to the clock you pass. If that clock resets between sessions (e.g.
        /// <c>Time.time</c> restarts at 0), a timer saved mid-countdown reads leniently after a reload until the
        /// next tick.
        /// <b>Recommended pattern</b>: checkpoint BEFORE a timed objective arms — a save taken mid-countdown then
        /// effectively restarts the timed challenge on reload (the deadline re-arms against the new clock), the same
        /// way you would not save in the middle of a cutscene. Only pass a monotonic / persisted playtime (so the
        /// elapsed time is preserved across the save) if a game genuinely needs faithful mid-countdown persistence.
        /// </para>
        /// </summary>
        public void Evaluate(float now) => Run(now, useTime: true);

        private void Run(float now, bool useTime)
        {
            if (_quest == null || _context == null) return;
            if (useTime) _lastNow = now;

            if (IsAbandoned)
            {
                RaiseQuest(QuestState.Abandoned);
                return;
            }

            // Quest gate: while the unlock condition is unmet, everything is Locked.
            if (!QuestUnlocked())
            {
                foreach (var obj in Objectives())
                    RaiseObjective(obj.Id, QuestState.Locked);
                RaiseQuest(QuestState.Locked);
                SyncQuestCompletionMarker(QuestState.Locked);
                return;
            }

            // Sync the engine to the current completed-set, then record fail/completion for available objectives.
            _reactive.Reevaluate();
            foreach (var obj in Objectives())
            {
                if (_context.CollectionContains(_failedKey, obj.Id)) continue;
                if (_context.CollectionContains(_completedKey, obj.Id)) continue;
                if (_reactive.GetState(obj.Id) != ReactiveNodeState.Available) continue;

                // Explicit fail precedes completion.
                if (obj.FailCondition != null && obj.FailCondition.Evaluate(_context))
                {
                    _context.AddToCollection(_failedKey, obj.Id);
                    continue;
                }
                // Completion is checked before timer expiry (completing on the deadline tick still succeeds).
                if (obj.CompletionCondition != null && obj.CompletionCondition.Evaluate(_context))
                {
                    _reactive.MarkCompleted(obj.Id);
                    continue;
                }
                // Time limit: arm a deadline the first time the objective is Active, then fail once it passes.
                if (useTime && obj.TimeLimitSeconds > 0f)
                {
                    var dKey = QuestContextKeys.DeadlineKey(_questId, obj.Id);
                    if (!_context.TryGet<float>(dKey, out var deadline) || float.IsNaN(deadline))
                    {
                        deadline = now + obj.TimeLimitSeconds;
                        _context.Set<float>(dKey, deadline);
                    }
                    if (now >= deadline)
                        _context.AddToCollection(_failedKey, obj.Id);
                }
            }

            // Re-derive, emit states, fire objective rewards.
            _reactive.Reevaluate();
            foreach (var obj in Objectives())
            {
                var state = GetObjectiveState(obj.Id);
                RaiseObjective(obj.Id, state);
                if (state == QuestState.Completed && obj.Reward != null)
                    FireReward(obj.Id, obj.Id, obj.Reward);
            }

            // Quest aggregate + quest reward.
            var questState = ComputeQuestState();
            RaiseQuest(questState);
            SyncQuestCompletionMarker(questState);
            if (questState == QuestState.Completed && _quest.CompletionReward != null)
                FireReward(QuestContextKeys.QuestRewardMarker, _questId, _quest.CompletionReward);
        }

        // Keeps this quest's id in/out of the shared CompletedQuests set so other quests can chain on it
        // (cross-quest gating). Derived from the current state, so it reverts on a context revert (replay-safe).
        private void SyncQuestCompletionMarker(QuestState state)
        {
            if (string.IsNullOrEmpty(_questId) || _context == null) return;
            bool done = state == QuestState.Completed;
            bool inSet = _context.CollectionContains(QuestContextKeys.CompletedQuests, _questId);
            if (done && !inSet) _context.AddToCollection(QuestContextKeys.CompletedQuests, _questId);
            else if (!done && inSet) _context.RemoveFromCollection(QuestContextKeys.CompletedQuests, _questId);
        }

        /// <summary>
        /// Abandons the quest (player-initiated drop). The quest transitions to <see cref="QuestState.Abandoned"/>
        /// and no further evaluation will change its state until <see cref="Reset"/> is called. Distinct from
        /// <see cref="QuestState.Failed"/> (condition-driven) and <see cref="Reset"/> (replay).
        /// </summary>
        public void Abandon()
        {
            if (_context == null) return;
            _context.AddToCollection(_abandonedKey, _questId);
            _context.RemoveFromCollection(QuestContextKeys.CompletedQuests, _questId);
            var state = QuestState.Abandoned;
            RaiseQuest(state);
            foreach (var obj in Objectives())
                RaiseObjective(obj.Id, state);
        }

        /// <summary>True when the quest has been explicitly abandoned by the player.</summary>
        public bool IsAbandoned => _context != null && _context.CollectionContains(_abandonedKey, _questId);

        /// <summary>
        /// Clears this quest's progress for replay: empties its completed / failed / rewarded context sets (only
        /// the keys scoped to this quest's id — other quests sharing the context are untouched) and re-derives, so
        /// every objective returns to Locked/Active and one-shot rewards can fire again. The consumer needs no
        /// knowledge of the internal key scoping.
        /// <para>
        /// <b>Rewinds this quest's own bookkeeping ONLY.</b> It does not clear the shared context values that
        /// completion/fail conditions READ (e.g. a <c>"boss_defeated"</c> flag set by gameplay) — the library
        /// can't know which keys feed an arbitrary <see cref="BaseCondition"/>. So if those world values still
        /// hold, the next <see cref="Evaluate()"/> re-completes the objective immediately. For a full replay, the
        /// consumer must also reset its own world inputs (the keys its conditions test).
        /// </para>
        /// </summary>
        public void Reset()
        {
            if (_context == null) return;
            _context.ClearCollection(_completedKey);
            _context.ClearCollection(_failedKey);
            _context.ClearCollection(_rewardedKey);
            _context.ClearCollection(_abandonedKey);
            _context.RemoveFromCollection(QuestContextKeys.CompletedQuests, _questId);
            // Disarm any timer deadlines so they re-arm fresh on the next Evaluate(now).
            if (_quest != null)
                foreach (var obj in Objectives())
                    if (obj.TimeLimitSeconds > 0f)
                        _context.Set<float>(QuestContextKeys.DeadlineKey(_questId, obj.Id), float.NaN);
            _lastObjectiveStates.Clear();
            _questStateKnown = false;
            _reactive.Reevaluate();
        }

        /// <summary>
        /// Rewinds a SINGLE objective for replay/retry — the granular counterpart to <see cref="Reset"/>. Clears
        /// <paramref name="objectiveId"/> from this quest's completed/failed/rewarded sets and disarms its time
        /// limit (so a timed objective re-arms fresh on the next <see cref="Evaluate(float)"/>), then re-derives.
        /// The objective returns to Active/Locked and its one-shot reward can fire again. Other objectives and
        /// other quests sharing the context are untouched. No-op for a null/empty id.
        /// <para>
        /// Like <see cref="Reset"/>, this rewinds the quest's OWN bookkeeping only — not the world values the
        /// completion/fail conditions read. If those still hold, the next <see cref="Evaluate()"/> re-completes
        /// the objective; reset the relevant world inputs too for a genuine retry. When
        /// <see cref="EnableAutoEvaluate"/> is active the collection changes here re-derive automatically;
        /// otherwise call <see cref="Evaluate()"/> to emit the updated states.
        /// </para>
        /// </summary>
        public void ResetObjective(string objectiveId)
        {
            if (_context == null || string.IsNullOrEmpty(objectiveId)) return;
            _context.RemoveFromCollection(_completedKey, objectiveId);
            _context.RemoveFromCollection(_failedKey, objectiveId);
            _context.RemoveFromCollection(_rewardedKey, objectiveId);

            var obj = FindObjective(objectiveId);
            if (obj != null && obj.TimeLimitSeconds > 0f)
                _context.Set<float>(QuestContextKeys.DeadlineKey(_questId, objectiveId), float.NaN);

            _lastObjectiveStates.Remove(objectiveId);
            _questStateKnown = false;   // un-completing an objective can change the quest aggregate
            _reactive.Reevaluate();
        }

        /// <summary>The current derived state of <paramref name="objectiveId"/>.</summary>
        public QuestState GetObjectiveState(string objectiveId)
        {
            if (string.IsNullOrEmpty(objectiveId) || _context == null) return QuestState.Locked;
            if (!QuestUnlocked()) return QuestState.Locked;
            if (_context.CollectionContains(_failedKey, objectiveId)) return QuestState.Failed;
            switch (_reactive.GetState(objectiveId))
            {
                case ReactiveNodeState.Completed: return QuestState.Completed;
                case ReactiveNodeState.Available: return QuestState.Active;
                default: return QuestState.Locked;
            }
        }

        /// <summary>The current aggregated quest state.</summary>
        public QuestState State
        {
            get
            {
                if (_quest == null || _context == null) return QuestState.Locked;
                if (IsAbandoned) return QuestState.Abandoned;
                if (!QuestUnlocked()) return QuestState.Locked;
                return ComputeQuestState();
            }
        }

        /// <summary>The ids of all objectives currently <see cref="QuestState.Active"/>.</summary>
        public IReadOnlyCollection<string> ActiveObjectiveIds => CollectByState(QuestState.Active);

        /// <summary>The ids of all objectives currently <see cref="QuestState.Completed"/>.</summary>
        public IReadOnlyCollection<string> CompletedObjectiveIds => CollectByState(QuestState.Completed);

        // ── Journal data layer (for a consumer quest-log UI) ──────────────────

        /// <summary>
        /// Enables localization: journal text (quest/objective names and descriptions) is resolved through
        /// deterministic keys (<see cref="QuestLocalizationKeys"/>) via <paramref name="provider"/>, falling
        /// back to the authored text when a key is missing. Pass null to go back to literals.
        /// </summary>
        public QuestEvaluator UseLocalization(ILocalizationProvider provider)
        {
            _localization = provider;
            return this;
        }

        /// <summary>The quest's display title (falls back to its id), localized when a provider is set. Empty when no quest.</summary>
        public string DisplayName => _quest != null
            ? ResolveWithFallback(QuestLocalizationKeys.ForQuest(_questId), _quest.DisplayName)
            : string.Empty;

        /// <summary>The quest's longer description, localized when a provider is set. Empty when none / no quest.</summary>
        public string Description => _quest != null
            ? ResolveWithFallback(QuestLocalizationKeys.ForQuestDescription(_questId), _quest.Description)
            : string.Empty;

        /// <summary>How many required objectives are currently <see cref="QuestState.Completed"/> (a progress numerator).</summary>
        public int RequiredCompleted => CountRequired(onlyCompleted: true);

        /// <summary>The total number of required objectives (a progress denominator).</summary>
        public int RequiredTotal => CountRequired(onlyCompleted: false);

        /// <summary>
        /// A read-only snapshot of every objective (in graph order) with its label, description, required flag, and
        /// current state — everything a quest-log UI needs, so the consumer keeps no id→label table of its own.
        /// </summary>
        public IReadOnlyList<ObjectiveView> GetObjectives()
        {
            var list = new List<ObjectiveView>();
            if (_quest == null) return list;
            foreach (var obj in Objectives())
            {
                int progress = 0, progressTarget = 0;
                if (!string.IsNullOrEmpty(obj.ProgressCollectionKey) && obj.ProgressTarget > 0)
                {
                    progress = _context != null ? _context.CollectionCount(obj.ProgressCollectionKey) : 0;
                    progressTarget = obj.ProgressTarget;
                }
                list.Add(new ObjectiveView(
                    obj.Id,
                    ResolveWithFallback(QuestLocalizationKeys.ForObjective(obj.Id),
                        string.IsNullOrEmpty(obj.Title) ? obj.Id : obj.Title),
                    ResolveWithFallback(QuestLocalizationKeys.ForObjectiveDescription(obj.Id), obj.Description),
                    obj.Required,
                    GetObjectiveState(obj.Id),
                    progress,
                    progressTarget,
                    obj.IsHideObjective));
            }
            return list;
        }

        /// <summary>
        /// Seconds left before <paramref name="objectiveId"/> times out, as of the last <see cref="Evaluate(float)"/>:
        /// its full limit before the timer is armed, the live countdown while ticking, 0 once expired, and
        /// <see cref="float.PositiveInfinity"/> for an objective with no time limit. It reads the clock from the
        /// last <see cref="Evaluate(float)"/> call, so right after a restore (before the first tick) it is stale —
        /// call <see cref="Evaluate(float)"/> once to refresh it.
        /// </summary>
        public float GetRemainingSeconds(string objectiveId)
        {
            var obj = FindObjective(objectiveId);
            if (obj == null || obj.TimeLimitSeconds <= 0f) return float.PositiveInfinity;
            if (_context != null
                && _context.TryGet<float>(QuestContextKeys.DeadlineKey(_questId, objectiveId), out var deadline)
                && !float.IsNaN(deadline))
                return Math.Max(0f, deadline - _lastNow);
            return obj.TimeLimitSeconds;
        }

        // Resolves a deterministic key through the provider; falls back to the authored text when no
        // provider is set or the key resolves to ITS OWN "#key" missing-translation marker. Compares the
        // exact marker for THIS key rather than a bare StartsWith("#") — a genuine translation that happens
        // to start with '#' (a hashtag, "#1 Hunter", a room number) must not be mistaken for a missing key.
        private string ResolveWithFallback(string key, string authoredText)
        {
            if (_localization == null || string.IsNullOrEmpty(key)) return authoredText;
            var resolved = _localization.Resolve(key, _localization.CurrentLocale);
            if (string.IsNullOrEmpty(resolved) || resolved == $"#{key}") return authoredText;
            return resolved;
        }

        private ObjectiveNodeData FindObjective(string id)
        {
            if (_quest == null || string.IsNullOrEmpty(id)) return null;
            foreach (var obj in Objectives())
                if (obj.Id == id) return obj;
            return null;
        }

        private int CountRequired(bool onlyCompleted)
        {
            if (_quest == null || _context == null) return 0;
            int n = 0;
            foreach (var obj in Objectives())
            {
                if (!obj.Required) continue;
                if (onlyCompleted && GetObjectiveState(obj.Id) != QuestState.Completed) continue;
                n++;
            }
            return n;
        }

        // ── Internals ─────────────────────────────────────────────────────────

        private bool QuestUnlocked()
            => _quest.UnlockCondition == null || _quest.UnlockCondition.Evaluate(_context);

        private IEnumerable<ObjectiveNodeData> Objectives()
        {
            foreach (var node in _quest.Nodes)
                if (node is ObjectiveNodeData obj && !string.IsNullOrEmpty(obj.Id))
                    yield return obj;
        }

        private QuestState ComputeQuestState()
        {
            int requiredCount = 0;
            int completedCount = 0;
            int failedCount = 0;
            foreach (var obj in Objectives())
            {
                if (!obj.Required) continue;
                requiredCount++;
                var s = GetObjectiveState(obj.Id);
                if (s == QuestState.Completed) completedCount++;
                else if (s == QuestState.Failed) failedCount++;
            }

            switch (_quest.CompletionRule)
            {
                case QuestCompletionRule.AnyRequired:
                    if (completedCount > 0) return QuestState.Completed;
                    if (failedCount == requiredCount && requiredCount > 0) return QuestState.Failed;
                    return QuestState.Active;

                case QuestCompletionRule.Threshold:
                    int threshold = _quest.CompletionThreshold;
                    if (completedCount >= threshold && threshold > 0) return QuestState.Completed;
                    int remaining = requiredCount - completedCount - failedCount;
                    if (completedCount + remaining < threshold) return QuestState.Failed;
                    return QuestState.Active;

                default: // AllRequired
                    if (failedCount > 0) return QuestState.Failed;
                    if (requiredCount > 0 && completedCount == requiredCount) return QuestState.Completed;
                    return QuestState.Active;
            }
        }

        private List<string> CollectByState(QuestState state)
        {
            var result = new List<string>();
            if (_quest == null || _context == null) return result;
            foreach (var obj in Objectives())
                if (GetObjectiveState(obj.Id) == state) result.Add(obj.Id);
            return result;
        }

        private void RaiseObjective(string id, QuestState state)
        {
            if (_lastObjectiveStates.TryGetValue(id, out var prev) && prev == state) return;
            _lastObjectiveStates[id] = state;
            OnObjectiveStateChanged?.Invoke(id, state);
        }

        private void RaiseQuest(QuestState state)
        {
            if (_questStateKnown && _lastQuestState == state) return;
            _lastQuestState = state;
            _questStateKnown = true;
            OnQuestStateChanged?.Invoke(state);
        }

        // Fires exactly once per guard key: guarded by the rewarded-set (persisted, so restore never re-fires).
        private void FireReward(string guardKey, string eventId, BaseAction reward)
        {
            if (reward == null || _context.CollectionContains(_rewardedKey, guardKey)) return;
            reward.Execute(_context);
            _context.AddToCollection(_rewardedKey, guardKey);
            OnRewardFired?.Invoke(eventId);
        }

        // ── Auto-evaluate (push mode) ────────────────────────────────────────

        private bool _autoEvaluate;
        private bool _evaluating;
        private bool _dirtyDuringEvaluate;

        /// <summary>True when auto-evaluate is active (subscribed to context changes).</summary>
        public bool IsAutoEvaluateEnabled => _autoEvaluate;

        /// <summary>
        /// Opts into push-mode evaluation: the evaluator subscribes to all context parameter changes,
        /// collection changes, and raised signals, and calls <see cref="Evaluate()"/> automatically.
        /// Eliminates the need for frame-polling — including for quests gated purely on
        /// <see cref="SignalRaisedCondition"/>. Idempotent — calling twice is a no-op.
        /// <para>
        /// <b>Timers are NOT auto-ticked.</b> Objectives with <see cref="ObjectiveNodeData.TimeLimitSeconds"/>
        /// still require the consumer to call <see cref="Evaluate(float)"/> with a clock.
        /// </para>
        /// </summary>
        public void EnableAutoEvaluate()
        {
            if (_autoEvaluate || _context == null) return;
            _autoEvaluate = true;
            _context.OnAnyVariableChanged(HandleAutoEvaluateTrigger);
            _context.OnAnyCollectionChanged(HandleAutoEvaluateTrigger);
            _context.OnAnySignalRaised(HandleAutoEvaluateTrigger);
        }

        /// <summary>
        /// Unregisters the wrapped <see cref="ReactiveEvaluator"/>'s editor live-state probe. A host that
        /// discards this evaluator (teardown, rebuilding it over the same quest graph) should call this so
        /// the dead evaluator's probe does not shadow the new one in the graph editor. No-op outside the
        /// editor. Pair with <see cref="DisableAutoEvaluate"/> when tearing down.
        /// </summary>
        public void DetachEditorProbe() => _reactive.DetachEditorProbe();

        /// <summary>
        /// Disables auto-evaluate and unsubscribes from context changes. Idempotent.
        /// </summary>
        public void DisableAutoEvaluate()
        {
            if (!_autoEvaluate || _context == null) return;
            _autoEvaluate = false;
            _dirtyDuringEvaluate = false;
            _context.OffAnyVariableChanged(HandleAutoEvaluateTrigger);
            _context.OffAnyCollectionChanged(HandleAutoEvaluateTrigger);
            _context.OffAnySignalRaised(HandleAutoEvaluateTrigger);
        }

        private void HandleAutoEvaluateTrigger(string _)
        {
            if (_evaluating)
            {
                _dirtyDuringEvaluate = true;
                return;
            }
            _evaluating = true;
            try
            {
                Evaluate();
                while (_dirtyDuringEvaluate)
                {
                    _dirtyDuringEvaluate = false;
                    Evaluate();
                }
            }
            finally
            {
                _evaluating = false;
            }
        }
    }
}
