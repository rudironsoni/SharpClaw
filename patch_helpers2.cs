using System;
using System.IO;

class Program {
    static void Main() {
        var files = new[] {
            "tests/SharpClaw.Runs.UnitTests/TestHelpers.cs",
            "tests/SharpClaw.Runs.IntegrationTests/TestHelpers.cs",
            "tests/SharpClaw.Gateway.End2EndTests/TestHelpers.cs",
            "tests/SharpClaw.Gateway.IntegrationTests/TestHelpers.cs"
        };
        foreach(var f in files) {
            var content = File.ReadAllText(f);
            content = content.Replace("using SharpClaw.Abstractions.Execution;", "using SharpClaw.Execution.Abstractions;");
            content = content.Replace("Task<SandboxHandle> StartAsync(SharpClaw.Abstractions.Execution.SandboxStartRequest request, CancellationToken cancellationToken = default)", "Task<SandboxHandle> StartAsync(CancellationToken cancellationToken = default)");
            content = content.Replace("new[] { dummyProvider }", "new SharpClaw.Execution.Abstractions.ISandboxProvider[] { dummyProvider }");
            File.WriteAllText(f, content);
        }
    }
}
