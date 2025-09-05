using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;

namespace TrackableStateCodeGenerator.Internal;

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
        
        StringBuilder sb = new StringBuilder();

        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Collections.Specialized;");
        sb.AppendLine("using System.ComponentModel;");
        sb.AppendLine("using Klopoff.TrackableState;");
        sb.AppendLine();
        
        if (!string.IsNullOrEmpty(ns))
        {
            sb.AppendLine("namespace " + ns);
            sb.AppendLine("{");
        }

        sb.AppendLine($"    public partial class {trackableName} : {baseName}, ITrackable");
        sb.AppendLine("    {");
        sb.AppendLine("        private readonly Dictionary<ITrackable, string> _childTrackables = new();");
        sb.AppendLine();
        sb.AppendLine("        public bool IsDirty { get; private set; }");
        sb.AppendLine("        public event EventHandler<ChangeEventArgs> Changed;");
        sb.AppendLine();
        
        // overrides
        foreach (IPropertySymbol p in propsVirtual)
        {
            EmitPropertyOverride(sb, p);
        }

        // ctors
        sb.AppendLine($"        public {trackableName}() {{ }}");
        sb.AppendLine();
        sb.AppendLine($"        public {trackableName}({baseName} source)");
        sb.AppendLine("        {");
        foreach (IPropertySymbol p in propsVirtual)
        {
            EmitAssignmentForConstructor(sb, p, srcVar: "source");
        }
        sb.AppendLine("        }");
        sb.AppendLine();

        // AcceptChanges
        sb.AppendLine("        public void AcceptChanges()");
        sb.AppendLine("        {");
        sb.AppendLine("            foreach (var c in _childTrackables.Keys)");
        sb.AppendLine("            {");
        sb.AppendLine("                c.AcceptChanges();");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            IsDirty = false;");
        sb.AppendLine("        }");
        sb.AppendLine();

        // helpers
        sb.AppendLine("        protected void OnChange(ChangeEventArgs args)");
        sb.AppendLine("        {");
        sb.AppendLine("            IsDirty = true;");
        sb.AppendLine("            Changed?.Invoke(this, args);");
        sb.AppendLine("        }");
        sb.AppendLine();

        // child attach/detach
        sb.AppendLine("        private void AttachChild(string memberName, ITrackable child)");
        sb.AppendLine("        {");
        sb.AppendLine("            if (_childTrackables.ContainsKey(child))");
        sb.AppendLine("            {");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine("            _childTrackables.Add(child, memberName);");
        sb.AppendLine("            child.Changed += ChildChanged;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        private void DetachChild(string memberName, ITrackable child)");
        sb.AppendLine("        {");
        sb.AppendLine("            if (_childTrackables.Remove(child))");
        sb.AppendLine("            {");
        sb.AppendLine("                child.Changed -= ChildChanged;");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        private void ChildChanged(object sender, ChangeEventArgs e)");
        sb.AppendLine("        {");
        sb.AppendLine("            string memberName = _childTrackables[(ITrackable)sender];");
        sb.AppendLine("            OnChange(new ChangeEventArgs(PathSegmentType.Property, ChangeKind.ChildChange, memberName, e.OldValue, e.NewValue, e.Index, e.Key, e));");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine($"    public static class {classSymbol.Name}TrackableExtensions");
        sb.AppendLine("    {");
        sb.AppendLine($"        public static {trackableName} AsTrackable(this {baseName} source)");
        sb.AppendLine("        {");
        sb.AppendLine($"            if (source is {trackableName} t)");
        sb.AppendLine("            {");
        sb.AppendLine("                return t;");
        sb.AppendLine("            }");
        sb.AppendLine($"            return new {trackableName}(source);");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        
        if (!string.IsNullOrEmpty(ns))
        {
            sb.AppendLine("}");
        }

        spc.AddSource(hintName: $"{trackableName}.g.cs", source: sb.ToString());
    }

    private static void EmitAssignmentForConstructor(StringBuilder sb, IPropertySymbol p, string srcVar)
    {
        if (TypeInspection.IsCollectionType(p.Type))
        {
            sb.AppendLine(TypeInspection.IsCollectionElementTypeTrackable(p.Type)
                ? $"            base.{p.Name} = {srcVar}.{p.Name} is null ? null : {srcVar}.{p.Name}.AsTrackable(static x => x is null ? null : x.AsTrackable());"
                : $"            base.{p.Name} = {srcVar}.{p.Name} is null ? null : {srcVar}.{p.Name}.AsTrackable(static x => x);");
        }
        else if (TypeInspection.IsTrackableType(p.Type))
        {
            sb.AppendLine($"            base.{p.Name} = {srcVar}.{p.Name} is null ? null : {srcVar}.{p.Name}.AsTrackable();");
        }
        else
        {
            sb.AppendLine($"            base.{p.Name} = {srcVar}.{p.Name};");
        }
    }

    private static void EmitPropertyOverride(StringBuilder sb, IPropertySymbol p)
    {
        string typeName = p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        string name = p.Name;

        if (TypeInspection.IsCollectionType(p.Type))
        {
            sb.AppendLine($"        public override {typeName} {name}");
            sb.AppendLine("        {");
            sb.AppendLine($"            get => base.{name};");
            sb.AppendLine("            set");
            sb.AppendLine("            {");
            sb.AppendLine($"                if (!EqualityComparer<{typeName}>.Default.Equals(base.{name}, value))");
            sb.AppendLine("                {");
            sb.AppendLine($"                    {typeName} oldValue = base.{name};");
            sb.AppendLine("                    if (oldValue is ITrackable ot)");
            sb.AppendLine("                    {");
            sb.AppendLine($"                        DetachChild(\"{name}\", ot);");
            sb.AppendLine("                    }");
            sb.AppendLine(TypeInspection.IsCollectionElementTypeTrackable(p.Type)
                        ? $"                    {typeName} newValue = value is null ? null : value.AsTrackable(static x => x is null ? null : x.AsTrackable());"
                        : $"                    {typeName} newValue = value is null ? null : value.AsTrackable(static x => x);");
            sb.AppendLine($"                    base.{name} = newValue;");
            sb.AppendLine("                    if (newValue is ITrackable nt)");
            sb.AppendLine("                    {");
            sb.AppendLine($"                        AttachChild(\"{name}\", nt);");
            sb.AppendLine("                    }");
            sb.AppendLine($"                    OnChange(ChangeEventArgs.PropertySet(nameof({name}), oldValue, value));");
            sb.AppendLine("                }");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine();
        }
        else if (TypeInspection.IsTrackableType(p.Type))
        {
            sb.AppendLine($"        public override {typeName} {name}");
            sb.AppendLine("        {");
            sb.AppendLine($"            get => base.{name};");
            sb.AppendLine("            set");
            sb.AppendLine("            {");
            sb.AppendLine($"                if (!EqualityComparer<{typeName}>.Default.Equals(base.{name}, value))");
            sb.AppendLine("                {");
            sb.AppendLine($"                    {typeName} oldValue = base.{name};");
            sb.AppendLine("                    if (oldValue is ITrackable ot)");
            sb.AppendLine("                    {");
            sb.AppendLine($"                        DetachChild(\"{name}\", ot);");
            sb.AppendLine("                    }");
            sb.AppendLine($"                    {typeName} newValue = value is null ? null : value.AsTrackable();");
            sb.AppendLine($"                    base.{name} = newValue;");
            sb.AppendLine("                    if (newValue is ITrackable nt)");
            sb.AppendLine("                    {");
            sb.AppendLine($"                        AttachChild(\"{name}\", nt);");
            sb.AppendLine("                    }");
            sb.AppendLine($"                    OnChange(ChangeEventArgs.PropertySet(nameof({name}), oldValue, value));");
            sb.AppendLine("                }");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine();
        }
        else
        {
            sb.AppendLine($"        public override {typeName} {name}");
            sb.AppendLine("        {");
            sb.AppendLine($"            get => base.{name};");
            sb.AppendLine("            set");
            sb.AppendLine("            {");
            sb.AppendLine($"               if (!EqualityComparer<{typeName}>.Default.Equals(base.{name}, value))");
            sb.AppendLine("                {");
            sb.AppendLine($"                    {typeName} oldValue = base.{name};");
            sb.AppendLine($"                    base.{name} = value;");
            sb.AppendLine($"                    OnChange(ChangeEventArgs.PropertySet(nameof({name}), oldValue, value));");
            sb.AppendLine("                }");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine();
        }
    }
}