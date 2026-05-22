using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Fabricate;

/// <summary>
/// Analyzes a target type to extract property information and select the best constructor.
/// </summary>
public static class TypeAnalyzer
{
    public static ImmutableArray<PropertyInfo> GetAllProperties(INamedTypeSymbol typeSymbol)
    {
        var properties = new List<PropertyInfo>();
        var currentType = typeSymbol;

        while (currentType != null)
        {
            foreach (var member in currentType.GetMembers().OfType<IPropertySymbol>())
            {
                if (member.IsStatic || member.IsIndexer)
                    continue;

                if (member.DeclaredAccessibility != Accessibility.Public)
                    continue;

                // Include properties that are settable (set or init) or are constructor parameters
                var isSettable = member.SetMethod != null &&
                                 member.SetMethod.DeclaredAccessibility == Accessibility.Public;

                properties.Add(new PropertyInfo(
                    member.Name,
                    member.Type,
                    isSettable,
                    member.NullableAnnotation == NullableAnnotation.Annotated || 
                    (member.Type.IsValueType && member.Type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)));
            }

            currentType = currentType.BaseType;
        }

        // DistinctBy not available in netstandard2.0, use GroupBy
        // Stryker disable once Linq : GroupBy groups are never empty, First() is safe
        return properties
            .GroupBy(p => p.Name)
            .Select(g => g.First())
            .ToImmutableArray();
    }

    public static ConstructorInfo? SelectConstructor(INamedTypeSymbol typeSymbol, ImmutableArray<PropertyInfo> properties)
    {
        var constructors = typeSymbol.Constructors
            .Where(c => !c.IsStatic && c.DeclaredAccessibility == Accessibility.Public)
            .OrderByDescending(c => c.Parameters.Length)
            .ToList();

        if (constructors.Count == 0)
            return null;

        // Try to find the longest constructor where all parameters match properties
        foreach (var ctor in constructors)
        {
            var matchedParams = new List<ConstructorParameterInfo>();
            var allMatched = true;

            foreach (var param in ctor.Parameters)
            {
                var matchingProperty = properties.FirstOrDefault(p =>
                    string.Equals(p.Name, param.Name, System.StringComparison.OrdinalIgnoreCase) &&
                    SymbolEqualityComparer.Default.Equals(p.Type, param.Type));

                if (matchingProperty == null)
                {
                    allMatched = false;
                    break;
                }

                matchedParams.Add(new ConstructorParameterInfo(param.Name, param.Type, matchingProperty.Name));
            }

            if (allMatched)
            {
                return new ConstructorInfo(matchedParams.ToImmutableArray());
            }
        }

        return null;
    }

    public static bool IsCollectionType(ITypeSymbol type)
    {
        if (type is IArrayTypeSymbol)
            return true;

        if (type is not INamedTypeSymbol namedType)
            return false;

        var fullName = namedType.OriginalDefinition.ToDisplayString();
        return fullName.StartsWith("System.Collections.Generic.") ||
               fullName.StartsWith("System.Collections.Immutable.") ||
               fullName == "System.Collections.IEnumerable" ||
               fullName == "System.Collections.IList" ||
               fullName == "System.Collections.ICollection";
    }

    public static ITypeSymbol? GetCollectionElementType(ITypeSymbol type)
    {
        if (type is IArrayTypeSymbol arrayType)
            return arrayType.ElementType;

        if (type is INamedTypeSymbol namedType && namedType.TypeArguments.Length == 1)
            return namedType.TypeArguments[0];

        return null;
    }
}

public sealed class PropertyInfo
{
    public string Name { get; }
    public ITypeSymbol Type { get; }
    public bool IsSettable { get; }
    public bool IsNullable { get; }

    public PropertyInfo(string name, ITypeSymbol type, bool isSettable, bool isNullable)
    {
        Name = name;
        Type = type;
        IsSettable = isSettable;
        IsNullable = isNullable;
    }
}

public sealed class ConstructorInfo
{
    public ImmutableArray<ConstructorParameterInfo> Parameters { get; }

    public ConstructorInfo(ImmutableArray<ConstructorParameterInfo> parameters)
    {
        Parameters = parameters;
    }
}

public sealed class ConstructorParameterInfo
{
    public string ParameterName { get; }
    public ITypeSymbol Type { get; }
    public string PropertyName { get; }

    public ConstructorParameterInfo(string parameterName, ITypeSymbol type, string propertyName)
    {
        ParameterName = parameterName;
        Type = type;
        PropertyName = propertyName;
    }
}
