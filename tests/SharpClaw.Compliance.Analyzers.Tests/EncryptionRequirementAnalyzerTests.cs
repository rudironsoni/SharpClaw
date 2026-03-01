using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Testing.Verifiers;
using Xunit;

namespace SharpClaw.Compliance.Analyzers.Tests;

public sealed class EncryptionRequirementAnalyzerTests
{
    [Fact]
    public async Task Property_WithPassword_PlainString_TriggersDiagnostic()
    {
        const string test = @"
public class Credentials
{
    public string Password { get; set; }
}";

        var expected = CSharpCodeFixVerifier<EncryptionRequirementAnalyzer, EmptyCodeFixProvider, XUnitVerifier>
            .Diagnostic(EncryptionRequirementAnalyzer.PlainTextStorageDiagnosticId)
            .WithLocation(4, 19)
            .WithArguments("Password");

        await CSharpCodeFixVerifier<EncryptionRequirementAnalyzer, EmptyCodeFixProvider, XUnitVerifier>
            .VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task Variable_WithApiKey_LiteralAssignment_TriggersDiagnostic()
    {
        const string test = @"
public class Test
{
    public void Method()
    {
        string apiKey = ""secret123"";
    }
}";

        var expected = CSharpCodeFixVerifier<EncryptionRequirementAnalyzer, EmptyCodeFixProvider, XUnitVerifier>
            .Diagnostic(EncryptionRequirementAnalyzer.PlainTextStorageDiagnosticId)
            .WithLocation(6, 16)
            .WithArguments("apiKey");

        await CSharpCodeFixVerifier<EncryptionRequirementAnalyzer, EmptyCodeFixProvider, XUnitVerifier>
            .VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task Assignment_WithSecret_Literal_TriggersDiagnostic()
    {
        const string test = @"
public class Test
{
    public void Method()
    {
        string secret = null;
        secret = ""mysecret"";
    }
}";

        var expected = CSharpCodeFixVerifier<EncryptionRequirementAnalyzer, EmptyCodeFixProvider, XUnitVerifier>
            .Diagnostic(EncryptionRequirementAnalyzer.PlainTextStorageDiagnosticId)
            .WithLocation(7, 9)
            .WithArguments("secret");

        await CSharpCodeFixVerifier<EncryptionRequirementAnalyzer, EmptyCodeFixProvider, XUnitVerifier>
            .VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task Property_WithEncryptionMethods_NoDiagnostic()
    {
        const string test = @"
public class Credentials
{
    private string _encryptedPassword;
    
    public string Password 
    { 
        get => Decrypt(_encryptedPassword);
        set => _encryptedPassword = Encrypt(value);
    }
    
    private string Encrypt(string value) => value;
    private string Decrypt(string value) => value;
}";

        await CSharpCodeFixVerifier<EncryptionRequirementAnalyzer, EmptyCodeFixProvider, XUnitVerifier>
            .VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task Variable_WithWeakEncryption_TriggersDiagnostic()
    {
        const string test = @"
using System.Security.Cryptography;

public class Test
{
    public void Method()
    {
        var encrypted = DES.Create();
    }
}";

        var expected = CSharpCodeFixVerifier<EncryptionRequirementAnalyzer, EmptyCodeFixProvider, XUnitVerifier>
            .Diagnostic(EncryptionRequirementAnalyzer.WeakEncryptionDiagnosticId)
            .WithLocation(8, 23)
            .WithArguments("encrypted", "DES");

        await CSharpCodeFixVerifier<EncryptionRequirementAnalyzer, EmptyCodeFixProvider, XUnitVerifier>
            .VerifyAnalyzerAsync(test, expected);
    }
}
