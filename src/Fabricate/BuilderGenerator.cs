using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Fabricate;

[Generator]
public sealed class BuilderGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var builderDeclarations = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                "Fabricate.FabricateAttribute`1",
                predicate: static (node, _) => node is ClassDeclarationSyntax,
                transform: static (ctx, _) => GetBuilderInfo(ctx))
            .Where(static info => info is not null);

        context.RegisterSourceOutput(builderDeclarations, static (spc, info) =>
        {
            // Stryker disable once Statement : defensive guard; nulls are filtered by Where clause above
            if (info is null) return;
            GenerateBuilder(spc, info);
        });
    }

    private static BuilderInfo? GetBuilderInfo(GeneratorAttributeSyntaxContext context)
    {
        if (context.TargetSymbol is not INamedTypeSymbol builderSymbol)
            return null;

        var attributeData = context.Attributes.FirstOrDefault();
        if (attributeData is null)
            return null;

        var attributeClass = attributeData.AttributeClass;
        // Stryker disable once Logical : FabricateAttribute<T> always has exactly 1 type argument
        if (attributeClass is null || !attributeClass.IsGenericType || attributeClass.TypeArguments.Length != 1)
            return null;

        var targetType = attributeClass.TypeArguments[0] as INamedTypeSymbol;
        if (targetType is null)
            return null;

        // Check if user has implemented ValidInstance
        var validInstanceMethod = builderSymbol.GetMembers("ValidInstance")
            .OfType<IMethodSymbol>()
            .FirstOrDefault(m => m.IsStatic &&
                                 m.Parameters.Length == 0 &&
                                 SymbolEqualityComparer.Default.Equals(m.ReturnType, targetType));

        var hasValidInstance = validInstanceMethod is not null;
        var validInstanceAccessibility = validInstanceMethod?.DeclaredAccessibility ?? Accessibility.Private;

        var classDeclaration = context.TargetNode as ClassDeclarationSyntax;
        var location = classDeclaration?.GetLocation();

        return new BuilderInfo(
            builderSymbol,
            targetType,
            hasValidInstance,
            validInstanceAccessibility,
            location);
    }

    private static void GenerateBuilder(SourceProductionContext context, BuilderInfo info)
    {
        var builderName = info.BuilderSymbol.Name;
        var targetType = info.TargetType;
        var targetTypeName = targetType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var builderNamespace = info.BuilderSymbol.ContainingNamespace.IsGlobalNamespace
            ? null
            : info.BuilderSymbol.ContainingNamespace.ToDisplayString();

        // Diagnostic: FAB002 - naming suggestion
        if (!builderName.EndsWith("Builder") && info.Location != null)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.BuilderNamingSuggestion,
                info.Location,
                builderName,
                targetType.Name));
        }

        // Diagnostic: FAB001 - missing ValidInstance
        if (!info.HasValidInstance && info.Location != null)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.MissingValidInstance,
                info.Location,
                builderName,
                targetTypeName));
        }

        // Get all properties
        var properties = TypeAnalyzer.GetAllProperties(targetType);

        // Select constructor
        var constructorInfo = TypeAnalyzer.SelectConstructor(targetType, properties);
        if (constructorInfo is null)
        {
            if (info.Location != null)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.CannotResolveConstructor,
                    info.Location,
                    targetType.Name));
            }
            return;
        }

        // Only properties that can actually be populated are usable: either settable
        // (set/init) or supplied via the selected constructor. Read-only computed
        // properties would otherwise produce phantom With methods that silently do nothing.
        var constructorPropertyNames = new System.Collections.Generic.HashSet<string>(
            constructorInfo.Parameters.Select(p => p.PropertyName),
            System.StringComparer.Ordinal);
        var usableProperties = properties
            .Where(p => p.IsSettable || constructorPropertyNames.Contains(p.Name))
            .ToImmutableArray();

        // Generate source
        var source = GenerateSource(builderName, builderNamespace, targetType, targetTypeName, usableProperties, constructorInfo, info.ValidInstanceAccessibility);
        context.AddSource($"{builderName}.g.cs", SourceText.From(source, Encoding.UTF8));
    }

    private static string GenerateSource(
        string builderName,
        string? builderNamespace,
        INamedTypeSymbol targetType,
        string targetTypeName,
        ImmutableArray<PropertyInfo> properties,
        ConstructorInfo constructorInfo,
        Accessibility validInstanceAccessibility)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();

        if (builderNamespace != null)
        {
            sb.AppendLine($"namespace {builderNamespace};");
            sb.AppendLine();
        }

        sb.AppendLine($"partial class {builderName}");
        sb.AppendLine("{");

        // Emit partial method signature for ValidInstance
        sb.AppendLine($"    {NamingStrategy.AccessibilityKeyword(validInstanceAccessibility)} static partial {targetTypeName} ValidInstance();");
        sb.AppendLine();

        // Emit fields
        foreach (var property in properties)
        {
            var fieldName = NamingStrategy.GetFieldName(property.Name);
            var typeName = property.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

            if (property.IsNullable && !property.Type.IsValueType)
            {
                typeName += "?";
            }

            if (TypeAnalyzer.IsCollectionType(property.Type))
            {
                var elementType = TypeAnalyzer.GetCollectionElementType(property.Type);
                if (elementType != null)
                {
                    var elementTypeName = elementType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    var nullableSuffix = property.IsNullable ? "?" : "";
                    sb.AppendLine($"    private {elementTypeName}[]{nullableSuffix} {fieldName} = default!;");
                    continue;
                }
            }

            sb.AppendLine($"    private {typeName} {fieldName} = default!;");
        }
        sb.AppendLine();

        // Emit constructor
        sb.AppendLine($"    public {builderName}()");
        sb.AppendLine("    {");
        sb.AppendLine($"        var seed = ValidInstance();");
        foreach (var property in properties)
        {
            var fieldName = NamingStrategy.GetFieldName(property.Name);
            if (TypeAnalyzer.IsCollectionType(property.Type))
            {
                var elementType = TypeAnalyzer.GetCollectionElementType(property.Type);
                if (elementType != null)
                {
                    if (property.IsNullable)
                    {
                        sb.AppendLine($"        {fieldName} = seed.{property.Name} == null ? null : System.Linq.Enumerable.ToArray(seed.{property.Name});");
                    }
                    else
                    {
                        sb.AppendLine($"        {fieldName} = System.Linq.Enumerable.ToArray(seed.{property.Name});");
                    }
                    continue;
                }
            }
            sb.AppendLine($"        {fieldName} = seed.{property.Name};");
        }
        sb.AppendLine("    }");
        sb.AppendLine();

        // Emit Build method
        sb.Append(BuildMethodEmitter.Emit(targetType, constructorInfo, properties));
        sb.AppendLine();

        // Emit implicit operator
        sb.Append(BuildMethodEmitter.EmitImplicitOperator(builderName, targetType));
        sb.AppendLine();

        // Emit With/Without methods
        sb.Append(WithMethodEmitter.Emit(builderName, properties));

        sb.AppendLine("}");

        return sb.ToString();
    }
}

internal sealed class BuilderInfo
{
    public INamedTypeSymbol BuilderSymbol { get; }
    public INamedTypeSymbol TargetType { get; }
    public bool HasValidInstance { get; }
    public Accessibility ValidInstanceAccessibility { get; }
    public Location? Location { get; }

    public BuilderInfo(INamedTypeSymbol builderSymbol, INamedTypeSymbol targetType, bool hasValidInstance, Accessibility validInstanceAccessibility, Location? location)
    {
        BuilderSymbol = builderSymbol;
        TargetType = targetType;
        HasValidInstance = hasValidInstance;
        ValidInstanceAccessibility = validInstanceAccessibility;
        Location = location;
    }
}
