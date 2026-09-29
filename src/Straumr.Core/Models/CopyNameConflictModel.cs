namespace Straumr.Core.Models;

public sealed record CopyNameConflictModel(DependencyCopyModel? Dependency, string Message);
