using System;
using System.Reflection;
using Microsoft.Agents.AI;
public class Dump {
    public static void Main() {
        foreach (var t in typeof(AIAgent).Assembly.GetExportedTypes()) {
            Console.WriteLine(t.FullName);
        }
    }
}
