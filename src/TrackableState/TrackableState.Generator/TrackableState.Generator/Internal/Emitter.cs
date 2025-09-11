using System.Linq;
using Microsoft.CodeAnalysis;

namespace Klopoff.TrackableState.Generator.Internal;

internal static class Emitter
{
    public static void EmitForClass(SourceProductionContext spc, INamedTypeSymbol classSymbol)
    {
        if (classSymbol.IsSealed)
        {
            spc.ReportDiagnostic(Microsoft.CodeAnalysis.Diagnostic.Create(
                descriptor: Diagnostic.ClassMustNotBeSealed,
                location: classSymbol.Locations.FirstOrDefault(),
                messageArgs: classSymbol.ToDisplayString()
            ));
            return;
        }

        IPropertySymbol[] propsVirtual = classSymbol.GetMembers()
            .OfType<IPropertySymbol>()
            .Where(p => !p.IsStatic && p.SetMethod is not null && p.GetMethod is not null && p.IsVirtual)
            .ToArray();

        IPropertySymbol[] notVirtual = classSymbol.GetMembers()
            .OfType<IPropertySymbol>()
            .Where(p => !p.IsStatic && p.SetMethod is not null && p.GetMethod is not null && !p.IsVirtual)
            .ToArray();

        foreach (IPropertySymbol p in notVirtual)
        {
            spc.ReportDiagnostic(Microsoft.CodeAnalysis.Diagnostic.Create(
                descriptor: Diagnostic.PropertyMustBeVirtual,
                location: p.Locations.FirstOrDefault(),
                messageArgs: $"{classSymbol.Name}.{p.Name}"
            ));
        }

        string? ns = classSymbol.ContainingNamespace.IsGlobalNamespace ? null : classSymbol.ContainingNamespace.ToDisplayString();
        string trackableName = "Trackable" + classSymbol.Name;
        string baseName = classSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        
        CodeWriter w = new CodeWriter();
        
        w.Usings(
            "System",
            "System.Collections.Generic",
            "System.Collections.Specialized",
            "System.ComponentModel",
            "System.Runtime.CompilerServices",
            "Klopoff.TrackableState.Core"
        );
        
        w.OptionalBlock(ns is null ? null : $"namespace {ns}", () =>
        {
            w.Block($"public sealed class {trackableName} : {baseName}, ITrackable", () =>
            {
                w.WriteLine("private readonly Dictionary<ITrackable, string> _children;");
                w.BlankLine();
                w.WriteLine("public event ChangeEventHandler Changed;");
                w.Block("public bool IsDirty", () =>
                {
                    w.WriteLine("[MethodImpl(MethodImplOptions.AggressiveInlining)]");
                    w.WriteLine("get;");
                    w.WriteLine("[MethodImpl(MethodImplOptions.AggressiveInlining)]");
                    w.WriteLine("private set;");
                });
                w.BlankLine();

                // overrides
                foreach (IPropertySymbol p in propsVirtual)
                {
                    EmitPropertyOverride(w, p);
                }
                
                // ctors
                w.Block($"public {trackableName}()", () =>
                {
                    w.WriteLine("_children = new Dictionary<ITrackable, string>();");
                    w.BlankLine();
                    w.WriteLine("IsDirty = false;");
                });
                w.BlankLine();
                
                w.Block($"public {trackableName}({baseName} source)", () =>
                {
                    w.WriteLine("_children = new Dictionary<ITrackable, string>();");
                    w.BlankLine();
                    w.WriteLine("IsDirty = false;");
                    w.BlankLine();
                    foreach (IPropertySymbol p in propsVirtual)
                    {
                        EmitAssignmentForConstructor(w, p, srcVar: "source");
                    }
                    w.BlankLine();
                    foreach (IPropertySymbol p in propsVirtual)
                    {
                        EmitAttachmentForConstructor(w, p);
                    }
                });
                w.BlankLine();
                
                // normalizer
                w.Block($"public {baseName} Normalize()", () =>
                {
                    w.Block($"return new {baseName}", () =>
                    {
                        foreach (IPropertySymbol p in propsVirtual)
                        {
                            EmitAssignmentForNormalizer(w, p);
                        }
                    }, suffix: ";");
                });
                w.BlankLine();
                
                // accept changes
                w.Block("public void AcceptChanges()", () =>
                {
                    w.Block("foreach (var c in _children.Keys)", () =>
                    {
                        w.WriteLine("c.AcceptChanges();");
                    });
                    w.BlankLine();
                    w.WriteLine("IsDirty = false;");
                });
                w.BlankLine();
                
                // child attach
                w.WriteLine("[MethodImpl(MethodImplOptions.AggressiveInlining)]");
                w.Block("public void AttachChild(string memberName, ITrackable child)", () =>
                {
                    w.Block("if (_children.TryAdd(child, memberName))", () =>
                    {
                        w.WriteLine("child.Changed += OnChange;");
                    });
                });
                w.BlankLine();
                
                // child deattach
                w.WriteLine("[MethodImpl(MethodImplOptions.AggressiveInlining)]");
                w.Block("public void DetachChild(ITrackable child)", () =>
                {
                    w.Block("if (_children.Remove(child))", () =>
                    {
                        w.WriteLine("child.Changed -= OnChange;");
                    });
                });
                w.BlankLine();
                
                // child changed handler
                w.WriteLine("[MethodImpl(MethodImplOptions.AggressiveInlining)]");
                w.Block("public void OnChange(object sender, in ChangeEventArgs args)", () =>
                {
                    w.WriteLine("IsDirty = true;");
                    w.WriteLine("Changed?.Invoke(this, ChangeEventArgs.ChildProperty(args, _children[(ITrackable)sender]));");
                });
            });
            w.BlankLine();

            w.Block($"public static class {classSymbol.Name}TrackableExtensions", () =>
            {
                w.Block($"public static {trackableName} AsTrackable(this {baseName} source)", () =>
                {
                    w.Block($"if (source is {trackableName} t)", () =>
                    {
                        w.WriteLine("return t;");
                    });
                    w.WriteLine($"return new {trackableName}(source);");
                });
                w.BlankLine();

                w.Block($"public static {baseName} AsNormal(this {baseName} source)", () =>
                {
                    w.Block($"if (source is {trackableName} t)", () =>
                    {
                        w.WriteLine("return t.Normalize();");
                    });
                    w.WriteLine("return source;");
                });
            });
        });

        spc.AddSource(hintName: $"{trackableName}.g.cs", source: w.ToString());
    }

