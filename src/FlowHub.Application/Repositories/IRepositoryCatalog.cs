using FlowHub.Domain.Repositories;

namespace FlowHub.Application.Repositories;

/// <summary>Port: katalog repozytoriów (repos.yaml).</summary>
public interface IRepositoryCatalog
{
    IReadOnlyList<CodeRepository> GetAll();

    CodeRepository? Find(string name);

    void Reload();

    /// <summary>Zapisuje repo w katalogu (repos.yaml): nowe (originalName = null) albo zmienione (originalName = nazwa przed zmianą).</summary>
    CodeRepository Save(CodeRepository repository, string? originalName);

    bool Remove(string name);
}
