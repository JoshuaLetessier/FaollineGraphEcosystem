using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Faolline.GraphDialogue;

namespace Faolline.GraphDialogue.UI
{
    /// <summary>
    /// Abstract base for dialogue views. Holds the technology-independent pieces shared by every
    /// front-end: the speaker registry and the avatar lifecycle (spawn current, demote previous,
    /// despawn, optional transition, clear on hide). Concrete subclasses render lines and choices.
    /// </summary>
    public abstract class DialogueViewBase : MonoBehaviour, IDialogueView
    {
        [Header("Avatars")]
        [SerializeField] private Transform currentAvatarRoot;
        [SerializeField] private Transform previousAvatarRoot;
        [SerializeField] private bool destroyAvatarOnHide = true;
        [SerializeField] private AvatarTransition transition;

        [Header("Debug")]
        [SerializeField] protected bool verboseLog;

        // Speaker registry, indexed by Speaker.SpeakerId.
        private readonly Dictionary<string, Speaker> _speakersById = new Dictionary<string, Speaker>();

        // Avatar state.
        private GameObject _currentAvatar;
        private GameObject _previousAvatar;
        private string _currentSpeakerId;
        private string _currentExpressionKey;
        private Coroutine _swapCo;

        /// <summary>Raised when an avatar GameObject is spawned (for game-side hooks).</summary>
        public event Action<GameObject> OnAvatarSpawned;
        /// <summary>Raised when an avatar GameObject is despawned.</summary>
        public event Action<GameObject> OnAvatarDespawned;

        /// <inheritdoc/>
        public event Action<string> ChoiceSelected;

        /// <summary>Raises <see cref="ChoiceSelected"/> for the given routing id. Used by subclasses.</summary>
        protected void RaiseChoiceSelected(string choiceId)
        {
            if (string.IsNullOrEmpty(choiceId)) return;
            ChoiceSelected?.Invoke(choiceId);
        }

        // ── Speakers ─────────────────────────────────────────────────────────────────

        /// <inheritdoc/>
        public virtual void BindSpeakers(IReadOnlyList<Speaker> speakers)
        {
            _speakersById.Clear();
            if (speakers == null) return;
            foreach (var s in speakers)
            {
                if (s == null || string.IsNullOrEmpty(s.SpeakerId)) continue;
                if (!_speakersById.ContainsKey(s.SpeakerId))
                    _speakersById.Add(s.SpeakerId, s);
            }
        }

        /// <summary>Resolves a bound speaker by id, or null.</summary>
        protected Speaker FindSpeaker(string speakerId)
        {
            if (string.IsNullOrEmpty(speakerId)) return null;
            return _speakersById.TryGetValue(speakerId, out var s) ? s : null;
        }

        // ── Avatar lifecycle ─────────────────────────────────────────────────────────

        /// <summary>
        /// Requests the avatar for <paramref name="speakerId"/> + <paramref name="expressionKey"/>.
        /// Resolves the prefab via <see cref="Speaker.TryGetExpression"/> (falls back to the speaker's
        /// fallback asset). Unknown speaker/expression clears the current avatar — never throws.
        /// </summary>
        protected void RequestAvatarSwap(string speakerId, string expressionKey)
        {
            var prefab = ResolvePrefab(speakerId, expressionKey);
            if (prefab == null) { ClearCurrentAvatar(); return; }

            if (_currentAvatar != null && _currentSpeakerId == speakerId && _currentExpressionKey == expressionKey)
                return; // already showing it

            if (transition != null && Application.isPlaying && isActiveAndEnabled)
            {
                if (_swapCo != null) StopCoroutine(_swapCo);
                _swapCo = StartCoroutine(SwapAnimated(prefab, speakerId, expressionKey));
            }
            else
            {
                SwapInstant(prefab, speakerId, expressionKey);
            }
        }

