namespace Faolline.GraphLocalization
{
    /// <summary>
    /// Flags indicating which localized asset types accompany a node's text. Combinable:
    /// a voiced line with a localized portrait is <c>Audio | Sprite</c>. The localization
    /// pipeline creates Asset Table entries only for nodes with at least one non-text flag set.
    /// New flags can be added (next power of two) without breaking existing data.
    /// </summary>
    [System.Flags]
    public enum LocalizedAssetFlags
    {
        None    = 0,
        Text    = 1 << 0,
        Audio   = 1 << 1,
        Sprite  = 1 << 2,
        Texture = 1 << 3,
        Video   = 1 << 4,
        Font    = 1 << 5,
    }
}
