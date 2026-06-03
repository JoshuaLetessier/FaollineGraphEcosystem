using System.Runtime.CompilerServices;

// Expose internal test seams (e.g. ConfigureForTest) to the test assemblies only.
[assembly: InternalsVisibleTo("com.faolline.graphdialoguesystem.UI.Tests.EditMode")]
[assembly: InternalsVisibleTo("com.faolline.graphdialoguesystem.UI.Tests.PlayMode")]
