using Microsoft.CodeAnalysis;

namespace Fabricate;

/// <summary>
/// Determines method naming based on whether a type is BCL or custom,
/// and whether it's unique among all properties.
/// </summary>
public static class NamingStrategy
{
    public static bool IsBclType(ITypeSymbol type)
    {
        var ns = type.ContainingNamespace?.ToDisplayString() ?? "";
        
        // Unwrap nullable
        if (type is INamedTypeSymbol namedType && 
            namedType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T &&
            namedType.TypeArguments.Length == 1)
        {
            return IsBclType(namedType.TypeArguments[0]);
        }

        return type.SpecialType != SpecialType.None ||
               ns.StartsWith("System") ||
               ns.StartsWith("Microsoft");
    }

    public static string GetWithMethodName(PropertyInfo property, bool isTypeUnique)
    {
        var effectiveType = property.Type;

        // For collections, consider the element type
        if (TypeAnalyzer.IsCollectionType(property.Type))
        {
            var elementType = TypeAnalyzer.GetCollectionElementType(property.Type);
            if (elementType != null)
            {
                effectiveType = elementType;
            }
        }

        // Unwrap nullable for type checking
        if (effectiveType is INamedTypeSymbol nullable &&
            nullable.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T &&
            nullable.TypeArguments.Length == 1)
        {
            effectiveType = nullable.TypeArguments[0];
        }

        if (IsBclType(effectiveType))
        {
            return $"With{property.Name}";
        }

        // Custom type: use just "With" if unique, otherwise "With{PropertyName}"
        return isTypeUnique ? "With" : $"With{property.Name}";
    }

    public static string GetWithoutMethodName(PropertyInfo property)
    {
        return $"Without{property.Name}";
    }

    public static string GetFieldName(string propertyName)
    {
        return $"_{char.ToLowerInvariant(propertyName[0])}{propertyName.Substring(1)}";
    }
}
