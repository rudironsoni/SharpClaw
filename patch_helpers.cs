using System;
using System.IO;

class Program {
    static void Main() {
        var files = new[] {
            "tests/SharpClaw.Runs.UnitTests/TestHelpers.cs",
            "tests/SharpClaw.Runs.IntegrationTests/TestHelpers.cs",
            "tests/SharpClaw.Gateway.End2EndTests/TestHelpers.cs"
        };
        foreach(var f in files) {
            var content = File.ReadAllText(f);
            content = content.Replace("using SharpClaw.Execution.Abstractions;", "using SharpClaw.Abstractions.Execution;");
            content = content.Replace("public Task<SandboxHandle> StartAsync(CancellationToken cancellationToken = default)", "public Task<SandboxHandle> StartAsync(SandboxStartRequest request, CancellationToken cancellationToken = default)");
            content = content.Replace("public Task<ExecutionResult> ExecuteCommandAsync(SandboxHandle handle, string command, TimeSpan? timeout = null, CancellationToken cancellationToken = default) => Task.FromResult(new ExecutionResult(0, \"\", \"\"));", "");
            File.WriteAllText(f, content);
        }
    }
}