    private static void EmitPropertyOverride(CodeWriter w, IPropertySymbol p)
    {
        string typeName = p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        string name = p.Name;

        if (TypeInspection.IsCollectionType(p.Type))
        {
            string call = CallAsTrackableCollection("value", p.Type);
            
            w.Block($"public override {typeName} {name}", () =>
            {
                w.WriteLine("[MethodImpl(MethodImplOptions.AggressiveInlining)]");
                w.WriteLine($"get => base.{name};");
                w.WriteLine("[MethodImpl(MethodImplOptions.AggressiveInlining)]");
                w.Block("set", () =>
                {
                    w.Block($"if (!EqualityComparer<{typeName}>.Default.Equals(base.{name}, value))", () =>
                    {
                        w.WriteLine($"{typeName} oldValue = base.{name};");
                        w.Block("if (oldValue is ITrackable ot)", () =>
                        {
                            w.WriteLine("DetachChild(ot);");
                        });
                        w.WriteLine($"{typeName} newValue = value is null ? null : {call};");
                        w.WriteLine($"base.{name} = newValue;");
                        w.Block("if (newValue is ITrackable nt)", () =>
                        {
                            w.WriteLine($"AttachChild(\"{name}\", nt);");
                        });
                        w.WriteLine("IsDirty = true;");
                        w.WriteLine($"Changed?.Invoke(this, ChangeEventArgs.PropertySet(\"{name}\", Payload24.From(oldValue), Payload24.From(value)));");
                    });
                });
            });
        }
        else if (TypeInspection.IsTrackableType(p.Type))
        {
            string call = CallAsTrackableOnType("value", p.Type);
            
            w.Block($"public override {typeName} {name}", () =>
            {
                w.WriteLine("[MethodImpl(MethodImplOptions.AggressiveInlining)]");
                w.WriteLine($"get => base.{name};");
                w.WriteLine("[MethodImpl(MethodImplOptions.AggressiveInlining)]");
                w.Block("set", () =>
                {
                    w.Block($"if (!EqualityComparer<{typeName}>.Default.Equals(base.{name}, value))", () =>
                    {
                        w.WriteLine($"{typeName} oldValue = base.{name};");
                        w.Block("if (oldValue is ITrackable ot)", () =>
                        {
                            w.WriteLine("DetachChild(ot);");
                        });
                        w.WriteLine($"{typeName} newValue = value is null ? null : {call};");
                        w.WriteLine($"base.{name} = newValue;");
                        w.Block("if (newValue is ITrackable nt)", () =>
                        {
                            w.WriteLine($"AttachChild(\"{name}\", nt);");
                        });
                        w.WriteLine("IsDirty = true;");
                        w.WriteLine($"Changed?.Invoke(this, ChangeEventArgs.PropertySet(\"{name}\", Payload24.From(oldValue), Payload24.From(value)));");
                    });
                });
            });
        }
        else
        {
            w.Block($"public override {typeName} {name}", () =>
            {
                w.WriteLine("[MethodImpl(MethodImplOptions.AggressiveInlining)]");
                w.WriteLine($"get => base.{name};");
                w.WriteLine("[MethodImpl(MethodImplOptions.AggressiveInlining)]");
                w.Block("set", () =>
                {
                    w.Block($"if (!EqualityComparer<{typeName}>.Default.Equals(base.{name}, value))", () =>
                    {
                        w.WriteLine($"{typeName} oldValue = base.{name};");
                        w.WriteLine($"base.{name} = value;");
                        w.WriteLine("IsDirty = true;");
                        w.WriteLine($"Changed?.Invoke(this, ChangeEventArgs.PropertySet(\"{name}\", Payload24.From(oldValue), Payload24.From(value)));");
                    });
                });
            });
        }
    }

