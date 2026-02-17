using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
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
        
        CodeWriter w = new();
        
        w.OptionalBlock(ns is null ? null : $"namespace {ns}", () =>
        {
            if (ShouldCopyAttributes(classSymbol))
            {
                EmitAttributes(w, classSymbol.GetAttributes());
            }
            
            w.Block($"public sealed class {trackableName} : {baseName}, global::Klopoff.TrackableState.Core.ITrackable", () =>
            {
                w.WriteLine("private readonly global::System.Collections.Generic.Dictionary<global::Klopoff.TrackableState.Core.ITrackable, global::Klopoff.TrackableState.Core.MemberInfo> _children;");
                w.BlankLine();
                w.WriteLine("public event global::Klopoff.TrackableState.Core.ChangeEventHandler Changed;");
                w.BlankLine();
                w.WriteLine("[global::System.Runtime.Serialization.IgnoreDataMemberAttribute]");
                w.Block("public bool IsDirty", () =>
                {
                    w.WriteLine("[global::System.Runtime.CompilerServices.MethodImplAttribute(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
                    w.WriteLine("get;");
                    w.WriteLine("[global::System.Runtime.CompilerServices.MethodImplAttribute(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
                    w.WriteLine("private set;");
                });
                w.BlankLine();

                // overrides
                foreach (IPropertySymbol p in propsVirtual)
                {
                    EmitPropertyOverride(w, p);
                }
                
                // prop id const
                for (int i = 0; i < propsVirtual.Length; i++)
                {
                    IPropertySymbol p = propsVirtual[i];
                    EmitPropertyIdConst(w, p, i + 1);
                }
                w.BlankLine();
                
                // ctors
                w.Block($"public {trackableName}()", () =>
                {
                    w.WriteLine("_children = new global::System.Collections.Generic.Dictionary<global::Klopoff.TrackableState.Core.ITrackable, global::Klopoff.TrackableState.Core.MemberInfo>();");
                    w.BlankLine();
                    w.WriteLine("IsDirty = false;");
                });
                w.BlankLine();
                
                w.Block($"public {trackableName}({baseName} source)", () =>
                {
                    w.WriteLine("_children = new global::System.Collections.Generic.Dictionary<global::Klopoff.TrackableState.Core.ITrackable, global::Klopoff.TrackableState.Core.MemberInfo>();");
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
                w.WriteLine("[global::System.Runtime.CompilerServices.MethodImplAttribute(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
                w.Block("public void AttachChild(in global::Klopoff.TrackableState.Core.MemberInfo member, global::Klopoff.TrackableState.Core.ITrackable child)", () =>
                {
                    w.Block("if (_children.TryAdd(child, member))", () =>
                    {
                        w.WriteLine("child.Changed += OnChange;");
                    });
                });
                w.BlankLine();
                
                // child deattach
                w.WriteLine("[global::System.Runtime.CompilerServices.MethodImplAttribute(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
                w.Block("public void DetachChild(global::Klopoff.TrackableState.Core.ITrackable child)", () =>
                {
                    w.Block("if (_children.Remove(child))", () =>
                    {
                        w.WriteLine("child.Changed -= OnChange;");
                    });
                });
                w.BlankLine();
                
                // child changed handler
                w.WriteLine("[global::System.Runtime.CompilerServices.MethodImplAttribute(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
                w.Block("public void OnChange(object sender, in global::Klopoff.TrackableState.Core.ChangeEventArgs args)", () =>
                {
                    w.WriteLine("IsDirty = true;");
                    w.WriteLine("Changed?.Invoke(this, global::Klopoff.TrackableState.Core.ChangeEventArgs.ChildProperty(args, _children[(global::Klopoff.TrackableState.Core.ITrackable)sender]));");
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
    
    private static void EmitPropertyIdConst(CodeWriter w, IPropertySymbol p, int id)
    {
        w.WriteLine($"internal const int {p.Name}Id = {id};");
    }

    private static void EmitPropertyOverride(CodeWriter w, IPropertySymbol p)
    {
        if (ShouldCopyAttributes(p))
        {
            EmitAttributes(w, p.GetAttributes());
        }
        
        string typeName = p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        string name = p.Name;

        if (TypeInspection.IsCollectionType(p.Type))
        {
            string call = CallAsTrackableCollection("value", p.Type);
            
            w.Block($"public override {typeName} {name}", () =>
            {
                w.WriteLine("[global::System.Runtime.CompilerServices.MethodImplAttribute(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
                w.WriteLine($"get => base.{name};");
                w.WriteLine("[global::System.Runtime.CompilerServices.MethodImplAttribute(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
                w.Block("set", () =>
                {
                    w.Block($"if (!global::System.Collections.Generic.EqualityComparer<{typeName}>.Default.Equals(base.{name}, value))", () =>
                    {
                        w.WriteLine($"{typeName} oldValue = base.{name};");
                        w.Block("if (oldValue is global::Klopoff.TrackableState.Core.ITrackable ot)", () =>
                        {
                            w.WriteLine("DetachChild(ot);");
                        });
                        w.WriteLine($"{typeName} newValue = value is null ? null : {call};");
                        w.WriteLine($"base.{name} = newValue;");
                        w.Block("if (newValue is global::Klopoff.TrackableState.Core.ITrackable nt)", () =>
                        {
                            w.WriteLine($"AttachChild(new global::Klopoff.TrackableState.Core.MemberInfo({name}Id, \"{name}\"), nt);");
                        });
                        w.WriteLine("IsDirty = true;");
                        w.WriteLine($"Changed?.Invoke(this, global::Klopoff.TrackableState.Core.ChangeEventArgs.PropertySet(new global::Klopoff.TrackableState.Core.MemberInfo({name}Id, \"{name}\"), global::Klopoff.TrackableState.Core.Payload24.From(oldValue), global::Klopoff.TrackableState.Core.Payload24.From(value)));");
                    });
                });
            });
        }
        else if (TypeInspection.IsTrackableType(p.Type))
        {
            string call = CallAsTrackableOnType("value", p.Type);
            
            w.Block($"public override {typeName} {name}", () =>
            {
                w.WriteLine("[global::System.Runtime.CompilerServices.MethodImplAttribute(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
                w.WriteLine($"get => base.{name};");
                w.WriteLine("[global::System.Runtime.CompilerServices.MethodImplAttribute(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
                w.Block("set", () =>
                {
                    w.Block($"if (!global::System.Collections.Generic.EqualityComparer<{typeName}>.Default.Equals(base.{name}, value))", () =>
                    {
                        w.WriteLine($"{typeName} oldValue = base.{name};");
                        w.Block("if (oldValue is global::Klopoff.TrackableState.Core.ITrackable ot)", () =>
                        {
                            w.WriteLine("DetachChild(ot);");
                        });
                        w.WriteLine($"{typeName} newValue = value is null ? null : {call};");
                        w.WriteLine($"base.{name} = newValue;");
                        w.Block("if (newValue is global::Klopoff.TrackableState.Core.ITrackable nt)", () =>
                        {
                            w.WriteLine($"AttachChild(new global::Klopoff.TrackableState.Core.MemberInfo({name}Id, \"{name}\"), nt);");
                        });
                        w.WriteLine("IsDirty = true;");
                        w.WriteLine($"Changed?.Invoke(this, global::Klopoff.TrackableState.Core.ChangeEventArgs.PropertySet(new global::Klopoff.TrackableState.Core.MemberInfo({name}Id, \"{name}\"), global::Klopoff.TrackableState.Core.Payload24.From(oldValue), global::Klopoff.TrackableState.Core.Payload24.From(value)));");
                    });
                });
            });
        }
        else
        {
            w.Block($"public override {typeName} {name}", () =>
            {
                w.WriteLine("[global::System.Runtime.CompilerServices.MethodImplAttribute(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
                w.WriteLine($"get => base.{name};");
                w.WriteLine("[global::System.Runtime.CompilerServices.MethodImplAttribute(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
                w.Block("set", () =>
                {
                    w.Block($"if (!global::System.Collections.Generic.EqualityComparer<{typeName}>.Default.Equals(base.{name}, value))", () =>
                    {
                        w.WriteLine($"{typeName} oldValue = base.{name};");
                        w.WriteLine($"base.{name} = value;");
                        w.WriteLine("IsDirty = true;");
                        w.WriteLine($"Changed?.Invoke(this, global::Klopoff.TrackableState.Core.ChangeEventArgs.PropertySet(new global::Klopoff.TrackableState.Core.MemberInfo({name}Id, \"{name}\"), global::Klopoff.TrackableState.Core.Payload24.From(oldValue), global::Klopoff.TrackableState.Core.Payload24.From(value)));");
                    });
                });
            });
        }
        
        w.BlankLine();
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
            w.Block($"if (base.{p.Name} is global::Klopoff.TrackableState.Core.ITrackable t_{p.Name})", () =>
            {
                w.WriteLine($"AttachChild(new global::Klopoff.TrackableState.Core.MemberInfo({p.Name}Id, \"{p.Name}\"), t_{p.Name});");
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
    
    private static void EmitAttributes(CodeWriter w, ImmutableArray<AttributeData> attributes)
    {
        foreach (AttributeData? attr in attributes)
        {
            INamedTypeSymbol? type = attr.AttributeClass;
            if (type == null)
            {
                continue;
            }

            if (IsTrackableAttribute(type))
            {
                continue;
            }

            string typeName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

            List<string> args = [];

            foreach (TypedConstant a in attr.ConstructorArguments)
            {
                args.Add(RenderTypedConstant(a));
            }

            foreach (KeyValuePair<string, TypedConstant> n in attr.NamedArguments)
            {
                args.Add($"{n.Key} = {RenderTypedConstant(n.Value)}");
            }

            if (args.Count == 0)
            {
                w.WriteLine($"[{typeName}]");
            }
            else
            {
                w.WriteLine($"[{typeName}({string.Join(", ", args)})]");
            }
        }
    }
    
    private static string RenderTypedConstant(TypedConstant c)
    {
        if (c.IsNull)
        {
            return "null";
        }

        switch (c.Kind)
        {
            case TypedConstantKind.Primitive:
            {

                return c.Value switch
                {
                    string s => $"\"{EscapeString(s)}\"",
                    char ch => $"'{ch}'",
                    bool b => b ? "true" : "false",
                    float f => f.ToString("R", CultureInfo.InvariantCulture) + "f",
                    double d => d.ToString("R", CultureInfo.InvariantCulture),
                    decimal m => m.ToString(CultureInfo.InvariantCulture) + "m",
                    IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
                    _ => c.Value!.ToString()!
                };
            }
            case TypedConstantKind.Enum:
            {
                string enumType = c.Type!.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                return $"{enumType}.{c.Value}";
            }
            case TypedConstantKind.Type:
            {
                return $"typeof({c.Value})";
            }
            case TypedConstantKind.Array:
            {
                IEnumerable<string> values = c.Values.Select(RenderTypedConstant);
                string type = c.Type!.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                return $"new {type} {{ {string.Join(", ", values)} }}";
            }
            default: 
                return c.Value?.ToString() ?? "null";
        }
    }
    
    private static string EscapeString(string s)
    {
        return s
            .Replace("\\", @"\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");
    }
    
    private static bool ShouldCopyAttributes(ISymbol symbol)
    {
        AttributeData? attr = symbol
            .GetAttributes()
            .FirstOrDefault(a => IsTrackableAttribute(a.AttributeClass));

        if (attr == null)
        {
            return true;
        }

        foreach (KeyValuePair<string, TypedConstant> named in attr.NamedArguments)
        {
            if (named is { Key: "CopyAttributes", Value.Value: bool b })
            {
                return b;
            }
        }

        return true;
    }
    
    private static bool IsTrackableAttribute(INamedTypeSymbol? symbol)
    {
        if (symbol == null)
        {
            return false;
        }

        return symbol.Name == "TrackableAttribute" && 
               symbol.ContainingNamespace.ToDisplayString() == "Klopoff.TrackableState.Core";
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
        return $"{GetExtensionClassFqn(collectionType)}.AsTrackable({expr}, {wrapperLambda}, {unwrapperLambda})";
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