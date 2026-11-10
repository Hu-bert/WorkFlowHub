using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace FlowHub.Infrastructure.Yaml;

/// <summary>Wspólne ustawienia YAML: klucze camelCase, nieznany klucz = błąd (literówki nie przechodzą po cichu).</summary>
internal static class YamlFormat
{
    public static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    public static readonly ISerializer Serializer = new SerializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull | DefaultValuesHandling.OmitEmptyCollections)
        .Build();

    public static string Describe(YamlException error) =>
        $"Błąd składni YAML (linia {error.Start.Line}, kolumna {error.Start.Column}): {error.InnerException?.Message ?? error.Message}";
}
