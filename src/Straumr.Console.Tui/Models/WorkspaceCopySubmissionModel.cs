namespace Straumr.Console.Tui.Models;

internal sealed record WorkspaceCopySubmissionModel(
    Guid SourceId,
    WorkspaceFormSubmissionModel Submission);
