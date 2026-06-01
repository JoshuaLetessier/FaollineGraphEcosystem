using System;
using Faolline.GraphCore;

namespace Faolline.GraphDialogue
{
    /// <summary>
    /// A selectable dialogue option. Extends <see cref="BaseChoice"/> (which carries the stable <c>Id</c>
    /// used as the output port routing key, an optional gating <c>Condition</c>, and the editor-facing
    /// <c>Title</c>). The displayed label's localization key is derived from the choice Id via
    /// <see cref="DialogueLocalizationKeys.ForChoice"/> — there is no hand-typed key field that can break.
    /// </summary>
    [Serializable]
    public class DialogueChoice : BaseChoice
    {
    }
}
