using System.Runtime.CompilerServices;

// Expose internal test seams (e.g. ConfigureForTest) to the EditMode test assembly only.
[assembly: InternalsVisibleTo("com.faolline.graphdialoguesystem.UI.Tests.EditMode")]
