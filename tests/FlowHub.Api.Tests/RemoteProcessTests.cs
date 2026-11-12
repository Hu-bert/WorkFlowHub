using FlowHub.Application.Repositories;
using FlowHub.Domain;
using FlowHub.Domain.Repositories;
using FlowHub.Infrastructure.Processes;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FlowHub.Api.Tests;

/// <summary>„Czy serwis wstał po wdrożeniu?” — procesy i usługi przypisane do repo.</summary>
public sealed class RemoteProcessTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Real_probe_finds_a_process_running_on_this_machine_by_the_start_of_its_name()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "CIM jest tylko na Windows.");
        var probe = new WindowsProcessProbe();

        var found = await probe.FindAsync("localhost", "explorer", Ct);
        var missing = await probe.FindAsync("localhost", "nie-ma-takiego-procesu-flowhub", Ct);

        Assert.Contains(found, item => item.Kind == "process" && item.Name.StartsWith("explorer", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(missing);
    }

    [Fact]
    public async Task Unreachable_machine_gives_one_readable_line_instead_of_a_powershell_dump()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "CIM jest tylko na Windows.");

        var error = await Assert.ThrowsAsync<DomainException>(() => new WindowsProcessProbe().FindAsync("nie-ma-takiej-maszyny-flowhub", "explorer", Ct));

        Assert.StartsWith("nie-ma-takiej-maszyny-flowhub: ", error.Message);
        Assert.DoesNotContain("\uFFFD", error.Message); // polskie znaki nie są zepsute
        Assert.DoesNotContain("CategoryInfo", error.Message);
    }

    [Fact]
    public async Task One_unreachable_machine_does_not_hide_the_result_from_the_others()
    {
        var probe = Substitute.For<IRemoteProcessProbe>();
        probe.FindAsync("v-app-02", "Kcf.Monitoring", Arg.Any<CancellationToken>())
            .Returns([new RunningProcess("process", "Kcf.Monitoring.Service.exe", "Running", DateTime.UtcNow)]);
        probe.FindAsync("v-app-03", "Kcf.Monitoring", Arg.Any<CancellationToken>()).ThrowsAsync(new DomainException("v-app-03: brak dostępu"));
        var repository = new CodeRepository
        {
            Name = "Kcf", Url = "x",
            Processes = [new("v-app-02", "Kcf.Monitoring", "VDEV2"), new("v-app-03", "Kcf.Monitoring", "TEST-CD")],
        };

        var states = await new RemoteProcessService(probe).CheckAsync(repository, Ct);

        Assert.Equal((true, "VDEV2", null), (states[0].Running, states[0].Label, states[0].Error));
        Assert.Equal((false, "v-app-03: brak dostępu"), (states[1].Running, states[1].Error));
    }

    [Fact]
    public async Task Stopped_service_counts_as_not_running()
    {
        var probe = Substitute.For<IRemoteProcessProbe>();
        probe.FindAsync(default!, default!, default).ReturnsForAnyArgs([new RunningProcess("service", "Kcf Monitoring", "Stopped", null)]);
        var repository = new CodeRepository { Name = "Kcf", Url = "x", Processes = [new("v-app-02", "Kcf")] };

        var state = Assert.Single(await new RemoteProcessService(probe).CheckAsync(repository, Ct));

        Assert.False(state.Running);
        Assert.Single(state.Found);
    }

    [Theory]
    [InlineData("v-app-02; Remove-Item C:\\", "Kcf")]
    [InlineData("v-app-02", "Kcf' OR 1=1 --")]
    [InlineData("v-app-02", "$(calc)")]
    [InlineData("", "Kcf")]
    public async Task Unsafe_host_or_process_names_never_reach_the_command(string host, string name)
    {
        var probe = Substitute.For<IRemoteProcessProbe>();
        var repository = new CodeRepository { Name = "Kcf", Url = "x", Processes = [new(host, name)] };

        var state = Assert.Single(await new RemoteProcessService(probe).CheckAsync(repository, Ct));

        Assert.NotNull(state.Error);
        await probe.DidNotReceiveWithAnyArgs().FindAsync(default!, default!, default);
    }
}