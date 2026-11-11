using System.Diagnostics;
using FlowHub.Api.Endpoints;
using FlowHub.Application.Integrations;
using FlowHub.Application.Repositories;
using FlowHub.Application.Workspace;
using FlowHub.Domain;
using FlowHub.Domain.Repositories;
using FlowHub.Infrastructure.Git;
using FlowHub.Infrastructure.Yaml;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace FlowHub.Api.Tests;

/// <summary>Skan folderu z repozytoriami (prawdziwy git w katalogu tymczasowym) i dopisywanie do repos.yaml.</summary>
public sealed class LocalRepositoryTests(FlowHubApiFactory factory) : IClassFixture<FlowHubApiFactory>, IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"flowhub-scan-{Guid.NewGuid():N}");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        if (!Directory.Exists(_root))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        Directory.Delete(_root, recursive: true);
    }

    private YamlRepositoryCatalog Catalog(string yaml)
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "repos.yaml"), yaml);
        return new YamlRepositoryCatalog(new WorkspacePaths(Options.Create(new WorkspaceOptions { Root = _root })), NullLogger<YamlRepositoryCatalog>.Instance);
    }

    private LocalRepositoryService Scanner(string scanRoot, IRepositoryCatalog catalog) =>
        new(Options.Create(new LocalRepositoriesOptions { ScanRoot = scanRoot }), new GitCliService(), catalog);

    private string CreateRepository(string relativePath, string branch, string? origin, string commitMessage)
    {
        var path = Path.Combine(_root, "tfs", relativePath);
        Directory.CreateDirectory(path);
        Git(path, "init", "-q", "-b", branch);
        if (origin is not null)
        {
            Git(path, "remote", "add", "origin", origin);
        }

        File.WriteAllText(Path.Combine(path, "README.md"), "# demo");
        Git(path, "add", ".");
        Git(path, "-c", "user.name=Test", "-c", "user.email=test@example.com", "commit", "-q", "-m", commitMessage);
        return path;
    }

    private static void Git(string path, params string[] arguments)
    {
        var info = new ProcessStartInfo("git") { WorkingDirectory = path, UseShellExecute = false, RedirectStandardError = true, RedirectStandardOutput = true };
        foreach (var argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        using var process = Process.Start(info)!;
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, $"git {string.Join(' ', arguments)}: {process.StandardError.ReadToEnd()}");
    }

    [Fact]
    public async Task Repositories_are_found_loose_and_inside_branch_folders_with_branch_and_commits()
    {
        var catalog = Catalog("repositories:\n  - name: Billing.Api\n    url: https://tfs.example/col/P/_git/Billing.Api\n");
        CreateRepository("Shared.Lib", "main", null, "Pierwszy commit");
        var inBranchFolder = CreateRepository(Path.Combine("feature-123", "Billing.Api"), "feature/123", "https://tfs.example/col/P/_git/Billing.Api", "Poprawka VAT");
        File.WriteAllText(Path.Combine(inBranchFolder, "nowy.txt"), "x");
        Directory.CreateDirectory(Path.Combine(_root, "tfs", "notatki")); // zwykły folder — nie repo

        var scan = await Scanner(Path.Combine(_root, "tfs"), catalog).ScanAsync(Ct);

        Assert.Equal(["Billing.Api", "Shared.Lib"], scan.Repositories.Select(repository => repository.Name).Order());
        var billing = scan.Repositories.Single(repository => repository.Name == "Billing.Api");
        Assert.Equal((Path.Combine("feature-123", "Billing.Api"), "feature/123", "Billing.Api", 1), (billing.Folder, billing.Branch, billing.CatalogName, billing.UncommittedFiles));
        Assert.Equal("Poprawka VAT", Assert.Single(billing.RecentCommits).Subject);
        Assert.Null(scan.Repositories.Single(repository => repository.Name == "Shared.Lib").CatalogName);
    }

    [Fact]
    public async Task Local_repository_with_an_origin_can_be_added_to_the_catalog_without_touching_the_rest_of_the_file()
    {
        const string Original = "# Mój komentarz\nrepositories:\n  - name: Billing.Api\n    url: demo://Billing.Api\n    keywords: [faktura]\n";
        var catalog = Catalog(Original);
        var path = CreateRepository("Orders.Service", "main", "https://tfs.example/col/P/_git/Orders.Service", "Start");
        var scanner = Scanner(Path.Combine(_root, "tfs"), catalog);

        var added = await scanner.AddToCatalogAsync(path, Ct);
        var again = await scanner.AddToCatalogAsync(path, Ct);

        Assert.Equal(("Orders.Service", "https://tfs.example/col/P/_git/Orders.Service"), (added.Name, added.Url));
        Assert.Equal(added, again);
        Assert.Equal(["Billing.Api", "Orders.Service"], catalog.GetAll().Select(repository => repository.Name));
        var yaml = File.ReadAllText(Path.Combine(_root, "repos.yaml"));
        Assert.StartsWith(Original.TrimEnd(), yaml);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(yaml, "Orders.Service\"\\s*\n\\s*url"));
    }

    [Fact]
    public async Task Repository_without_origin_or_outside_the_folder_cannot_be_added()
    {
        var catalog = Catalog("repositories: []\n");
        var path = CreateRepository("Lokalne", "main", null, "Start");
        var scanner = Scanner(Path.Combine(_root, "tfs"), catalog);

        await Assert.ThrowsAsync<DomainException>(() => scanner.AddToCatalogAsync(path, Ct));
        await Assert.ThrowsAsync<DomainException>(() => scanner.AddToCatalogAsync(@"C:\Windows", Ct));
        Assert.Empty(catalog.GetAll());
    }

    [Theory]
    [InlineData("https://tfs.firma.pl/tfs/Col/Projekt/_git/Billing.Api", "Billing.Api")]
    [InlineData("git@github.com:firma/shared-lib.git", "shared-lib")]
    [InlineData("demo://Customer.Portal/", "Customer.Portal")]
    [InlineData(null, null)]
    public void Repository_name_comes_from_the_remote_address(string? url, string? expected)
    {
        Assert.Equal(expected, LocalRepositoryService.NameFromUrl(url));
    }

    [Fact]
    public async Task Without_a_configured_folder_the_scan_is_empty_and_says_so()
    {
        var scan = await factory.CreateClient().GetAsync<LocalRepositoryScan>("/api/repositories/local");

        Assert.Null(scan.Root);
        Assert.Empty(scan.Repositories);
    }

    [Fact]
    public async Task Server_repositories_can_be_listed_and_picking_a_known_one_returns_the_catalog_entry()
    {
        var client = factory.CreateClient();

        var remote = await client.GetAsync<List<RemoteRepository>>("/api/repositories/remote");
        var picked = await (await client.PostAsync("/api/repositories/remote", new AddRemoteRepositoryRequest("Billing.Api"))).ReadAsync<CodeRepository>();
        var unknown = await client.PostAsync("/api/repositories/remote", new AddRemoteRepositoryRequest("Nie.Ma.Takiego"));

        Assert.Contains(remote, repository => repository.Name == "Orders.Service"); // jest na serwerze, nie ma go jeszcze w katalogu
        Assert.Equal("Billing.Api", picked.Name);
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, unknown.StatusCode);
    }

    [Fact]
    public async Task Branch_status_shows_pull_request_build_and_deployments_for_any_branch_of_a_catalog_repo()
    {
        var status = await factory.CreateClient().GetAsync<BranchStatus>("/api/repositories/Shared.Lib/branch-status?branch=feature%2F123");

        Assert.Equal(HealthLevel.Ok, status.PullRequest.Level);
        Assert.Equal(HealthLevel.Ok, status.Build.Level);
        Assert.Contains(status.Environments, cell => cell.Environment == "DEV" && cell.Status.Level == HealthLevel.Ok);
    }
}