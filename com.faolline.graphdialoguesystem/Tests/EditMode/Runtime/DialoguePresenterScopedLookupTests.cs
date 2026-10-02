using Faolline.GraphCore;
using Faolline.GraphLocalization;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Faolline.GraphDialogue.Tests
{
    /// <summary>
    /// 052 US2: the presenter looks each text up in the table that holds it — a line/choice/voice in its owning
    /// graph's table, a speaker name in the speaker's group table — when the backend supports targeted lookups.
    /// The two-argument overloads keep the classic (untargeted) lookup for line/choice text.
    /// </summary>
    public class DialoguePresenterScopedLookupTests
    {
        private DialogueGraph _owner;
        private Speaker _speaker;

        [SetUp]
        public void SetUp()
        {
            _owner = ScriptableObject.CreateInstance<DialogueGraph>();
            _owner.name = "DLG_Owner";
            _speaker = ScriptableObject.CreateInstance<Speaker>();
            _speaker.SpeakerId = "npc";
            _speaker.DisplayNameFallback = "NPC";
            _speaker.LocalizationGroup = "Chapitre1";
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_owner);
            Object.DestroyImmediate(_speaker);
        }

        private static DialogueLineNodeData Line() => new DialogueLineNodeData
        {
            Id = "l", NodeType = DialogueLineNodeData.NodeTypeId, SpeakerKey = "npc", Title = "Authored"
        };

        private static ChoiceNodeData Choice()
        {
            var node = new ChoiceNodeData { Id = "c", NodeType = ChoiceNodeData.NodeTypeId };
            node.Choices.Add(new DialogueChoice { Id = "a", Title = "Yes" });
            return node;
        }

        [Test]
        public void ResolveLine_WithOwner_TextAndVoiceInOwnerTable_SpeakerInGroupTable()
        {
            var provider = new ScopedRecordingProvider()
                .With("DLG_Owner", "line_l", "Bonjour")
                .With("GraphDialogue_Speakers_Chapitre1", "speaker_npc", "Aubergiste");
            var assets = new ScopedRecordingAssets();
            var presenter = new DialoguePresenter(provider, assets, _ => _speaker);

            var step = presenter.ResolveLine(Line(), new BaseContext(), _owner);

            Assert.AreEqual("Bonjour", step.ResolvedText);
            Assert.AreEqual("Aubergiste", step.ResolvedSpeakerName);
            CollectionAssert.AreEquivalent(new[] { "DLG_Owner", "GraphDialogue_Speakers_Chapitre1" }, provider.TablesAsked());
            CollectionAssert.AreEqual(new[] { ("DLG_Owner", "line_l") }, assets.ScopedCalls);
            Assert.IsEmpty(provider.ClassicCalls);
            Assert.IsEmpty(assets.ClassicCalls);
        }

        [Test]
        public void ResolveChoice_WithOwner_LabelsInOwnerTable()
        {
            var provider = new ScopedRecordingProvider().With("DLG_Owner", "choice_a", "Oui");
            var presenter = new DialoguePresenter(provider);

            var step = presenter.ResolveChoice(Choice(), new BaseContext(), _owner);

            Assert.AreEqual("Oui", step.Options[0].ResolvedLabel);
            CollectionAssert.AreEqual(new[] { ("DLG_Owner", "choice_a") }, provider.ScopedCalls);
        }

        [Test]
        public void Resolve_WithOwner_DispatchesToTheTargetedOverloads()
        {
            var provider = new ScopedRecordingProvider().With("DLG_Owner", "line_l", "Bonjour");
            var presenter = new DialoguePresenter(provider);

            var step = presenter.Resolve(Line(), new BaseContext(), _owner) as LineStep;

            Assert.AreEqual("Bonjour", step.ResolvedText);
        }

        [Test]
        public void TwoArgumentOverloads_KeepTheClassicLookupForLineText_SpeakerStillTargeted()
        {
            var provider = new ScopedRecordingProvider()
                .With("DLG_Owner", "line_l", "Bonjour")
                .With("GraphDialogue_Speakers_Chapitre1", "speaker_npc", "Aubergiste");
            var presenter = new DialoguePresenter(provider, speakerLookup: _ => _speaker);

            var step = presenter.ResolveLine(Line(), new BaseContext());

            Assert.AreEqual("Bonjour", step.ResolvedText, "classic lookup still finds the text");
            CollectionAssert.Contains(provider.ClassicCalls, "line_l");
            Assert.AreEqual("Aubergiste", step.ResolvedSpeakerName);
            CollectionAssert.AreEqual(new[] { ("GraphDialogue_Speakers_Chapitre1", "speaker_npc") }, provider.ScopedCalls);
        }

        [Test]
        public void ScopedMiss_TitleFallback_StillApplies()
        {
            var presenter = new DialoguePresenter(new ScopedRecordingProvider(), titleFallback: true);
            Assert.AreEqual("Authored", presenter.ResolveLine(Line(), new BaseContext(), _owner).ResolvedText);
        }

        [Test]
        public void ScopedMiss_Audit_RecordsTheMissingKey()
        {
            var presenter = new DialoguePresenter(new ScopedRecordingProvider(), strictMode: LocalizationStrictMode.Audit);
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Missing localization key 'line_l'"));
            presenter.ResolveLine(Line(), new BaseContext(), _owner);
            CollectionAssert.Contains(presenter.MissingKeys, "line_l");
        }

        [Test]
        public void ScopedMiss_Strict_Throws()
        {
            var presenter = new DialoguePresenter(new ScopedRecordingProvider(), strictMode: LocalizationStrictMode.Strict);
            Assert.Throws<LocalizationException>(() => presenter.ResolveLine(Line(), new BaseContext(), _owner));
        }

        [Test]
        public void SpeakerScopedMiss_FallsBackToDisplayNameFallback()
        {
            var presenter = new DialoguePresenter(new ScopedRecordingProvider(), speakerLookup: _ => _speaker);
            Assert.AreEqual("NPC", presenter.ResolveLine(Line(), new BaseContext(), _owner).ResolvedSpeakerName);
        }
    }
}
