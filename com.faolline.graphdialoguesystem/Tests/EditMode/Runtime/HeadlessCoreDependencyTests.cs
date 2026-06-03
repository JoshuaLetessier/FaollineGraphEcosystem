using System.Linq;
using NUnit.Framework;
using Faolline.GraphDialogue;

namespace Faolline.GraphDialogue.Tests
{
    /// <summary>
    /// Guards SC-007: the headless dialogue core (Runtime) must stay free of any UI dependency, so the
    /// player can run with the UI assembly removed. Verified by inspecting the Runtime assembly's
    /// referenced assemblies.
    /// </summary>
    public class HeadlessCoreDependencyTests
    {
        [Test]
        public void RuntimeAssembly_DoesNotReferenceUiAssemblies()
        {
            var refs = typeof(DialoguePlayer).Assembly
                .GetReferencedAssemblies()
                .Select(a => a.Name)
                .ToList();

            Assert.IsFalse(refs.Any(n => n != null && n.Contains("graphdialoguesystem.UI")),
                "Runtime must not reference the dialogue UI assembly.");
            Assert.IsFalse(refs.Contains("UnityEngine.UI"),
                "Runtime must not reference UnityEngine.UI.");
            Assert.IsFalse(refs.Contains("Unity.TextMeshPro"),
                "Runtime must not reference TextMeshPro.");
        }
    }
}