        /// <summary>Clears all avatars when the dialogue hides (if configured to destroy on hide).</summary>
        protected void ClearAvatarsOnHide()
        {
            if (!destroyAvatarOnHide) return;
            if (_swapCo != null) { StopCoroutine(_swapCo); _swapCo = null; }
            DestroyAvatar(ref _currentAvatar);
            DestroyAvatar(ref _previousAvatar);
            _currentSpeakerId = null;
            _currentExpressionKey = null;
        }

        private GameObject ResolvePrefab(string speakerId, string expressionKey)
        {
            var speaker = FindSpeaker(speakerId);
            if (speaker == null) return null;
            speaker.TryGetExpression(expressionKey, out var asset);
            return asset as GameObject;
        }

        private void SwapInstant(GameObject prefab, string speakerId, string expressionKey)
        {
            DemoteOrDestroyCurrent();
            SpawnCurrent(prefab, speakerId, expressionKey);
        }

        private IEnumerator SwapAnimated(GameObject prefab, string speakerId, string expressionKey)
        {
            if (_currentAvatar != null && previousAvatarRoot != null && transition != null)
                yield return transition.DemoteToPrevious(_currentAvatar, previousAvatarRoot);
            DemoteOrDestroyCurrent();

            SpawnCurrent(prefab, speakerId, expressionKey);
            if (transition != null && _currentAvatar != null)
                yield return transition.Spawn(_currentAvatar);

            _swapCo = null;
        }

        private void DemoteOrDestroyCurrent()
        {
            if (_currentAvatar == null) return;
            if (previousAvatarRoot != null)
            {
                DestroyAvatar(ref _previousAvatar);
                _currentAvatar.transform.SetParent(previousAvatarRoot, false);
                _previousAvatar = _currentAvatar;
            }
            else
            {
                DestroyAvatar(ref _currentAvatar);
            }
            _currentAvatar = null;
        }

        private void SpawnCurrent(GameObject prefab, string speakerId, string expressionKey)
        {
            if (currentAvatarRoot != null)
            {
                _currentAvatar = Instantiate(prefab, currentAvatarRoot);
                _currentAvatar.name = prefab.name + " (Current)";
                OnAvatarSpawned?.Invoke(_currentAvatar);
            }
            _currentSpeakerId = speakerId;
            _currentExpressionKey = expressionKey;
        }

        private void ClearCurrentAvatar()
        {
            DestroyAvatar(ref _currentAvatar);
            _currentSpeakerId = null;
            _currentExpressionKey = null;
        }

        private void DestroyAvatar(ref GameObject go)
        {
            if (go == null) return;
            var toKill = go;
            go = null;
            OnAvatarDespawned?.Invoke(toKill);
            if (Application.isPlaying) Destroy(toKill);
            else DestroyImmediate(toKill);
        }

        protected virtual void OnDestroy()
        {
            if (_swapCo != null) { StopCoroutine(_swapCo); _swapCo = null; }
            DestroyAvatar(ref _currentAvatar);
            DestroyAvatar(ref _previousAvatar);
        }

        // ── Rendering (implemented by concrete views) ───────────────────────────────

        /// <inheritdoc/>
        public abstract void ShowLine(LineStep step);

        /// <inheritdoc/>
        public abstract void ShowChoices(ChoiceStep step);

        /// <inheritdoc/>
        public abstract void HideAll();

        // ── Test seam ──────────────────────────────────────────────────────────────

        internal void ConfigureAvatarsForTest(Transform current, Transform previous, bool destroyOnHide = true)
        {
            currentAvatarRoot = current;
            previousAvatarRoot = previous;
            destroyAvatarOnHide = destroyOnHide;
        }

        internal void TestRequestAvatarSwap(string speakerId, string expressionKey)
            => RequestAvatarSwap(speakerId, expressionKey);

        internal void TestClearAvatarsOnHide() => ClearAvatarsOnHide();

        internal int TestCurrentAvatarCount => currentAvatarRoot != null ? currentAvatarRoot.childCount : 0;
        internal int TestPreviousAvatarCount => previousAvatarRoot != null ? previousAvatarRoot.childCount : 0;
    }
}
