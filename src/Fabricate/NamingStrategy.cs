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
        // Stryker disable once String : ContainingNamespace is never null for types we process
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

    /// <summary>
    /// Maps a symbol's accessibility to the C# keyword(s) used when re-declaring the
    /// <c>ValidInstance</c> partial method, so the generated declaration matches the
    /// user's implementing declaration exactly (partial method signatures must agree).
    /// </summary>
    public static string AccessibilityKeyword(Accessibility accessibility)
    {
        switch (accessibility)
        {
            case Accessibility.Public:
                return "public";
            case Accessibility.Internal:
                return "internal";
            case Accessibility.Protected:
                return "protected";
            case Accessibility.ProtectedOrInternal:
                return "protected internal";
            case Accessibility.ProtectedAndInternal:
                return "private protected";
            default:
                return "private";
        }
    }
}
