namespace Straumr.Console.Tui.Models;

internal sealed record WorkspaceExportSubmissionModel(
    Guid WorkspaceId,
    string WorkspaceName,
    string OutputDirectory);
