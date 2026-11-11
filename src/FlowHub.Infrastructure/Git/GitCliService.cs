using System.Diagnostics;
using System.Text;
using FlowHub.Application.Git;
using FlowHub.Domain;

namespace FlowHub.Infrastructure.Git;

/// <summary>Odczyt zmian przez polecenie `git` (tylko polecenia czytające).</summary>
public sealed class GitCliService : IGitService
{
    private const int MaxPatchLength = 400_000;

    public async Task<RepoDiff> DiffAsync(string repo, string repositoryPath, string baseBranch, CancellationToken ct)
    {
        if (!Directory.Exists(Path.Combine(repositoryPath, ".git")))
        {
            throw new DomainException($"Repo {repo} nie jest jeszcze sklonowane ({repositoryPath}).");
        }

        var stat = await RunAsync(repositoryPath, ct, "diff", "--stat", baseBranch);
        var patch = await RunAsync(repositoryPath, ct, "diff", baseBranch);
        var untracked = await RunAsync(repositoryPath, ct, "ls-files", "--others", "--exclude-standard");

        return new RepoDiff(
            repo,
            baseBranch,
            stat.TrimEnd(),
            patch.Length > MaxPatchLength ? patch[..MaxPatchLength] : patch,
            untracked.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            patch.Length > MaxPatchLength);
    }

    public async Task<GitWorkingCopy> DescribeAsync(string repositoryPath, CancellationToken ct)
    {
        // Puste repo (bez commitów) albo brak origin to nie błąd — po prostu nie ma czego pokazać.
        var remote = await TryRunAsync(repositoryPath, ct, "config", "--get", "remote.origin.url");
        var branch = await TryRunAsync(repositoryPath, ct, "rev-parse", "--abbrev-ref", "HEAD");
        var log = await TryRunAsync(repositoryPath, ct, "log", "-5", $"--pretty=format:%h{FieldSeparator}%an{FieldSeparator}%aI{FieldSeparator}%s");
        var status = await TryRunAsync(repositoryPath, ct, "status", "--porcelain");

        return new GitWorkingCopy(
            string.IsNullOrWhiteSpace(remote) ? null : remote.Trim(),
            string.IsNullOrWhiteSpace(branch) ? "(brak commitów)" : branch.Trim(),
            Lines(log).Select(ParseCommit).OfType<GitCommit>().ToList(),
            Lines(status).Length);
    }

    private const char FieldSeparator = '\u001f';

    private static GitCommit? ParseCommit(string line)
    {
        var fields = line.Split(FieldSeparator);
        return fields.Length == 4 && DateTimeOffset.TryParse(fields[2], out var date)
            ? new GitCommit(fields[0], fields[1], date, fields[3])
            : null;
    }

    private static string[] Lines(string? text) =>
        (text ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static async Task<string?> TryRunAsync(string repositoryPath, CancellationToken ct, params string[] arguments)
    {
        try
        {
            return await RunAsync(repositoryPath, ct, arguments);
        }
        catch (DomainException)
        {
            return null;
        }
    }

    private static async Task<string> RunAsync(string repositoryPath, CancellationToken ct, params string[] arguments)
    {
        var info = new ProcessStartInfo("git")
        {
            WorkingDirectory = repositoryPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        // Polskie znaki w nazwach plików bez „\305\202”.
        foreach (var argument in (string[])["-c", "core.quotepath=false", .. arguments])
        {
            info.ArgumentList.Add(argument);
        }

        using var process = Process.Start(info) ?? throw new DomainException("Nie udało się uruchomić polecenia git.");
        var output = process.StandardOutput.ReadToEndAsync(ct);
        var errors = process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);

        return process.ExitCode == 0
            ? await output
            : throw new DomainException($"git {string.Join(' ', arguments)}: {(await errors).Trim()}");
    }
}
