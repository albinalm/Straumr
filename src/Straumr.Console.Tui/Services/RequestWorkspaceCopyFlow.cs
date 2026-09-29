using Straumr.Console.Tui.Screens.Components.Shared;
using Straumr.Core.Enums;
using Straumr.Core.Exceptions;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;

namespace Straumr.Console.Tui.Services;

internal sealed class RequestWorkspaceCopyFlow
{
    private readonly IStraumrRequestCopyService _copies;
    private readonly Action<TuiCommandResultModel> _notify;
    private readonly WorkspaceCopyFlow _picker;
    private readonly IStraumrWorkspaceService _workspaces;
    private string _destinationName = string.Empty;
    private Func<CancellationToken, Task>? _pending;
    private DependencyCopyAction? _variablePolicy;
    public RequestWorkspaceCopyFlow(IStraumrWorkspaceService workspaces, IStraumrRequestCopyService copies, Action<TuiCommandResultModel> notify)
    {
        (_workspaces, _copies, _notify) = (workspaces, copies, notify);
        _picker = new WorkspaceCopyFlow(workspaces, notify);
    }
    public void Begin(StraumrWorkspaceEntry source, Guid id, string name)
    {
        _variablePolicy = null;
        _picker.Choose(source, name, destination => _pending = token => PrepareAsync(source, id, name, destination, token));
    }
    public async Task UpdateAsync(CancellationToken cancellationToken)
    {
        await _picker.UpdateAsync(cancellationToken);
        if (_pending is not { } operation)
        {
            return;
        }
        _pending = null;
        try { await operation(cancellationToken); }
        catch (Exception exception) when (exception is StraumrException or IOException or UnauthorizedAccessException)
        {
            _notify(TuiCommandResultModel.Failed($"copy failed: {exception.Message}"));
        }
    }
    private async Task PrepareAsync(StraumrWorkspaceEntry source, Guid id, string name, StraumrWorkspace destination, CancellationToken token)
    {
        _destinationName = destination.Name;
        RequestCopyPlanModel plan = await _copies.PrepareAsync(source, id, _workspaces.GetEntry(destination.Id), name, token);
        await CheckRequestNameAsync(plan, token);
    }
    private async Task CheckRequestNameAsync(RequestCopyPlanModel plan, CancellationToken token)
    {
        if (await _copies.FindNameConflictAsync(plan, token) is { } conflict)
        {
            Rename(plan.Copy, conflict.Message, () => _pending = next => CheckRequestNameAsync(plan, next));
            return;
        }
        if (!plan.HasDependencies)
        {
            _pending = next => SaveAsync(plan, next);
            return;
        }
        string detail = string.Join(", ", plan.Dependencies.Select(dependency => $"{dependency.Kind}: {dependency.Original.Name}"));
        if (plan.Missing.Count > 0)
        {
            detail += $"{Environment.NewLine}Missing: {string.Join(", ", plan.Missing)}. Repair these before carrying dependencies.";
        }
        new ConfirmDialog("Copy request dependencies", "Carry the bound auth and variables into the destination workspace?", detail,
            "Carry dependencies", false, () =>
            {
                plan.CarryDependencies = true;
                _pending = next => ResolveDependenciesAsync(plan, next);
            }, alternateLabel: "Request only", alternate: () => _pending = next => SaveAsync(plan, next)).Show();
    }
    private Task ResolveDependenciesAsync(RequestCopyPlanModel plan, CancellationToken token)
    {
        DependencyCopyModel? conflict = plan.Dependencies.FirstOrDefault(dependency => dependency.Existing is not null && !dependency.Resolved);
        if (conflict is null)
        {
            _pending = next => SaveAsync(plan, next);
            return Task.CompletedTask;
        }
        if (conflict.Kind == "variable" && _variablePolicy is { } policy)
        {
            ApplyChoice(plan, conflict, policy, false);
            return Task.CompletedTask;
        }
        int remaining = plan.Variables.Count(variable => variable.Existing is not null && !variable.Resolved);
        new DependencyConflictDialog(conflict.Kind, conflict.Original.Name, remaining,
            (choice, all) => ApplyChoice(plan, conflict, choice, all)).Show();
        return Task.CompletedTask;
    }
    private void ApplyChoice(RequestCopyPlanModel plan, DependencyCopyModel dependency, DependencyCopyAction choice, bool all)
    {
        if (all && dependency.Kind == "variable")
        {
            _variablePolicy = choice;
        }
        dependency.Action = choice;
        dependency.Resolved = true;
        if (choice == DependencyCopyAction.Create)
        {
            Rename(dependency.Copy, $"Choose a new name for the copied {dependency.Kind} {dependency.Original.Name}.",
                () => _pending = token => ResolveDependenciesAsync(plan, token));
        }
        else
        {
            _pending = token => ResolveDependenciesAsync(plan, token);
        }
    }
    private async Task SaveAsync(RequestCopyPlanModel plan, CancellationToken token)
    {
        if (await _copies.FindNameConflictAsync(plan, token) is { } conflict)
        {
            Rename(conflict.Dependency?.Copy ?? plan.Copy, conflict.Message, () => _pending = next => SaveAsync(plan, next));
            return;
        }
        await _copies.CopyAsync(plan, token);
        _notify(TuiCommandResultModel.Ok($"copied {plan.Copy.Name} to {_destinationName}"));
    }
    private static void Rename(StraumrModelBase entity, string message, Action next) =>
        new TextPromptDialog("Name the copy", message, entity.Name, "Continue",
            name => string.IsNullOrWhiteSpace(name) ? "Enter a name." : name.Contains('"') ? "Names cannot contain double quotes." : null,
            name =>
            {
                entity.Name = name;
                next();
            }).Show();
}
