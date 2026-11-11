using FlowHub.Application.Settings;
using FlowHub.Domain;
using NSubstitute;

namespace FlowHub.Application.Tests;

public sealed class SettingsServiceTests
{
    private readonly ISettingsStore _settings = Substitute.For<ISettingsStore>();
    private readonly ISecretStore _secrets = Substitute.For<ISecretStore>();
    private readonly SettingsService _service;

    public SettingsServiceTests()
    {
        _service = new SettingsService([new DemoModule()], _settings, _secrets);
    }

    [Fact]
    public void Secret_value_never_leaves_the_service()
    {
        _secrets.Has("Demo:Token").Returns(true);
        _secrets.Get("Demo:Token").Returns("tajne");

        var token = _service.List().Single().Fields.Single(field => field.Field.Key == "Demo:Token");

        Assert.True(token.HasValue);
        Assert.Null(token.Value);
    }

    [Fact]
    public void Plain_values_go_to_settings_and_secrets_to_the_secret_store()
    {
        _service.Save("demo", new Dictionary<string, string?> { ["Demo:Mode"] = "Real", ["Demo:Url"] = " https://x ", ["Demo:Token"] = "abc" });

        _settings.Received().Save(Arg.Is<IReadOnlyDictionary<string, string?>>(values =>
            values.Count == 2 && values["Demo:Mode"] == "Real" && values["Demo:Url"] == "https://x"));
        _secrets.Received().Set("Demo:Token", "abc");
    }

    [Fact]
    public void Secret_left_empty_in_the_form_is_not_overwritten()
    {
        _service.Save("demo", new Dictionary<string, string?> { ["Demo:Token"] = null });

        _secrets.DidNotReceiveWithAnyArgs().Set(default!, default);
    }

    [Theory]
    [InlineData("Demo:Mode", "Inny")]
    [InlineData("Demo:Limit", "-1")]
    [InlineData("Demo:Limit", "dużo")]
    [InlineData("Demo:Unknown", "x")]
    public void Invalid_values_are_rejected_with_a_readable_message(string key, string value)
    {
        Assert.Throws<DomainException>(() => _service.Save("demo", new Dictionary<string, string?> { [key] = value }));
        _settings.DidNotReceiveWithAnyArgs().Save(default!);
    }

    [Fact]
    public async Task Failed_connection_test_is_reported_instead_of_thrown()
    {
        var result = await _service.TestAsync("demo", TestContext.Current.CancellationToken);

        Assert.False(result.Ok);
        Assert.Equal("Brak sieci", result.Message);
    }

    private sealed class DemoModule : ISettingsModule
    {
        public string Id => "demo";
        public string Title => "Demo";
        public string Icon => "mdi-test-tube";
        public string Description => "";
        public bool CanTest => true;

        public IReadOnlyList<SettingField> Fields { get; } =
        [
            new("Demo:Mode", "Tryb", SettingKind.Choice, Choices: [new("Fake", "Atrapa"), new("Real", "Prawdziwy")]),
            new("Demo:Url", "Adres"),
            new("Demo:Limit", "Limit", SettingKind.Number),
            new("Demo:Token", "Token", SettingKind.Secret),
        ];

        public Task<ConnectionTest> TestAsync(CancellationToken ct) => throw new HttpRequestException("Brak sieci");
    }
}