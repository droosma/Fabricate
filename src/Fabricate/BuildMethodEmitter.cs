using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;

namespace Fabricate;

/// <summary>
/// Emits the Build() method using the selected constructor and object initializer.
/// </summary>
public static class BuildMethodEmitter
{
    public static string Emit(
        INamedTypeSymbol targetType,
        ConstructorInfo constructorInfo,
        ImmutableArray<PropertyInfo> properties)
    {
        var sb = new StringBuilder();
        var targetTypeName = targetType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var constructorParams = constructorInfo.Parameters;

        // Properties that go in the object initializer (settable and not in constructor)
        var initializerProperties = properties
            .Where(p => p.IsSettable && !constructorParams.Any(cp =>
                string.Equals(cp.PropertyName, p.Name, System.StringComparison.Ordinal)))
            .ToList();

        sb.Append($"    public {targetTypeName} Build()");

        if (constructorParams.Length == 0 && initializerProperties.Count == 0)
        {
            sb.AppendLine($" => new {targetTypeName}();");
            return sb.ToString();
        }

        sb.AppendLine();
        sb.Append($"        => new {targetTypeName}(");
        sb.Append(string.Join(", ", constructorParams.Select(p => BuildConstructorArgument(p, properties))));
        sb.Append(")");

        if (initializerProperties.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("        {");
            foreach (var prop in initializerProperties)
            {
                var fieldName = NamingStrategy.GetFieldName(prop.Name);
                if (TypeAnalyzer.IsMaterializableCollection(prop.Type))
                {
                    var materialization = TypeAnalyzer.GetCollectionMaterialization(prop.Type, fieldName);
                    var value = prop.IsNullable
                        ? $"{fieldName} == null ? null : {materialization}"
                        : materialization;
                    sb.AppendLine($"            {prop.Name} = {value},");
                }
                else
                {
                    sb.AppendLine($"            {prop.Name} = {fieldName},");
                }
            }
            sb.Append("        }");
        }

        sb.AppendLine(";");
        return sb.ToString();
    }

    private static string BuildConstructorArgument(
        ConstructorParameterInfo parameter,
        ImmutableArray<PropertyInfo> properties)
    {
        var fieldName = NamingStrategy.GetFieldName(parameter.PropertyName);

        if (TypeAnalyzer.IsMaterializableCollection(parameter.Type))
        {
            var materialization = TypeAnalyzer.GetCollectionMaterialization(parameter.Type, fieldName);
            var isNullable = properties.Any(p =>
                string.Equals(p.Name, parameter.PropertyName, System.StringComparison.Ordinal) && p.IsNullable);
            return isNullable ? $"{fieldName} == null ? null : {materialization}" : materialization;
        }

        return fieldName;
    }

    public static string EmitImplicitOperator(
        string builderClassName,
        INamedTypeSymbol targetType)
    {
        var targetTypeName = targetType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var sb = new StringBuilder();
        sb.AppendLine($"    public static implicit operator {targetTypeName}({builderClassName} builder)");
        sb.AppendLine("        => builder.Build();");
        return sb.ToString();
    }
}
