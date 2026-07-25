using System.Runtime.CompilerServices;

// BaseContext.CopyValuesFrom is internal — restoring a history snapshot is a BaseRunner-only operation,
// not a public API. BaseRunner lives in the Unity Runtime assembly (com.faolline.graphcore.Runtime),
// which needs friend access now that it sits in a separate assembly from BaseContext.
[assembly: InternalsVisibleTo("com.faolline.graphcore.Runtime")]
