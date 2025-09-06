using System.Collections.Immutable;
using System.Linq;
using Klopoff.TrackableState.Generator.Internal;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Klopoff.TrackableState.Generator;

[Generator(LanguageNames.CSharp)]
public sealed class TrackableGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        IncrementalValuesProvider<INamedTypeSymbol> candidates = context.SyntaxProvider.CreateSyntaxProvider(
            predicate: static (node, _) => node is ClassDeclarationSyntax { AttributeLists.Count: > 0 },
            transform: static (ctx, ct) =>
            {
                ClassDeclarationSyntax cds = (ClassDeclarationSyntax)ctx.Node;
                
                if (ctx.SemanticModel.GetDeclaredSymbol(cds, ct) is not INamedTypeSymbol symbol)
                {
                    return null;
                }
                
                foreach (AttributeData? a in symbol.GetAttributes())
                {
                    if (a.AttributeClass?.ToDisplayString() == TypeInspection.TrackableAttributeFullName)
                    {
                        return symbol;
                    }
                }
                
                return null;
            })
            .Where(static s => s is not null)
            .Select(static (s, _) => s!)
            .WithComparer(SymbolEqualityComparer.Default);

        IncrementalValueProvider<Compilation> compilationProvider = context.CompilationProvider;
        IncrementalValueProvider<(ImmutableArray<INamedTypeSymbol> Left, Compilation Right)> combined = candidates.Collect().Combine(compilationProvider);

        context.RegisterSourceOutput(combined, static (spc, pair) =>
        {
            (ImmutableArray<INamedTypeSymbol> classes, Compilation _) = pair;
            if (classes.IsDefaultOrEmpty)
            {
                return;
            }

            foreach (INamedTypeSymbol symbol in classes.Distinct(SymbolEqualityComparer.Default).Cast<INamedTypeSymbol>())
            {
                Emitter.EmitForClass(spc, symbol);
            }
        });
    }
}