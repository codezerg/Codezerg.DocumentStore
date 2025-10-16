using BenchmarkDotNet.Running;
using Codezerg.DocumentStore.Benchmarks;
using System;

namespace Codezerg.DocumentStore.Benchmarks;

public class Program
{
    public static void Main(string[] args)
    {
        Console.WriteLine("Codezerg.DocumentStore - JSONB Serialization Benchmarks");
        Console.WriteLine("=========================================================");
        Console.WriteLine();
        Console.WriteLine("This benchmark compares:");
        Console.WriteLine("1. JSON text serialization (System.Text.Json)");
        Console.WriteLine("2. JSON text + SQLite jsonb() conversion");
        Console.WriteLine("3. Direct JSONB binary serialization (BinaryDocumentSerializer)");
        Console.WriteLine();
        Console.WriteLine("Running benchmarks...");
        Console.WriteLine();

        var summary = BenchmarkRunner.Run<JsonbSerializationBenchmarks>();

        Console.WriteLine();
        Console.WriteLine("Benchmarks complete! Check BenchmarkDotNet.Artifacts for detailed results.");
    }
}
