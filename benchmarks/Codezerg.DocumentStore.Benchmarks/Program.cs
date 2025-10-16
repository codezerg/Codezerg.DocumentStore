using BenchmarkDotNet.Running;
using Codezerg.DocumentStore.Benchmarks;
using System;

namespace Codezerg.DocumentStore.Benchmarks;

public class Program
{
    public static void Main(string[] args)
    {
        Console.WriteLine("Codezerg.DocumentStore - Performance Benchmarks");
        Console.WriteLine("================================================");
        Console.WriteLine();
        Console.WriteLine("Benchmarking JSON serialization and SQLite operations:");
        Console.WriteLine("- Serialization (System.Text.Json)");
        Console.WriteLine("- Database inserts with SQLite jsonb()");
        Console.WriteLine("- Database reads and deserialization");
        Console.WriteLine();
        Console.WriteLine("Running benchmarks...");
        Console.WriteLine();

        var summary = BenchmarkRunner.Run<JsonbSerializationBenchmarks>();

        Console.WriteLine();
        Console.WriteLine("Benchmarks complete! Check BenchmarkDotNet.Artifacts for detailed results.");
    }
}
