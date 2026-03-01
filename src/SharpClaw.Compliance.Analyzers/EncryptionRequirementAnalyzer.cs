using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace SharpClaw.Compliance.Analyzers;

/// <summary>
/// Validates encryption requirements for sensitive data.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class EncryptionRequirementAnalyzer : DiagnosticAnalyzer
{
    public const string MissingEncryptionDiagnosticId = "SC005";
    public const string WeakEncryptionDiagnosticId = "SC006";
    public const string PlainTextStorageDiagnosticId = "SC007";

    private static readonly LocalizableString MissingTitle = "Missing encryption";
    private static readonly LocalizableString MissingMessageFormat = "Property '{0}' contains sensitive data but encryption is not configured";
    private static readonly LocalizableString MissingDescription = "Sensitive data properties should use encryption.";

    private static readonly LocalizableString WeakTitle = "Weak encryption algorithm";
    private static readonly LocalizableString WeakMessageFormat = "Property '{0}' uses weak encryption algorithm '{1}'";
    private static readonly LocalizableString WeakDescription = "Use strong encryption algorithms (AES-256-GCM, AES-256-CBC).";

    private static readonly LocalizableString PlainTextTitle = "Plain text storage of sensitive data";
    private static readonly LocalizableString PlainTextMessageFormat = "Property '{0}' stores sensitive data in plain text";
    private static readonly LocalizableString PlainTextDescription = "Never store sensitive data (passwords, keys, tokens) in plain text.";

    private const string Category = "Compliance";

    private static readonly DiagnosticDescriptor MissingRule = new(
        MissingEncryptionDiagnosticId,
        MissingTitle,
        MissingMessageFormat,
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: MissingDescription);

    private static readonly DiagnosticDescriptor WeakRule = new(
        WeakEncryptionDiagnosticId,
        WeakTitle,
        WeakMessageFormat,
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: WeakDescription);

    private static readonly DiagnosticDescriptor PlainTextRule = new(
        PlainTextStorageDiagnosticId,
        PlainTextTitle,
        PlainTextMessageFormat,
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: PlainTextDescription);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(MissingRule, WeakRule, PlainTextRule);

    // Properties that MUST be encrypted
    private static readonly HashSet<string> MustEncryptNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "password", "secret", "apikey", "api_key", "token", "accesstoken", "refreshtoken",
        "privatekey", "private_key", "secretkey", "secret_key", "connectionstring",
        "credential", "credentials"
    };

    // Weak encryption algorithms
    private static readonly HashSet<string> WeakAlgorithms = new(StringComparer.OrdinalIgnoreCase)
    {
        "DES", "3DES", "TripleDES", "RC2", "RC4", "MD5", "SHA1"
    };

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzePropertyDeclaration, SyntaxKind.PropertyDeclaration);
        context.RegisterSyntaxNodeAction(AnalyzeVariableDeclaration, SyntaxKind.VariableDeclaration);
        context.RegisterSyntaxNodeAction(AnalyzeAssignment, SyntaxKind.SimpleAssignmentExpression);
    }

    private static void AnalyzePropertyDeclaration(SyntaxNodeAnalysisContext context)
    {
        var property = (PropertyDeclarationSyntax)context.Node;
        var propertyName = property.Identifier.ValueText;

        // Check for properties that MUST be encrypted
        if (!MustEncryptNames.Any(name => propertyName.Contains(name, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        // Check if property type suggests encryption is being used
        var typeName = property.Type.ToString();

        // Plain string type for sensitive data is an error
        if (typeName is "string" or "String")
        {
            // Check if there's encryption-related code in getter/setter
            if (property.ExpressionBody == null &&
                (property.AccessorList == null || !HasEncryptionInAccessors(property.AccessorList)))
            {
                var diagnostic = Diagnostic.Create(
                    PlainTextRule,
                    property.Identifier.GetLocation(),
                    propertyName);
                context.ReportDiagnostic(diagnostic);
            }
        }

        // Check for weak encryption usage
        CheckForWeakEncryption(context, property, propertyName);
    }

    private static void AnalyzeVariableDeclaration(SyntaxNodeAnalysisContext context)
    {
        var declaration = (VariableDeclarationSyntax)context.Node;
        var typeName = declaration.Type.ToString();

        foreach (var variable in declaration.Variables)
        {
            var variableName = variable.Identifier.ValueText;

            if (!MustEncryptNames.Any(name => variableName.Contains(name, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            // Check for plain string assignment
            if (typeName is "string" or "String" && variable.Initializer?.Value != null)
            {
                var value = variable.Initializer.Value.ToString();

                // If it's a literal string value, that's definitely wrong
                if (variable.Initializer.Value is LiteralExpressionSyntax literal &&
                    literal.IsKind(SyntaxKind.StringLiteralExpression))
                {
                    var diagnostic = Diagnostic.Create(
                        PlainTextRule,
                        variable.Identifier.GetLocation(),
                        variableName);
                    context.ReportDiagnostic(diagnostic);
                }
            }

            // Check for weak encryption in initializer
            CheckExpressionForWeakEncryption(context, variable.Initializer?.Value, variableName);
        }
    }

    private static void AnalyzeAssignment(SyntaxNodeAnalysisContext context)
    {
        var assignment = (AssignmentExpressionSyntax)context.Node;

        if (assignment.Left is not IdentifierNameSyntax identifier)
        {
            return;
        }

        var name = identifier.Identifier.ValueText;

        if (!MustEncryptNames.Any(must => name.Contains(must, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        // Check for literal string assignment
        if (assignment.Right is LiteralExpressionSyntax literal &&
            literal.IsKind(SyntaxKind.StringLiteralExpression))
        {
            var diagnostic = Diagnostic.Create(
                PlainTextRule,
                assignment.GetLocation(),
                name);
            context.ReportDiagnostic(diagnostic);
        }

        CheckExpressionForWeakEncryption(context, assignment.Right, name);
    }

    private static bool HasEncryptionInAccessors(AccessorListSyntax accessorList)
    {
        var accessorsText = accessorList.ToString().ToUpperInvariant();

        // Check for encryption-related keywords
        return accessorsText.Contains("ENCRYPT") ||
               accessorsText.Contains("DECRYPT") ||
               accessorsText.Contains("PROTECT") ||
               accessorsText.Contains("UNPROTECT") ||
               accessorsText.Contains("HASH");
    }

    private static void CheckForWeakEncryption(SyntaxNodeAnalysisContext context, PropertyDeclarationSyntax property, string propertyName)
    {
        // Check property type for weak algorithm names
        var typeName = property.Type.ToString();

        foreach (var weakAlgo in WeakAlgorithms)
        {
            if (typeName.Contains(weakAlgo, StringComparison.OrdinalIgnoreCase))
            {
                var diagnostic = Diagnostic.Create(
                    WeakRule,
                    property.Type.GetLocation(),
                    propertyName,
                    weakAlgo);
                context.ReportDiagnostic(diagnostic);
                return;
            }
        }
    }

    private static void CheckExpressionForWeakEncryption(SyntaxNodeAnalysisContext context, ExpressionSyntax? expression, string name)
    {
        if (expression == null)
        {
            return;
        }

        var expressionText = expression.ToString();

        foreach (var weakAlgo in WeakAlgorithms)
        {
            if (expressionText.Contains(weakAlgo, StringComparison.OrdinalIgnoreCase))
            {
                var diagnostic = Diagnostic.Create(
                    WeakRule,
                    expression.GetLocation(),
                    name,
                    weakAlgo);
                context.ReportDiagnostic(diagnostic);
                return;
            }
        }
    }
}
