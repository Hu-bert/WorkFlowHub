using FlowHub.Domain.Repositories;

namespace FlowHub.Application.Repositories;

/// <summary>Port: katalog repozytoriów (repos.yaml).</summary>
public interface IRepositoryCatalog
{
    IReadOnlyList<CodeRepository> GetAll();

    CodeRepository? Find(string name);

    void Reload();
}
