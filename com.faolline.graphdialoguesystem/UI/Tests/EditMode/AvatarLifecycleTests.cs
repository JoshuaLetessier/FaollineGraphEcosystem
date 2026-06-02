using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Faolline.GraphDialogue;
using Faolline.GraphDialogue.UI;

namespace Faolline.GraphDialogue.UI.Tests
{
    /// <summary>EditMode tests for the shared avatar lifecycle in DialogueViewBase (instant path).</summary>
    public class AvatarLifecycleTests
    {
        // Minimal concrete view exposing the avatar seam; rendering is irrelevant here.
        private sealed class AvatarTestView : DialogueViewBase
        {
            public override void ShowLine(LineStep step) { }
            public override void ShowChoices(ChoiceStep step) { }
            public override void HideAll() { }
        }

        private GameObject _viewGo, _curRoot, _prevRoot;
        private readonly List<Object> _spawnedPrefabs = new List<Object>();

        private AvatarTestView Build()
        {
            _viewGo = new GameObject("avatar-view");
            var view = _viewGo.AddComponent<AvatarTestView>();
            _curRoot = new GameObject("current").transform.gameObject;
            _prevRoot = new GameObject("previous").transform.gameObject;
            view.ConfigureAvatarsForTest(_curRoot.transform, _prevRoot.transform);
            return view;
        }

        private Speaker NewSpeaker(string id, bool withAvatar)
        {
            var s = ScriptableObject.CreateInstance<Speaker>();
            s.SpeakerId = id;
            if (withAvatar)
            {
                var prefab = new GameObject($"{id}-avatar");
                _spawnedPrefabs.Add(prefab);
                s.FallbackExpression = prefab; // TryGetExpression returns the fallback for any key
            }
            _spawnedPrefabs.Add(s);
            return s;
        }

        [TearDown]
        public void TearDown()
        {
            if (_viewGo) Object.DestroyImmediate(_viewGo);
            if (_curRoot) Object.DestroyImmediate(_curRoot);
            if (_prevRoot) Object.DestroyImmediate(_prevRoot);
            foreach (var o in _spawnedPrefabs) if (o) Object.DestroyImmediate(o);
            _spawnedPrefabs.Clear();
        }

        [Test]
        public void KnownSpeaker_SpawnsAvatarAtCurrentRoot()
        {
            var view = Build();
            view.BindSpeakers(new[] { NewSpeaker("npc", true) });

            view.TestRequestAvatarSwap("npc", "neutral");

            Assert.AreEqual(1, view.TestCurrentAvatarCount, "Avatar spawned at the current root.");
            Assert.AreEqual(0, view.TestPreviousAvatarCount);
        }

        [Test]
        public void UnknownSpeaker_SpawnsNothing_NoThrow()
        {
            var view = Build();
            view.BindSpeakers(new[] { NewSpeaker("npc", true) });

            Assert.DoesNotThrow(() => view.TestRequestAvatarSwap("ghost", "neutral"));
            Assert.AreEqual(0, view.TestCurrentAvatarCount);
        }

        [Test]
        public void SpeakerWithoutAvatar_SpawnsNothing()
        {
            var view = Build();
            view.BindSpeakers(new[] { NewSpeaker("mute", false) });

            view.TestRequestAvatarSwap("mute", "neutral");

            Assert.AreEqual(0, view.TestCurrentAvatarCount);
        }

        [Test]
        public void SwappingSpeaker_DemotesPreviousToPreviousRoot()
        {
            var view = Build();
            view.BindSpeakers(new[] { NewSpeaker("a", true), NewSpeaker("b", true) });

            view.TestRequestAvatarSwap("a", "neutral");
            view.TestRequestAvatarSwap("b", "neutral");

            Assert.AreEqual(1, view.TestCurrentAvatarCount, "New speaker's avatar at current root.");
            Assert.AreEqual(1, view.TestPreviousAvatarCount, "Prior speaker's avatar demoted to previous root.");
        }

        [Test]
        public void ClearAvatarsOnHide_RemovesAll()
        {
            var view = Build();
            view.BindSpeakers(new[] { NewSpeaker("a", true), NewSpeaker("b", true) });
            view.TestRequestAvatarSwap("a", "neutral");
            view.TestRequestAvatarSwap("b", "neutral");

            view.TestClearAvatarsOnHide();

            Assert.AreEqual(0, view.TestCurrentAvatarCount);
            Assert.AreEqual(0, view.TestPreviousAvatarCount);
        }
    }
}
