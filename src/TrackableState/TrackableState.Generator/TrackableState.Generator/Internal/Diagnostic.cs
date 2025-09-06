using Microsoft.CodeAnalysis;

namespace Klopoff.TrackableState.Generator.Internal;

internal static class Diagnostic
{
    public static readonly DiagnosticDescriptor ClassMustNotBeSealed = new(
        id: "TRK001",
        title: "Trackable class must not be sealed",
        messageFormat: "Class '{0}' is marked [Trackable] but is sealed",
        category: "Generator",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor PropertyMustBeVirtual = new(
        id: "TRK002",
        title: "Property must be virtual to allow tracking override",
        messageFormat: "Property '{0}' must be virtual to be overridden by trackable wrapper",
        category: "Generator",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}