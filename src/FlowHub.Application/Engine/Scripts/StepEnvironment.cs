using FlowHub.Application.Repositories;
using FlowHub.Application.Workspace;

namespace FlowHub.Application.Engine.Scripts;

/// <summary>
/// Kontekst kroku jako zmienne środowiskowe FLOWHUB_* — jedyny kontrakt między FlowHub a skryptami.
/// Skrypt czyta tylko to, czego potrzebuje, więc dodanie nowej zmiennej nie psuje istniejących skryptów.
/// </summary>
public sealed class StepEnvironment(WorkspacePaths paths, IRepositoryCatalog repositories)
{
    public IReadOnlyDictionary<string, string> For(StepContext context)
    {
        var item = context.Item;
        var variables = new Dictionary<string, string>
        {
            ["FLOWHUB_ROOT"] = paths.Root,
            ["FLOWHUB_BLOCKS_DIR"] = paths.BlocksDirectory,
            ["FLOWHUB_WORK_DIR"] = item.WorkDirectory,
            ["FLOWHUB_TASK_KEY"] = item.Key,
            ["FLOWHUB_TASK_TITLE"] = item.Title,
            ["FLOWHUB_TASK_TYPE"] = item.Type,
            ["FLOWHUB_TASK_DESCRIPTION"] = item.Description,
            ["FLOWHUB_TASK_URL"] = item.SourceUrl ?? "",
            ["FLOWHUB_STEP_ID"] = context.Step.StepId,
            ["FLOWHUB_REMARK"] = context.Step.Remark ?? "",
        };

        if (context.Repo is { } repo)
        {
            var catalogEntry = repositories.Find(repo.RepositoryName);

            variables["FLOWHUB_REPO_NAME"] = repo.RepositoryName;
            variables["FLOWHUB_REPO_PATH"] = repo.LocalPath;
            variables["FLOWHUB_BRANCH"] = repo.Branch;
            variables["FLOWHUB_REPO_URL"] = catalogEntry?.Url ?? "";
            variables["FLOWHUB_DEFAULT_BRANCH"] = catalogEntry?.DefaultBranch ?? "main";
        }

        return variables;
    }
}
