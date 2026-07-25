using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Faolline.GraphCore
{
    /// <summary>
    /// Wires the Unity engine layer into Core once per domain load: registers <c>Vector2</c>/<c>Vector3</c>/
    /// <c>Color</c> with <see cref="BaseContextTypeRegistry"/> (Core cannot name these types itself — see
    /// that class) and points <see cref="GraphLog"/> at <c>Debug.LogWarning</c>/<c>Debug.LogError</c>.
    /// <see cref="InitializeOnLoadAttribute"/> covers the editor (including EditMode tests, which run
    /// inside the editor process); <see cref="RuntimeInitializeOnLoadMethodAttribute"/> covers player
    /// builds, where the editor-only attribute above does not exist.
    /// </summary>
#if UNITY_EDITOR
    [InitializeOnLoad]
#endif
    internal static class GraphCoreUnityBootstrap
    {
        static GraphCoreUnityBootstrap() => Register();

#if !UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void RuntimeRegister() => Register();
#endif

        private static void Register()
        {
            BaseContextTypeRegistry.RegisterSupportedType<Vector2>();
            BaseContextTypeRegistry.RegisterSupportedType<Vector3>();
            BaseContextTypeRegistry.RegisterSupportedType<Color>();

            GraphLog.WarningSink = Debug.LogWarning;
            GraphLog.ErrorSink = Debug.LogError;
        }
    }
}
