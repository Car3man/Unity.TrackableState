using Microsoft.CodeAnalysis;

namespace CodeGenerator.Trackables.Internal;

internal static class TypeInspection
{
    public const string TrackableAttributeFullName = "Klopoff.TrackableState.TrackableAttribute";

    public static bool IsIList(ITypeSymbol? typeSymbol)
    {
        if (typeSymbol is INamedTypeSymbol nt)
        {
            if (nt.IsGenericType)
            {
                string def = nt.ConstructedFrom.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                if (def is "global::System.Collections.Generic.IList<T>" or "global::System.Collections.Generic.ICollection<T>")
                {
                    return true;
                }
            }
            foreach (INamedTypeSymbol? i in nt.AllInterfaces)
            {
                if (i.IsGenericType)
                {
                    string id = i.ConstructedFrom.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    if (id is "global::System.Collections.Generic.IList<T>" or "global::System.Collections.Generic.ICollection<T>")
                    {
                        return true;
                    }
                }
            }
        }
        return false;
    }

    public static bool IsIDictionary(ITypeSymbol? typeSymbol)
    {
        if (typeSymbol is INamedTypeSymbol nt)
        {
            if (nt.IsGenericType)
            {
                string def = nt.ConstructedFrom.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                if (def is "global::System.Collections.Generic.IDictionary<TKey, TValue>")
                {
                    return true;
                }
            }
            foreach (INamedTypeSymbol? i in nt.AllInterfaces)
            {
                if (i.IsGenericType)
                {
                    string id = i.ConstructedFrom.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    if (id is "global::System.Collections.Generic.IDictionary<TKey, TValue>")
                    {
                        return true;
                    }
                }
            }
        }
        return false;
    }

    public static bool IsISet(ITypeSymbol? typeSymbol)
    {
        if (typeSymbol is INamedTypeSymbol nt)
        {
            if (nt.IsGenericType)
            {
                string def = nt.ConstructedFrom.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                if (def is "global::System.Collections.Generic.ISet<T>")
                {
                    return true;
                }
            }
            foreach (INamedTypeSymbol? i in nt.AllInterfaces)
            {
                if (i.IsGenericType)
                {
                    string id = i.ConstructedFrom.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    if (id is "global::System.Collections.Generic.ISet<T>")
                    {
                        return true;
                    }
                }
            }
        }
        return false;
    }
    
    public static bool IsCollectionType(ITypeSymbol? type) => IsIDictionary(type) || IsIList(type) || IsISet(type);
    
    public static ITypeSymbol? GetCollectionElementType(ITypeSymbol? type)
    {
        if (IsIDictionary(type))
        {
            return type is INamedTypeSymbol nt ? nt.TypeArguments[1] : null;
        }
        if (IsIList(type) || IsISet(type))
        {
            return type is INamedTypeSymbol nt ? nt.TypeArguments[0] : null;
        }
        return null;
    }
    
    public static bool IsTrackableType(ITypeSymbol? typeSymbol)
    {
        if (typeSymbol is INamedTypeSymbol nt)
        {
            foreach (AttributeData? a in nt.GetAttributes())
            {
                if (a.AttributeClass?.ToDisplayString() == TrackableAttributeFullName)
                {
                    return true;
                }
            }
        }
        return false;
    }
    
    public static bool IsCollectionElementTypeTrackable(ITypeSymbol? typeSymbol)
    {
        return IsTrackableType(GetCollectionElementType(typeSymbol));
    }
}