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

                // A public getter is required: the generated builder seeds every field from
                // ValidInstance() by reading the property, so write-only properties are unusable.
                // This getter check also subsumes a property-level accessibility check, because a
                // non-public property can never expose a public getter.
                if (member.GetMethod is null || member.GetMethod.DeclaredAccessibility != Accessibility.Public)
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

    /// <summary>
    /// True when the type is a collection whose element type is known, so it can be stored as an
    /// array internally and materialized back via <see cref="GetCollectionMaterialization"/>.
    /// Non-generic collections (e.g. <c>System.Collections.IEnumerable</c>) return false and are
    /// treated as scalars.
    /// </summary>
    public static bool IsMaterializableCollection(ITypeSymbol type)
        => IsCollectionType(type) && GetCollectionElementType(type) != null;

    /// <summary>
    /// Builds the expression that converts the internally-stored array field back into the
    /// property's declared collection type. Handles arrays, generic collection interfaces,
    /// concrete collection types with an IEnumerable constructor, and immutable collections.
    /// Only call when <see cref="IsCollectionType"/> is true and <see cref="GetCollectionElementType"/> is non-null.
    /// </summary>
    public static string GetCollectionMaterialization(ITypeSymbol collectionType, string fieldExpression)
    {
        // The field is already an array of the element type.
        if (collectionType is IArrayTypeSymbol)
            return fieldExpression;

        var namedType = (INamedTypeSymbol)collectionType;
        // Stryker disable once String : ContainingNamespace is never null for types we process
        var ns = namedType.ContainingNamespace?.ToDisplayString() ?? "";

        if (ns == "System.Collections.Immutable")
        {
            // Concrete immutable types (ImmutableArray<T>, ImmutableList<T>, ...) expose a static
            // CreateRange factory of the same name; immutable interfaces (IImmutableList<T>, ...)
            // map to the factory named without the leading 'I'.
            var factory = namedType.TypeKind == TypeKind.Interface
                ? namedType.Name.Substring(1)
                : namedType.Name;
            return $"System.Collections.Immutable.{factory}.CreateRange({fieldExpression})";
        }

        // Interfaces cannot be instantiated; List<T> satisfies every generic collection interface.
        if (namedType.TypeKind == TypeKind.Interface)
        {
            var elementType = GetCollectionElementType(namedType);
            var elementTypeName = elementType!.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            return $"new System.Collections.Generic.List<{elementTypeName}>({fieldExpression})";
        }

        var concreteTypeName = namedType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        return $"new {concreteTypeName}({fieldExpression})";
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
