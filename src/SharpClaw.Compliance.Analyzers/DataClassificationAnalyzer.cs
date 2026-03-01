using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace SharpClaw.Compliance.Analyzers;

/// <summary>
/// Validates proper usage of [DataClassification] attributes.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DataClassificationAnalyzer : DiagnosticAnalyzer
{
    public const string MissingClassificationDiagnosticId = "SC002";
    public const string InvalidLevelDiagnosticId = "SC003";
    public const string MissingPiiFlagDiagnosticId = "SC004";

    private static readonly LocalizableString MissingTitle = "Missing data classification";
    private static readonly LocalizableString MissingMessageFormat = "Class '{0}' contains properties that may have PII but no classification is specified";
    private static readonly LocalizableString MissingDescription = "Classes with PII properties should have a class-level data classification attribute.";

    private static readonly LocalizableString InvalidTitle = "Invalid classification level";
    private static readonly LocalizableString InvalidMessageFormat = "Property '{0}' has {1} classification but contains obvious PII";
    private static readonly LocalizableString InvalidDescription = "PII properties should not have Public or Internal classification.";

    private static readonly LocalizableString MissingPiiTitle = "Missing PII indicator";
    private static readonly LocalizableString MissingPiiMessageFormat = "Property '{0}' appears to contain PII but is not marked with ContainsPii flag";
    private static readonly LocalizableString MissingPiiDescription = "Properties with PII should explicitly indicate ContainsPii=true.";

    private const string Category = "Compliance";

    private static readonly DiagnosticDescriptor MissingRule = new(
        MissingClassificationDiagnosticId,
        MissingTitle,
        MissingMessageFormat,
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: MissingDescription);

    private static readonly DiagnosticDescriptor InvalidRule = new(
        InvalidLevelDiagnosticId,
        InvalidTitle,
        InvalidMessageFormat,
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: InvalidDescription);

    private static readonly DiagnosticDescriptor MissingPiiRule = new(
        MissingPiiFlagDiagnosticId,
        MissingPiiTitle,
        MissingPiiMessageFormat,
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: MissingPiiDescription);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(MissingRule, InvalidRule, MissingPiiRule);

    private static readonly HashSet<string> PiiPropertyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "email", "emailaddress", "phone", "phonenumber", "ssn",
        "socialsecurity", "name", "firstname", "lastname", "fullname",
        "address", "dob", "birthdate", "credit", "creditcard"
    };

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeClassDeclaration, SyntaxKind.ClassDeclaration);
        context.RegisterSyntaxNodeAction(AnalyzeRecordDeclaration, SyntaxKind.RecordDeclaration);
    }

    private static void AnalyzeClassDeclaration(SyntaxNodeAnalysisContext context)
    {
        var classDeclaration = (ClassDeclarationSyntax)context.Node;
        AnalyzeTypeDeclaration(context, classDeclaration.Identifier, classDeclaration.AttributeLists, classDeclaration.Members);
    }

    private static void AnalyzeRecordDeclaration(SyntaxNodeAnalysisContext context)
    {
        var recordDeclaration = (RecordDeclarationSyntax)context.Node;
        AnalyzeTypeDeclaration(context, recordDeclaration.Identifier, recordDeclaration.AttributeLists, recordDeclaration.Members);
    }

    private static void AnalyzeTypeDeclaration(
        SyntaxNodeAnalysisContext context,
        SyntaxToken identifier,
        SyntaxList<AttributeListSyntax> classAttributes,
        SyntaxList<MemberDeclarationSyntax> members)
    {
        var hasClassLevelClassification = HasClassificationAttribute(classAttributes);

        var piiProperties = new List<PropertyDeclarationSyntax>();

        foreach (var member in members)
        {
            if (member is not PropertyDeclarationSyntax property)
            {
                continue;
            }

            var propertyName = property.Identifier.ValueText;

            if (!IsLikelyPiiProperty(propertyName))
            {
                continue;
            }

            piiProperties.Add(property);

            var propertyAttributes = property.AttributeLists;
            var hasPropertyClassification = HasClassificationAttribute(propertyAttributes);

            // Check if property with PII has Public or Internal classification
            var classificationLevel = GetClassificationLevel(propertyAttributes);
            if (classificationLevel is "Public" or "Internal")
            {
                var diagnostic = Diagnostic.Create(
                    InvalidRule,
                    property.Identifier.GetLocation(),
                    propertyName,
                    classificationLevel);
                context.ReportDiagnostic(diagnostic);
            }

            // Check if PII property is missing ContainsPii flag
            if (!hasPropertyClassification && !hasClassLevelClassification)
            {
                var diagnostic = Diagnostic.Create(
                    MissingPiiRule,
                    property.Identifier.GetLocation(),
                    propertyName);
                context.ReportDiagnostic(diagnostic);
            }
        }

        // Report missing class-level classification if there are PII properties
        if (piiProperties.Count > 0 && !hasClassLevelClassification)
        {
            var diagnostic = Diagnostic.Create(
                MissingRule,
                identifier.GetLocation(),
                identifier.ValueText);
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static bool IsLikelyPiiProperty(string name)
    {
        var cleanName = name.Replace("_", "").ToLowerInvariant();
        return PiiPropertyNames.Any(pii => cleanName.Contains(pii));
    }

    private static bool HasClassificationAttribute(SyntaxList<AttributeListSyntax> attributeLists)
    {
        foreach (var attrList in attributeLists)
        {
            foreach (var attr in attrList.Attributes)
            {
                var name = attr.Name.ToString();
                if (name.Contains("DataClassification") ||
                    name.Contains("ClassifiedAs"))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static string? GetClassificationLevel(SyntaxList<AttributeListSyntax> attributeLists)
    {
        foreach (var attrList in attributeLists)
        {
            foreach (var attr in attrList.Attributes)
            {
                var name = attr.Name.ToString();
                if (!name.Contains("DataClassification"))
                {
                    continue;
                }

                // Check attribute arguments for classification level
                if (attr.ArgumentList == null)
                {
                    continue;
                }

                foreach (var arg in attr.ArgumentList.Arguments)
                {
                    var argText = arg.ToString();
                    if (argText.Contains("Public"))
                    {
                        return "Public";
                    }

                    if (argText.Contains("Internal"))
                    {
                        return "Internal";
                    }

                    if (argText.Contains("Confidential"))
                    {
                        return "Confidential";
                    }

                    if (argText.Contains("Restricted"))
                    {
                        return "Restricted";
                    }
                }
            }
        }

        return null;
    }
}
