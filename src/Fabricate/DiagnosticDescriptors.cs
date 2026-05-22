using Microsoft.CodeAnalysis;

namespace Fabricate;

public static class DiagnosticDescriptors
{
    public static readonly DiagnosticDescriptor MissingValidInstance = new(
        id: "FAB001",
        title: "Missing ValidInstance implementation",
        messageFormat: "Builder '{0}' must implement 'static partial {1} ValidInstance()' to provide default values",
        category: "Fabricate",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor BuilderNamingSuggestion = new(
        id: "FAB002",
        title: "Builder class naming suggestion",
        messageFormat: "Builder class '{0}' does not end with 'Builder' - consider renaming to '{1}Builder' to avoid namespace conflicts",
        category: "Fabricate",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor CannotResolveConstructor = new(
        id: "FAB003",
        title: "Cannot resolve constructor",
        messageFormat: "Cannot find a suitable constructor for type '{0}' - ensure the type has a public constructor whose parameters can be matched to its properties",
        category: "Fabricate",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}