    private static void EmitAssignmentForConstructor(CodeWriter w, IPropertySymbol p, string srcVar)
    {
        if (TypeInspection.IsCollectionType(p.Type))
        {
            string call = CallAsTrackableCollection($"{srcVar}.{p.Name}", p.Type);
            w.WriteLine($"base.{p.Name} = {srcVar}.{p.Name} is null ? null : {call};");
        }
        else if (TypeInspection.IsTrackableType(p.Type))
        {
            string call = CallAsTrackableOnType($"{srcVar}.{p.Name}", p.Type);
            w.WriteLine($"base.{p.Name} = {srcVar}.{p.Name} is null ? null : {call};");
        }
        else
        {
            w.WriteLine($"base.{p.Name} = {srcVar}.{p.Name};");
        }
    }
    
    private static void EmitAttachmentForConstructor(CodeWriter w, IPropertySymbol p)
    {
        if (TypeInspection.IsTrackableType(p.Type) || TypeInspection.IsCollectionType(p.Type))
        {
            w.Block($"if (base.{p.Name} is ITrackable t_{p.Name})", () =>
            {
                w.WriteLine($"AttachChild(\"{p.Name}\", t_{p.Name});");
            });
        }
    }

    private static void EmitAssignmentForNormalizer(CodeWriter w, IPropertySymbol p)
    {
        if (TypeInspection.IsCollectionType(p.Type) || TypeInspection.IsTrackableType(p.Type))
        {
            string call = CallAsNormalOnType($"{p.Name}", p.Type);
            w.WriteLine($"{p.Name} = {p.Name} is null ? null : {call},");
        }
        else
        {
            w.WriteLine($"{p.Name} = {p.Name},");
        }
    }

    private static string CallAsTrackableOnType(string expr, ITypeSymbol typeSymbol)
    {
        return $"{GetExtensionClassFqn(typeSymbol)}.AsTrackable({expr})";
    }
    
    private static string CallAsTrackableCollection(string expr, ITypeSymbol collectionType)
    {
        ITypeSymbol typeSymbol = TypeInspection.GetCollectionElementType(collectionType)!;
        bool elementIsTrackable = TypeInspection.IsTrackableType(typeSymbol);
        string wrapperLambda = elementIsTrackable
            ? $"static x => x is null ? null : {CallAsTrackableOnType("x", typeSymbol)}"
            : "static x => x";
        string unwrapperLambda = elementIsTrackable
            ? $"static x => x is null ? null : {CallAsNormalOnType("x", typeSymbol)}"
            : "static x => x";
        return $"{expr}.AsTrackable({wrapperLambda}, {unwrapperLambda})";
    }
    
    private static string CallAsNormalOnType(string expr, ITypeSymbol typeSymbol)
    {
        return $"{GetExtensionClassFqn(typeSymbol)}.AsNormal({expr})";
    }
    
    private static string GetExtensionClassFqn(ITypeSymbol typeSymbol)
    {
        if (TypeInspection.IsIDictionary(typeSymbol)) return "global::Klopoff.TrackableState.Core.TrackableDictionaryExtensions";
        if (TypeInspection.IsISet(typeSymbol)) return "global::Klopoff.TrackableState.Core.TrackableSetExtensions";
        if (TypeInspection.IsIList(typeSymbol)) return "global::Klopoff.TrackableState.Core.TrackableListExtensions";

        INamespaceSymbol? nsSymbol = typeSymbol.ContainingNamespace;
        string typeSimpleName = typeSymbol.Name;
        
        if (nsSymbol is null || nsSymbol.IsGlobalNamespace)
        {
            return $"global::{typeSimpleName}TrackableExtensions";
        }
        
        string nsFqn = nsSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        return $"{nsFqn}.{typeSimpleName}TrackableExtensions";
    }
}