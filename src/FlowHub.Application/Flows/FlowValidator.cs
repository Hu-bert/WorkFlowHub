using System.Text.RegularExpressions;
using FlowHub.Domain.Flows;

namespace FlowHub.Application.Flows;

/// <summary>Sprawdza dokument flow i zwraca czytelne komunikaty błędów (pusta lista = poprawny).</summary>
public static partial class FlowValidator
{
    public static IReadOnlyList<string> Validate(FlowDocument flow)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(flow.Type) || !TypePattern().IsMatch(flow.Type))
        {
            errors.Add("Pole 'type' jest wymagane: litery i cyfry bez spacji, np. Bug.");
        }

        if (string.IsNullOrWhiteSpace(flow.Name))
        {
            errors.Add("Pole 'name' jest wymagane.");
        }

        if (flow.Flow.Count == 0)
        {
            errors.Add("Flow musi mieć co najmniej jeden krok (lista 'flow').");
        }

        for (var index = 0; index < flow.Flow.Count; index++)
        {
            errors.AddRange(ValidateStep(flow.Flow[index], index + 1));
        }

        errors.AddRange(DuplicateIds(flow));
        return errors;
    }

    public static bool TryParseKind(string? text, out StepKind kind) =>
        Enum.TryParse(text, ignoreCase: true, out kind) && Enum.IsDefined(kind);

    private static IEnumerable<string> ValidateStep(StepDocument step, int position)
    {
        var label = string.IsNullOrWhiteSpace(step.Id) ? $"Krok nr {position}" : $"Krok '{step.Id}'";

        if (string.IsNullOrWhiteSpace(step.Id) || !IdPattern().IsMatch(step.Id))
        {
            yield return $"{label}: pole 'id' jest wymagane (małe litery, cyfry i myślniki, np. create-folder).";
        }

        if (!TryParseKind(step.Kind, out var kind))
        {
            yield return $"{label}: nieznany 'kind: {step.Kind}'. Dozwolone: {string.Join(", ", Enum.GetNames<StepKind>())}.";
            yield break;
        }

        if (MissingRequiredField(step, kind) is { } missing)
        {
            yield return $"{label}: brak pola '{missing}' (wymagane dla kind: {kind}).";
        }

        if (step.TokenBudget < 0)
        {
            yield return $"{label}: 'tokenBudget' nie może być ujemny.";
        }

        if (step.Model is { } model && string.IsNullOrWhiteSpace(model.Replace("tier:", "")))
        {
            yield return $"{label}: pole 'model' jest puste. Podaj np. tier:cheap albo id modelu.";
        }
    }

    private static string? MissingRequiredField(StepDocument step, StepKind kind) => kind switch
    {
        StepKind.Script when string.IsNullOrWhiteSpace(step.Script) => "script",
        StepKind.Agent when string.IsNullOrWhiteSpace(step.Agent) && string.IsNullOrWhiteSpace(step.Prompt) => "agent lub prompt",
        StepKind.Check when string.IsNullOrWhiteSpace(step.Check) => "check",
        _ => null,
    };

    private static IEnumerable<string> DuplicateIds(FlowDocument flow) =>
        flow.Flow
            .Where(step => !string.IsNullOrWhiteSpace(step.Id))
            .GroupBy(step => step.Id)
            .Where(group => group.Count() > 1)
            .Select(group => $"Id kroku '{group.Key}' powtarza się {group.Count()} razy — musi być unikalne.");

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9]*$")]
    private static partial Regex TypePattern();

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex IdPattern();
}
