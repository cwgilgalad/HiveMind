using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace HiveMind.Analyzers;

/// <summary>
/// HM0001: reports a method declared with <c>DllImport</c> or <c>LibraryImport</c> whose entry point
/// is on <see cref="BannedNativeApis"/> (spec section 8.2).
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class BannedNativeApiAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The diagnostic id.</summary>
    public const string DiagnosticId = "HM0001";

    private const string DllImportAttributeName = "System.Runtime.InteropServices.DllImportAttribute";
    private const string LibraryImportAttributeName = "System.Runtime.InteropServices.LibraryImportAttribute";
    private const string EntryPointArgumentName = "EntryPoint";

    private static readonly DiagnosticDescriptor _rule = new(
        DiagnosticId,
        title: "Banned native API",
        messageFormat: "'{0}' is banned in HiveMind: {1}",
        category: "Security",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "HiveMind never borrows tokens, tampers with other processes, duplicates their handles, resolves APIs dynamically, dumps process memory or installs services (spec section 8.2).");

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(_rule);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        // Generated code is analyzed too, so a banned import can't arrive through CsWin32's output.
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(AnalyzeMethod, SymbolKind.Method);

        // Symbol actions never see local functions, and C# accepts a static extern local function
        // with DllImport, so local functions are checked from the operation tree.
        context.RegisterOperationAction(AnalyzeLocalFunction, OperationKind.LocalFunction);
    }

    private static void AnalyzeMethod(SymbolAnalysisContext context) =>
        Analyze((IMethodSymbol)context.Symbol, context.ReportDiagnostic);

    private static void AnalyzeLocalFunction(OperationAnalysisContext context) =>
        Analyze(((ILocalFunctionOperation)context.Operation).Symbol, context.ReportDiagnostic);

    private static void Analyze(IMethodSymbol method, Action<Diagnostic> report)
    {
        // The implementing half of a partial method shares the definition's attributes. Report once,
        // on the definition, which carries the import.
        if (method.PartialDefinitionPart is not null)
        {
            return;
        }

        foreach (var attribute in method.GetAttributes())
        {
            if (!IsImportAttribute(attribute.AttributeClass))
            {
                continue;
            }

            var entryPoint = GetEntryPoint(attribute) ?? method.Name;
            if (BannedNativeApis.TryGetReason(entryPoint, out var reason))
            {
                report(Diagnostic.Create(_rule, method.Locations[0], entryPoint, reason));
                return;
            }
        }
    }

    private static bool IsImportAttribute(INamedTypeSymbol? attributeClass)
    {
        var name = attributeClass?.ToDisplayString();
        return name is DllImportAttributeName or LibraryImportAttributeName;
    }

    private static string? GetEntryPoint(AttributeData attribute)
    {
        foreach (var argument in attribute.NamedArguments)
        {
            if (argument.Key == EntryPointArgumentName && argument.Value.Value is string entryPoint)
            {
                return entryPoint;
            }
        }

        return null;
    }
}
