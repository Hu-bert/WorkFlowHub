using System.Net;
using FlowHub.Api.Endpoints;
using FlowHub.Application.Blocks;
using FlowHub.Application.WorkItems;
using FlowHub.Domain.WorkItems;

namespace FlowHub.Api.Tests;

/// <summary>Edycja plików md zadania, krok „co dalej?” w prawdziwych flow i generator bloczków przez API.</summary>
public sealed class EditingAndFollowUpTests(FlowHubApiFactory factory) : IClassFixture<FlowHubApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<WorkItemDetails> CreateAsync(HttpClient client, string key)
    {
        var created = await client.PostAsync("/api/work-items", new CreateWorkItemRequest(
            key, "Edycja i przegląd", "Opis", "Technical", TaskSourceKind.Manual, null, ["Shared.Lib"]));
        return await created.ReadAsync<WorkItemDetails>();
    }

    [Fact]
    public async Task Task_markdown_file_can_be_corrected_in_the_app()
    {
        var client = factory.CreateClient();
        var item = await CreateAsync(client, "EDIT-1");
        Directory.CreateDirectory(item.WorkDirectory);
        await File.WriteAllTextAsync(Path.Combine(item.WorkDirectory, "plan.md"), "# Plan\nstary", Ct);

        var saved = await client.PutAsync($"/api/work-items/{item.Summary.Id}/files/plan.md", new FileContent("plan.md", "# Plan\nnowy zakres"));
        var read = await client.GetAsync<FileContent>($"/api/work-items/{item.Summary.Id}/files/plan.md");

        Assert.Equal(HttpStatusCode.NoContent, saved.StatusCode);
        Assert.Equal("# Plan\nnowy zakres", read.Content);
    }

    [Theory]
    [InlineData("skrypt.ps1")]
    [InlineData("..%5C..%5Cappsettings.md")]
    public async Task Only_markdown_files_directly_in_the_task_folder_can_be_written(string name)
    {
        var client = factory.CreateClient();
        var item = await CreateAsync(client, $"EDIT-{Math.Abs(name.GetHashCode()) % 100000}");
        Directory.CreateDirectory(item.WorkDirectory);

        var response = await client.PutAsync($"/api/work-items/{item.Summary.Id}/files/{name}", new FileContent(name, "x"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(Directory.GetFiles(item.WorkDirectory));
    }

    [Theory]
    [InlineData("Business")]
    [InlineData("Bug")]
    [InlineData("Technical")]
    public async Task Flows_have_a_what_next_review_right_after_implementation(string type)
    {
        var flows = await factory.CreateClient().GetAsync<FlowsResponse>("/api/flows");
        var steps = flows.Flows.Single(flow => flow.Type == type).Steps.ToList();

        var review = steps.Single(step => step.Id == "review-implementation");

        Assert.Empty(flows.Problems);
        Assert.Equal("implement", review.FollowUp);
        Assert.Equal("implement", steps[steps.IndexOf(review) - 1].Id);
    }

    [Fact]
    public async Task Generated_block_is_only_a_draft_until_the_user_saves_it()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsync("/api/blocks/generate", new GenerateBlockRequest(
            "agents", "przeglad-bezpieczenstwa", "Przegląda zmianę pod kątem bezpieczeństwa.", null));
        var draft = await response.ReadAsync<BlockDraft>();
        var library = await client.GetAsync<List<BlockFile>>("/api/blocks");

        Assert.Equal("agents/przeglad-bezpieczenstwa.agent.md", draft.Path);
        Assert.True(draft.Demo); // testy działają na atrapie agenta
        Assert.Contains("name: przeglad-bezpieczenstwa", draft.Content);
        Assert.DoesNotContain(library, block => block.Path == draft.Path);
    }
}