using System;

namespace Faolline.GraphImport
{
    /// <summary>A speaker tables CSV that cannot be used as-is (missing column, empty key, conflicting rows).</summary>
    public sealed class SpeakerGroupMappingException : Exception
    {
        public SpeakerGroupMappingException(string message) : base(message) { }
    }
}
