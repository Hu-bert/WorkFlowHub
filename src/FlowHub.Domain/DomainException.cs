namespace FlowHub.Domain;

/// <summary>Złamana reguła domeny. Komunikat jest czytelny dla użytkownika.</summary>
public sealed class DomainException(string message) : Exception(message);
