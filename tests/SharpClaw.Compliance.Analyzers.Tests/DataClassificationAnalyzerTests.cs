using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Testing.Verifiers;
using Xunit;

namespace SharpClaw.Compliance.Analyzers.Tests;

public sealed class DataClassificationAnalyzerTests
{
    [Fact]
    public async Task Class_WithPiiProperties_NoClassification_TriggersDiagnostic()
    {
        const string test = @"
public class User
{
    public string Email { get; set; }
    public string Name { get; set; }
}";

        var expected = CSharpCodeFixVerifier<DataClassificationAnalyzer, EmptyCodeFixProvider, XUnitVerifier>
            .Diagnostic(DataClassificationAnalyzer.MissingClassificationDiagnosticId)
            .WithLocation(2, 14)
            .WithArguments("User");

        await CSharpCodeFixVerifier<DataClassificationAnalyzer, EmptyCodeFixProvider, XUnitVerifier>
            .VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task Class_WithPiiProperties_WithClassification_NoDiagnostic()
    {
        const string test = @"
[DataClassification(Classification.Confidential)]
public class User
{
    public string Email { get; set; }
    public string Name { get; set; }
}

public enum Classification { Confidential }
public class DataClassificationAttribute : System.Attribute
{
    public DataClassificationAttribute(Classification c) { }
}";

        await CSharpCodeFixVerifier<DataClassificationAnalyzer, EmptyCodeFixProvider, XUnitVerifier>
            .VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task Property_WithPii_PublicClassification_TriggersDiagnostic()
    {
        const string test = @"
public class User
{
    [DataClassification(Classification.Public)]
    public string SSN { get; set; }
}

public enum Classification { Public, Confidential }
public class DataClassificationAttribute : System.Attribute
{
    public DataClassificationAttribute(Classification c) { }
}";

        var expected = CSharpCodeFixVerifier<DataClassificationAnalyzer, EmptyCodeFixProvider, XUnitVerifier>
            .Diagnostic(DataClassificationAnalyzer.InvalidLevelDiagnosticId)
            .WithLocation(5, 19)
            .WithArguments("SSN", "Public");

        await CSharpCodeFixVerifier<DataClassificationAnalyzer, EmptyCodeFixProvider, XUnitVerifier>
            .VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task Record_WithPiiProperties_NoClassification_TriggersDiagnostic()
    {
        const string test = @"
public record Customer
{
    public string CreditCard { get; init; }
}";

        var expected = CSharpCodeFixVerifier<DataClassificationAnalyzer, EmptyCodeFixProvider, XUnitVerifier>
            .Diagnostic(DataClassificationAnalyzer.MissingClassificationDiagnosticId)
            .WithLocation(2, 15)
            .WithArguments("Customer");

        await CSharpCodeFixVerifier<DataClassificationAnalyzer, EmptyCodeFixProvider, XUnitVerifier>
            .VerifyAnalyzerAsync(test, expected);
    }
}
