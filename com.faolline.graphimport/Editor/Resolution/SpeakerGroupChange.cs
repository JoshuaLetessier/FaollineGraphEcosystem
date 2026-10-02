namespace Faolline.GraphImport.Editor
{
    /// <summary>One existing speaker whose localization group was realigned with the speaker tables mapping.</summary>
    public sealed class SpeakerGroupChange
    {
        public string SpeakerKey { get; }
        public string AssetPath { get; }
        public string OldGroup { get; }
        public string NewGroup { get; }

        public SpeakerGroupChange(string speakerKey, string assetPath, string oldGroup, string newGroup)
        {
            SpeakerKey = speakerKey;
            AssetPath = assetPath;
            OldGroup = oldGroup;
            NewGroup = newGroup;
        }
    }
}
