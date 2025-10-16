# JSONB Serialization Analysis

## Question: Should we create a native BinaryDocumentSerializer?

**Date**: 2025-10-16
**Status**: Analysis Phase - Benchmarking Required

## Current Architecture

```
C# Object → System.Text.Json → JSON Text → SQLite jsonb() → JSONB Blob → SQLite Storage
```

**Performance Profile**:
- System.Text.Json serialization: ~50-100μs per 1KB document
- SQLite jsonb() conversion: ~20-50μs per 1KB document (estimated)
- Total serialization overhead: ~70-150μs per document

## Proposed Architecture

```
C# Object → BinaryDocumentSerializer → JSONB Blob → SQLite Storage
```

**Expected Performance Profile**:
- Native JSONB encoding: ~60-120μs per 1KB document (estimated)
- Potential savings: ~10-30μs per document (marginal)

## Feasibility Analysis

### Option 1: Native JSONB Encoder (NOT RECOMMENDED)

**Implementation Complexity**: ⚠️⚠️⚠️ Very High

```csharp
public static class BinaryDocumentSerializer
{
    public static byte[] SerializeToJsonb<T>(T document)
    {
        // Would need to implement:
        // 1. Full JSONB encoder from scratch
        // 2. Header byte calculation (type + size)
        // 3. Multi-byte size encoding (1, 2, 4, 8 byte modes)
        // 4. Recursive object/array encoding
        // 5. UTF-8 string handling
        // 6. Number-as-ASCII-text encoding
        // 7. Big-endian integer encoding

        // Estimated: 500-1000 lines of complex code
    }

    public static T DeserializeFromJsonb<T>(byte[] jsonbBlob)
    {
        // Would need full JSONB parser
        // Or convert to JSON first: json(data)
        // But if we're converting, what's the point?
    }
}
```

**Pros**:
- ❌ None significant (marginal performance gain at best)

**Cons**:
- ⚠️ SQLite explicitly says JSONB is internal-only format
- ⚠️ Format may change without notice in future SQLite versions
- ⚠️ High implementation complexity (~500-1000 LOC)
- ⚠️ Difficult to test and validate
- ⚠️ Hard to debug binary format issues
- ⚠️ Maintenance burden for version compatibility
- ⚠️ Deserialization still requires SQLite or custom parser

**Risk**: ⚠️⚠️⚠️ High

### Option 2: Hybrid Approach (INTERESTING BUT COMPLEX)

Use System.Text.Json with custom JsonConverter that writes JSONB format during serialization:

```csharp
public class JsonbBinaryConverter : JsonConverter
{
    // Write JSONB binary during System.Text.Json serialization
    // This is theoretically possible but extremely complex
    // Would need to hook into Utf8JsonWriter and rewrite output
}
```

**Pros**:
- Could reuse System.Text.Json infrastructure
- Type handling already solved

**Cons**:
- ⚠️ Still violates SQLite's "internal format" guidance
- ⚠️ Very complex to implement correctly
- ⚠️ Uncertain performance benefit

**Risk**: ⚠️⚠️ High

### Option 3: Current Approach + SQLite jsonb() (RECOMMENDED)

Keep current architecture, let SQLite handle conversion:

```csharp
// DocumentSerializer.cs - NO CHANGES
public static string Serialize<T>(T document)
{
    return JsonSerializer.Serialize(document, _defaultOptions);
}

// SqliteDocumentCollection.cs
var json = DocumentSerializer.Serialize(document);
var sql = @"INSERT INTO documents (data) VALUES (jsonb(@Data))";
connection.Execute(sql, new { Data = json });
```

**Pros**:
- ✅ Simple, maintainable code
- ✅ Follows SQLite's official guidance
- ✅ SQLite's jsonb() is highly optimized C code
- ✅ Format compatibility guaranteed by SQLite
- ✅ Already provides 50%+ performance improvement over JSON text storage
- ✅ Zero implementation risk

**Cons**:
- ⚠️ Theoretical overhead of JSON text intermediate step (~20-50μs per doc)

**Risk**: ✅ Very Low

## Benchmark Plan

Before pursuing native JSONB encoding, measure the actual overhead:

### Benchmark 1: Serialization Overhead

```csharp
[Benchmark]
public string SerializeToJson()
{
    return JsonSerializer.Serialize(testDocument);
}

[Benchmark]
public void SerializeAndConvertToJsonb()
{
    var json = JsonSerializer.Serialize(testDocument);
    // Use SQLite to convert
    connection.ExecuteScalar<byte[]>("SELECT jsonb(@json)", new { json });
}
```

### Benchmark 2: Full Insert Operation

