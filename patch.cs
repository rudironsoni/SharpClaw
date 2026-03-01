using System;
using System.IO;

class Program {
    static void Main() {
        var path = "src/SharpClaw.Gateway/GatewayCore.cs";
        var content = File.ReadAllText(path);
        content = content.Replace("new EventFrame($\"runs.{runId}\", runEvent.Event, runEvent.Data);", "new EventFrame(runEvent.Event, runEvent.Data, runEvent.Seq);");
        File.WriteAllText(path, content);
    }
}
