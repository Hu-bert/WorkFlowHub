using FlowHub.Domain.Repositories;

namespace FlowHub.Application.Repositories;

/// <summary>Port: katalog repozytoriów (repos.yaml).</summary>
public interface IRepositoryCatalog
{
    IReadOnlyList<CodeRepository> GetAll();

    CodeRepository? Find(string name);

    void Reload();

    /// <summary>Dopisuje repo do katalogu (repos.yaml). Gdy repo o tej nazwie już jest — zwraca istniejące.</summary>
    CodeRepository Add(CodeRepository repository);
}
