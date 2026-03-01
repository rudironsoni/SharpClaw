using System;
using System.Reflection;
using Microsoft.Extensions.AI;

class Program
{
    static void Main()
    {
        Console.WriteLine("FunctionResultContent properties:");
        foreach (var p in typeof(FunctionResultContent).GetProperties())
        {
            Console.WriteLine("  " + p.Name + " - " + p.PropertyType.Name);
        }
    }
}