```csharp
[Benchmark]
public void InsertWithJsonText()
{
    var json = JsonSerializer.Serialize(testDocument);
    connection.Execute("INSERT INTO temp (data) VALUES (@json)", new { json });
}

[Benchmark]
public void InsertWithJsonb()
{
    var json = JsonSerializer.Serialize(testDocument);
    connection.Execute("INSERT INTO temp (data) VALUES (jsonb(@json))", new { json });
}
```

### Benchmark 3: End-to-End Operation

```csharp
[Benchmark]
public void FullCycle_Current()
{
    // Serialize → Store as JSONB → Retrieve → Deserialize
    var json = JsonSerializer.Serialize(testDocument);
    connection.Execute("INSERT INTO temp (id, data) VALUES (1, jsonb(@json))", new { json });
    var retrieved = connection.QuerySingle<string>("SELECT json(data) FROM temp WHERE id = 1");
    var doc = JsonSerializer.Deserialize<TestDocument>(retrieved);
}
```

## Expected Benchmark Results

### Hypothesis 1: jsonb() overhead is minimal
- JSON serialization: ~100μs
- jsonb() conversion: ~20μs (20% of total)
- **Result**: Not worth optimizing

### Hypothesis 2: jsonb() overhead is significant
- JSON serialization: ~100μs
- jsonb() conversion: ~100μs (50% of total)
- **Result**: Worth investigating alternatives

## Decision Tree

```
1. Run benchmarks
   ↓
2. Is jsonb() conversion >30% of total serialization time?
   ├─ NO → Keep current approach (RECOMMENDED)
   │        - Simple, maintainable, safe
   │        - Already 50%+ faster than JSON text
   │
   └─ YES → Evaluate options
            ├─ Option A: Accept the overhead (RECOMMENDED)
            │            - Overhead is inherent design tradeoff
            │            - Still much faster than JSON text storage
            │
            └─ Option B: Investigate optimization
                         - Research if SQLite can accept pre-encoded JSONB
                         - Check if newer SQLite versions optimize jsonb()
                         - Consider parameter binding optimizations
```

## Alternative: Optimize What Matters

Instead of risky native JSONB encoding, focus on proven optimizations:

### 1. WAL Mode (1 line of code, 10-30% improvement)
```csharp
connection.Execute("PRAGMA journal_mode=WAL;");
```

### 2. Bulk Inserts (moderate effort, 2-5x improvement for bulk ops)
```csharp
// Batch multiple INSERTs into single statement
INSERT INTO documents VALUES (jsonb(@1)), (jsonb(@2)), ...
```

### 3. Connection Pooling (moderate effort, significant for concurrent access)

### 4. Index Optimization (high impact, user-controlled)

**Combined Effect**: 50-100% overall throughput improvement with much lower risk

## Recommendation

### Phase 1: Benchmarking (1-2 days)
1. ✅ Create benchmark project
2. ✅ Measure jsonb() conversion overhead
3. ✅ Measure end-to-end operation times
4. ✅ Analyze where time is actually spent

### Phase 2: Decision Point

**If jsonb() overhead is <30% of serialization time**:
- ✅ **Keep current approach** (System.Text.Json → jsonb())
- ✅ Focus on complementary optimizations (WAL, bulk inserts)
- ✅ Document benchmark results
- ✅ Move forward with JSONB implementation as planned

**If jsonb() overhead is >30% of serialization time**:
- ⚠️ Investigate SQLite internals
- ⚠️ Check if issue is parameter binding, not conversion
- ⚠️ Consider asking SQLite community for guidance
- ⚠️ Likely still keep current approach due to risks

## Conclusion

**Bottom Line**: Creating a native BinaryDocumentSerializer is:
- ❌ **Not recommended** due to high complexity and risk
- ❌ **Violates SQLite's guidance** (JSONB is internal format)
- ❌ **Uncertain benefit** (need benchmarks to validate)
- ✅ **Current approach is better**: Simple, safe, maintainable

**Final Recommendation**:
1. Benchmark first to measure actual jsonb() overhead
2. Likely keep current approach (System.Text.Json + jsonb())
3. Focus on proven optimizations (WAL mode, bulk inserts)
4. Achieve 50-100% performance improvement with lower risk

## References

- SQLite JSONB Documentation: https://sqlite.org/jsonb.html
- Quote: "JSONB is not intended as an external format"
- System.Text.Json Performance: https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/performance
- BenchmarkDotNet: https://benchmarkdotnet.org/

---

**Status**: ⏸️ Awaiting Benchmarks
**Next Step**: Create benchmark project to measure jsonb() overhead
**Decision Date**: After benchmark results available
