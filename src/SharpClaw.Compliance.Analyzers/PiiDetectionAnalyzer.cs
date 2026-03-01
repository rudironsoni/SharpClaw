using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace SharpClaw.Compliance.Analyzers;

/// <summary>
/// Detects potential PII (Personally Identifiable Information) in code.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PiiDetectionAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "SC001";

    private static readonly LocalizableString Title = "Potential PII detected";
    private static readonly LocalizableString MessageFormat = "Property '{0}' may contain PII. Consider adding [DataClassification] attribute";
    private static readonly LocalizableString Description = "Properties with names suggesting PII should be marked with appropriate data classification attributes.";

    private const string Category = "Compliance";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        Title,
        MessageFormat,
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: Description);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    // Keywords that suggest PII content
    private static readonly HashSet<string> PiiKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "email", "emailaddress", "mail",
        "phone", "phonenumber", "mobile", "cell",
        "ssn", "socialsecurity", "socialsecuritynumber",
        "name", "firstname", "lastname", "fullname", "givenname", "surname",
        "address", "street", "city", "zip", "zipcode", "postalcode",
        "dob", "birthdate", "dateofbirth", "birthday",
        "passport", "driverlicense", "license",
        "credit", "creditcard", "cardnumber", "cc",
        "bank", "account", "accountnumber", "iban",
        "ip", "ipaddress", "mac", "macaddress",
        "latitude", "longitude", "geolocation", "location",
        "health", "medical", "diagnosis", "condition",
        "biometric", "fingerprint", "facial",
        "password", "secret", "apikey", "token", "privatekey"
    };

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzePropertyDeclaration, SyntaxKind.PropertyDeclaration);
        context.RegisterSyntaxNodeAction(AnalyzeFieldDeclaration, SyntaxKind.FieldDeclaration);
    }

    private static void AnalyzePropertyDeclaration(SyntaxNodeAnalysisContext context)
    {
        var property = (PropertyDeclarationSyntax)context.Node;
        var propertyName = property.Identifier.ValueText;

        if (!IsPiiProperty(propertyName))
        {
            return;
        }

        // Check if it already has DataClassification attribute
        if (HasDataClassificationAttribute(property.AttributeLists))
        {
            return;
        }

        var diagnostic = Diagnostic.Create(Rule, property.Identifier.GetLocation(), propertyName);
        context.ReportDiagnostic(diagnostic);
    }

    private static void AnalyzeFieldDeclaration(SyntaxNodeAnalysisContext context)
    {
        var field = (FieldDeclarationSyntax)context.Node;

        foreach (var variable in field.Declaration.Variables)
        {
            var fieldName = variable.Identifier.ValueText;

            if (!IsPiiProperty(fieldName))
            {
                continue;
            }

            if (HasDataClassificationAttribute(field.AttributeLists))
            {
                continue;
            }

            var diagnostic = Diagnostic.Create(Rule, variable.Identifier.GetLocation(), fieldName);
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static bool IsPiiProperty(string name)
    {
        // Remove common prefixes/suffixes
        var cleanName = name
            .Replace("_", "")
            .Replace("m_", "")
            .Replace("s_", "");

        return PiiKeywords.Any(keyword =>
            cleanName.Equals(keyword, StringComparison.OrdinalIgnoreCase) ||
            cleanName.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasDataClassificationAttribute(SyntaxList<AttributeListSyntax> attributeLists)
    {
        foreach (var attrList in attributeLists)
        {
            foreach (var attr in attrList.Attributes)
            {
                var name = attr.Name.ToString();
                if (name.Contains("DataClassification") ||
                    name.Contains("Pii") ||
                    name.Contains("Sensitive"))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
