using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;

namespace Fabricate;

/// <summary>
/// Emits With/Without methods for each property.
/// </summary>
public static class WithMethodEmitter
{
    public static string Emit(
        string builderClassName,
        ImmutableArray<PropertyInfo> properties)
    {
        var sb = new StringBuilder();
        var typeOccurrences = CountTypeOccurrences(properties);

        foreach (var property in properties)
        {
            EmitWithMethod(sb, builderClassName, property, typeOccurrences);

            if (property.IsNullable)
            {
                EmitWithoutMethod(sb, builderClassName, property);
            }
        }

        return sb.ToString();
    }

    private static Dictionary<string, int> CountTypeOccurrences(ImmutableArray<PropertyInfo> properties)
    {
        var counts = new Dictionary<string, int>();
        foreach (var prop in properties)
        {
            var typeKey = GetTypeKey(prop.Type);
            counts.TryGetValue(typeKey, out var current);
            counts[typeKey] = current + 1;
        }
        return counts;
    }

    private static string GetTypeKey(ITypeSymbol type)
    {
        if (TypeAnalyzer.IsCollectionType(type))
        {
            var elementType = TypeAnalyzer.GetCollectionElementType(type);
            if (elementType != null)
                return elementType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        }

        // Unwrap nullable
        if (type is INamedTypeSymbol nullable &&
            nullable.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T &&
            nullable.TypeArguments.Length == 1)
        {
            return nullable.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        }

        return type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
    }

    private static void EmitWithMethod(
        StringBuilder sb,
        string builderClassName,
        PropertyInfo property,
        Dictionary<string, int> typeOccurrences)
    {
        var fieldName = NamingStrategy.GetFieldName(property.Name);
        var typeKey = GetTypeKey(property.Type);
        var isTypeUnique = typeOccurrences[typeKey] == 1;
        var methodName = NamingStrategy.GetWithMethodName(property, isTypeUnique);

        if (TypeAnalyzer.IsCollectionType(property.Type))
        {
            var elementType = TypeAnalyzer.GetCollectionElementType(property.Type);
            if (elementType != null)
            {
                var elementTypeName = elementType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                var paramName = NamingStrategy.GetFieldName(property.Name).TrimStart('_');
                sb.AppendLine($"    public {builderClassName} {methodName}(params {elementTypeName}[] {paramName})");
                sb.AppendLine("    {");
                sb.AppendLine($"        {fieldName} = {paramName};");
                sb.AppendLine("        return this;");
                sb.AppendLine("    }");
                sb.AppendLine();
                return;
            }
        }

        var typeName = property.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        if (property.IsNullable && !property.Type.IsValueType)
        {
            typeName += "?";
        }
        var parameterName = NamingStrategy.GetFieldName(property.Name).TrimStart('_');

        sb.AppendLine($"    public {builderClassName} {methodName}({typeName} {parameterName})");
        sb.AppendLine("    {");
        sb.AppendLine($"        {fieldName} = {parameterName};");
        sb.AppendLine("        return this;");
        sb.AppendLine("    }");
        sb.AppendLine();
    }

    private static void EmitWithoutMethod(
        StringBuilder sb,
        string builderClassName,
        PropertyInfo property)
    {
        var fieldName = NamingStrategy.GetFieldName(property.Name);
        var methodName = NamingStrategy.GetWithoutMethodName(property);

        sb.AppendLine($"    public {builderClassName} {methodName}()");
        sb.AppendLine("    {");
        sb.AppendLine($"        {fieldName} = null;");
        sb.AppendLine("        return this;");
        sb.AppendLine("    }");
        sb.AppendLine();
    }
}
