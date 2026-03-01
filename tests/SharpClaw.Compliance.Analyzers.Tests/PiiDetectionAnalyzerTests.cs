using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Testing.Verifiers;
using Xunit;

namespace SharpClaw.Compliance.Analyzers.Tests;

public sealed class PiiDetectionAnalyzerTests
{
    [Fact]
    public async Task Property_WithPiiName_NoAttribute_TriggersDiagnostic()
    {
        const string test = @"
public class User
{
    public string Email { get; set; }
}";

        var expected = CSharpCodeFixVerifier<PiiDetectionAnalyzer, EmptyCodeFixProvider, XUnitVerifier>
            .Diagnostic(PiiDetectionAnalyzer.DiagnosticId)
            .WithLocation(4, 19)
            .WithArguments("Email");

        await CSharpCodeFixVerifier<PiiDetectionAnalyzer, EmptyCodeFixProvider, XUnitVerifier>
            .VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task Property_WithPiiName_WithAttribute_NoDiagnostic()
    {
        const string test = @"
public class User
{
    [DataClassification(Classification.Confidential)]
    public string Email { get; set; }
}

public enum Classification { Confidential }
public class DataClassificationAttribute : System.Attribute
{
    public DataClassificationAttribute(Classification c) { }
}";

        await CSharpCodeFixVerifier<PiiDetectionAnalyzer, EmptyCodeFixProvider, XUnitVerifier>
            .VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task Property_WithoutPiiName_NoDiagnostic()
    {
        const string test = @"
public class User
{
    public string Username { get; set; }
}";

        await CSharpCodeFixVerifier<PiiDetectionAnalyzer, EmptyCodeFixProvider, XUnitVerifier>
            .VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task Field_WithPiiName_NoAttribute_TriggersDiagnostic()
    {
        const string test = @"
public class User
{
    private string _ssn;
}";

        var expected = CSharpCodeFixVerifier<PiiDetectionAnalyzer, EmptyCodeFixProvider, XUnitVerifier>
            .Diagnostic(PiiDetectionAnalyzer.DiagnosticId)
            .WithLocation(4, 20)
            .WithArguments("_ssn");

        await CSharpCodeFixVerifier<PiiDetectionAnalyzer, EmptyCodeFixProvider, XUnitVerifier>
            .VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task Property_WithPasswordName_TriggersDiagnostic()
    {
        const string test = @"
public class Credentials
{
    public string Password { get; set; }
}";

        var expected = CSharpCodeFixVerifier<PiiDetectionAnalyzer, EmptyCodeFixProvider, XUnitVerifier>
            .Diagnostic(PiiDetectionAnalyzer.DiagnosticId)
            .WithLocation(4, 19)
            .WithArguments("Password");

        await CSharpCodeFixVerifier<PiiDetectionAnalyzer, EmptyCodeFixProvider, XUnitVerifier>
            .VerifyAnalyzerAsync(test, expected);
    }
}

/// <summary>
/// Empty code fix provider for analyzer tests.
/// </summary>
public sealed class EmptyCodeFixProvider : Microsoft.CodeAnalysis.CodeFixes.CodeFixProvider
{
    public override System.Collections.Immutable.ImmutableArray<string> FixableDiagnosticIds =>
        System.Collections.Immutable.ImmutableArray<string>.Empty;

    public override Task RegisterCodeFixesAsync(Microsoft.CodeAnalysis.CodeFixes.CodeFixContext context) =>
        Task.CompletedTask;
}
