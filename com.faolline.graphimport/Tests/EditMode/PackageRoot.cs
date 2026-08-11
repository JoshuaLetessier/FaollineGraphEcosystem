using System.IO;
using System.Linq;
using UnityEditor.PackageManager;
using UnityEngine;

namespace Faolline.GraphImport.Tests
{
    /// <summary>
    /// Resolves com.faolline.graphimport's actual root folder regardless of how it's installed. Two
    /// real cases, neither of which the other one handles:
    /// <list type="bullet">
    /// <item>A real consumer installs it as a genuine UPM (git) dependency, resolved into
    /// Library/PackageCache/com.faolline.graphimport@&lt;hash&gt;/ — <see cref="PackageInfo"/> knows
    /// about it; `Application.dataPath`-based paths do not, since it's never under Assets/ at all.
    /// Found via real usage in a consumer project.</item>
    /// <item>This repo's own dev layout has every package as a plain folder directly under
    /// Assets/FaollineGraphEcosystem/ for convenient co-development — package.json exists, but
    /// nothing registers it with the Package Manager (not embedded under Packages/, not in
    /// manifest.json), so <see cref="PackageInfo.FindForAssembly"/> returns null here. Found by
    /// actually running this fix's own tests in this repo.</item>
    /// </list>
    /// Try PackageInfo first (the real-consumer case); fall back to the fixed dev-repo relative path.
    /// </summary>
    static class PackageRoot
    {
        public static string Combine(params string[] relativeSegments)
        {
            var info = PackageInfo.FindForAssembly(typeof(PackageRoot).Assembly);
            var basePath = info != null
                ? info.resolvedPath
                : Path.Combine(Application.dataPath, "FaollineGraphEcosystem", "com.faolline.graphimport");

            return Path.Combine(new[] { basePath }.Concat(relativeSegments).ToArray());
        }
    }
}
