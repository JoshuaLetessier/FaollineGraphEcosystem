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

        [Header("Typewriter")]
        [SerializeField, Tooltip("Reveal line text character by character at play time.")]
        private bool typewriter = true;
        [SerializeField, Min(1f), Tooltip("Reveal speed in characters per second.")]
        private float charactersPerSecond = 40f;

        [Header("Debug")]
        [SerializeField] protected bool verboseLog;

        private Coroutine _typeRoutine;
        private Action<string> _typeApply;
        private string _typeFull = string.Empty;

        /// <summary>True while a line is being revealed by the typewriter.</summary>
        public bool IsTyping { get; private set; }

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
            StopTyping();
            if (_swapCo != null) { StopCoroutine(_swapCo); _swapCo = null; }
            DestroyAvatar(ref _currentAvatar);
            DestroyAvatar(ref _previousAvatar);
        }

        // ── Typewriter ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Applies <paramref name="full"/> via <paramref name="apply"/> — instantly in the editor or when
        /// disabled, or progressively at play time. Concrete views call this from ShowLine with their text
        /// setter so the typewriter is shared across front-ends.
        /// </summary>
        protected void ShowText(Action<string> apply, string full)
        {
            StopTyping();
            _typeApply = apply;
            _typeFull = full ?? string.Empty;

            if (!typewriter || !Application.isPlaying || charactersPerSecond <= 0f || _typeFull.Length == 0)
            {
                apply?.Invoke(_typeFull);
                IsTyping = false;
                return;
            }

            IsTyping = true;
            _typeRoutine = StartCoroutine(TypeRoutine());
        }

        /// <summary>Completes the current reveal immediately (e.g. the player advances while typing).</summary>
        public void SkipTyping()
        {
            if (!IsTyping) return;
            StopTyping();
            _typeApply?.Invoke(_typeFull);
        }

        private IEnumerator TypeRoutine()
        {
            _typeApply?.Invoke(string.Empty);
            float shown = 0f;
            while (shown < _typeFull.Length)
            {
                shown += Time.deltaTime * charactersPerSecond;
                int count = Mathf.Clamp(Mathf.FloorToInt(shown), 0, _typeFull.Length);
                _typeApply?.Invoke(_typeFull.Substring(0, count));
                yield return null;
            }
            _typeApply?.Invoke(_typeFull);
            IsTyping = false;
            _typeRoutine = null;
        }

        private void StopTyping()
        {
            if (_typeRoutine != null) { StopCoroutine(_typeRoutine); _typeRoutine = null; }
            IsTyping = false;
        }

        /// <summary>Name tint for a bound speaker (white when unknown). Used by concrete views.</summary>
        protected Color ResolveNameColor(string speakerId)
        {
            var s = FindSpeaker(speakerId);
            return s != null ? s.NameColor : Color.white;
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

        internal void ConfigureTypewriterForTest(bool enabled, float cps)
        {
            typewriter = enabled;
            charactersPerSecond = cps;
        }

        internal int TestCurrentAvatarCount => currentAvatarRoot != null ? currentAvatarRoot.childCount : 0;
        internal int TestPreviousAvatarCount => previousAvatarRoot != null ? previousAvatarRoot.childCount : 0;
    }
}
