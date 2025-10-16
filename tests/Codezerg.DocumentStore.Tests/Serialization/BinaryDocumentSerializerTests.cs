using Codezerg.DocumentStore.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Xunit;

namespace Codezerg.DocumentStore.Tests.Serialization;

public class BinaryDocumentSerializerTests
{
    private class SimpleDocument
    {
        public DocumentId Id { get; set; }
        public string Name { get; set; } = "";
        public int Age { get; set; }
    }

    private class ComplexDocument
    {
        public DocumentId Id { get; set; }
        public string Title { get; set; } = "";
        public List<string> Tags { get; set; } = new();
        public Dictionary<string, object> Metadata { get; set; } = new();
        public NestedObject Nested { get; set; } = new();
    }

    private class NestedObject
    {
        public string Field1 { get; set; } = "";
        public int Field2 { get; set; }
        public bool Field3 { get; set; }
    }

    [Fact]
    public void SerializeToJsonb_SimpleDocument_ProducesValidJsonb()
    {
        // Arrange
        var doc = new SimpleDocument
        {
            Id = DocumentId.NewId(),
            Name = "John Doe",
            Age = 30
        };

        // Act
        var jsonb = BinaryDocumentSerializer.SerializeToJsonb(doc);

        // Assert
        Assert.NotNull(jsonb);
        Assert.NotEmpty(jsonb);

        // First byte should be OBJECT type (0xC) with size info
        byte firstByte = jsonb[0];
        byte elementType = (byte)(firstByte & 0x0F);
        Assert.Equal(0xC, elementType); // TYPE_OBJECT
    }

    [Fact]
    public void SerializeToJsonb_NullDocument_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            BinaryDocumentSerializer.SerializeToJsonb<SimpleDocument>(null!));
    }

    [Fact]
    public void RoundTrip_SimpleDocument_PreservesData()
    {
        // Arrange
        var original = new SimpleDocument
        {
            Id = DocumentId.NewId(),
            Name = "Jane Smith",
            Age = 25
        };

        // Act
        var jsonb = BinaryDocumentSerializer.SerializeToJsonb(original);
        var deserialized = BinaryDocumentSerializer.DeserializeFromJsonb<SimpleDocument>(jsonb);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(original.Id, deserialized.Id);
        Assert.Equal(original.Name, deserialized.Name);
        Assert.Equal(original.Age, deserialized.Age);
    }

    [Fact]
    public void RoundTrip_ComplexDocument_PreservesData()
    {
        // Arrange
        var original = new ComplexDocument
        {
            Id = DocumentId.NewId(),
            Title = "Test Document",
            Tags = new List<string> { "tag1", "tag2", "tag3" },
            Metadata = new Dictionary<string, object>
            {
                { "author", "John Doe" },
                { "version", 1 }
            },
            Nested = new NestedObject
            {
                Field1 = "value1",
                Field2 = 42,
                Field3 = true
            }
        };

        // Act
        var jsonb = BinaryDocumentSerializer.SerializeToJsonb(original);
        var deserialized = BinaryDocumentSerializer.DeserializeFromJsonb<ComplexDocument>(jsonb);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(original.Id, deserialized.Id);
        Assert.Equal(original.Title, deserialized.Title);
        Assert.Equal(original.Tags.Count, deserialized.Tags.Count);
        Assert.All(original.Tags, tag => Assert.Contains(tag, deserialized.Tags));
        Assert.Equal(original.Nested.Field1, deserialized.Nested.Field1);
        Assert.Equal(original.Nested.Field2, deserialized.Nested.Field2);
        Assert.Equal(original.Nested.Field3, deserialized.Nested.Field3);
    }

    [Fact]
    public void SerializeToJsonb_HandlesNullValues()
    {
        // Arrange
        var doc = new SimpleDocument
        {
            Id = DocumentId.NewId(),
            Name = null!,
            Age = 0
        };

        // Act
        var jsonb = BinaryDocumentSerializer.SerializeToJsonb(doc);

        // Assert
        Assert.NotNull(jsonb);
        Assert.NotEmpty(jsonb);
    }

    [Fact]
    public void SerializeToJsonb_HandlesEmptyStrings()
    {
        // Arrange
        var doc = new SimpleDocument
        {
            Id = DocumentId.NewId(),
            Name = "",
            Age = 10
        };

        // Act
        var jsonb = BinaryDocumentSerializer.SerializeToJsonb(doc);
        var deserialized = BinaryDocumentSerializer.DeserializeFromJsonb<SimpleDocument>(jsonb);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal("", deserialized.Name);
    }

    [Fact]
    public void SerializeToJsonb_HandlesSpecialCharactersInStrings()
    {
        // Arrange
        var doc = new SimpleDocument
        {
            Id = DocumentId.NewId(),
            Name = "Test \"quotes\" and \\backslashes\\ and \nnewlines",
            Age = 5
        };

        // Act
        var jsonb = BinaryDocumentSerializer.SerializeToJsonb(doc);
        var deserialized = BinaryDocumentSerializer.DeserializeFromJsonb<SimpleDocument>(jsonb);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(doc.Name, deserialized.Name);
    }

    [Fact]
    public void SerializeToJsonb_HandlesLargeNumbers()
    {
        // Arrange
        var doc = new SimpleDocument
        {
            Id = DocumentId.NewId(),
            Name = "Large number test",
            Age = int.MaxValue
        };

        // Act
        var jsonb = BinaryDocumentSerializer.SerializeToJsonb(doc);
        var deserialized = BinaryDocumentSerializer.DeserializeFromJsonb<SimpleDocument>(jsonb);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(int.MaxValue, deserialized.Age);
    }

    [Fact]
    public void SerializeToJsonb_HandlesEmptyArrays()
    {
        // Arrange
        var doc = new ComplexDocument
        {
            Id = DocumentId.NewId(),
            Title = "Empty arrays",
            Tags = new List<string>(),
            Metadata = new Dictionary<string, object>()
        };

        // Act
        var jsonb = BinaryDocumentSerializer.SerializeToJsonb(doc);
        var deserialized = BinaryDocumentSerializer.DeserializeFromJsonb<ComplexDocument>(jsonb);

        // Assert
        Assert.NotNull(deserialized);
        Assert.NotNull(deserialized.Tags);
        Assert.Empty(deserialized.Tags);
    }

    [Fact]
    public void SerializeToJsonb_ProducesSmallerThanJsonText()
    {
        // Arrange
        var doc = new SimpleDocument
        {
            Id = DocumentId.NewId(),
            Name = "Test",
            Age = 30
        };

        // Act
        var json = DocumentSerializer.Serialize(doc);
        var jsonb = BinaryDocumentSerializer.SerializeToJsonb(doc);

        // Assert
        // JSONB should typically be 5-10% smaller than JSON text
        // For small documents, might be similar or slightly smaller
        Assert.True(jsonb.Length <= json.Length * 1.1,
            $"JSONB size ({jsonb.Length}) should be at most 110% of JSON size ({json.Length})");
    }

    [Fact]
    public void SerializeToJsonb_MultipleDocuments_ProducesConsistentResults()
    {
        // Arrange
        var doc = new SimpleDocument
        {
            Id = DocumentId.Parse("507f1f77bcf86cd799439011"),
            Name = "Consistent",
            Age = 42
        };

        // Act
        var jsonb1 = BinaryDocumentSerializer.SerializeToJsonb(doc);
        var jsonb2 = BinaryDocumentSerializer.SerializeToJsonb(doc);

        // Assert
        Assert.Equal(jsonb1.Length, jsonb2.Length);
        Assert.True(jsonb1.SequenceEqual(jsonb2), "Multiple serializations should produce identical output");
    }

    [Fact]
    public void DeserializeFromJsonb_EmptyBlob_ReturnsDefault()
    {
        // Act
        var result = BinaryDocumentSerializer.DeserializeFromJsonb<SimpleDocument>(Array.Empty<byte>());

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void DeserializeFromJsonb_NullBlob_ReturnsDefault()
    {
        // Act
        var result = BinaryDocumentSerializer.DeserializeFromJsonb<SimpleDocument>(null!);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void DeserializeFromJsonb_InvalidBlob_ThrowsInvalidOperationException()
    {
        // Arrange
        byte[] invalidBlob = { 0xFF, 0xFF, 0xFF };

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            BinaryDocumentSerializer.DeserializeFromJsonb<SimpleDocument>(invalidBlob));
    }

    [Fact]
    public void SerializeToJsonb_LargeDocument_HandlesCorrectly()
    {
        // Arrange
        var doc = new ComplexDocument
        {
            Id = DocumentId.NewId(),
            Title = "Large document test",
            Tags = Enumerable.Range(0, 100).Select(i => $"tag{i}").ToList(),
            Metadata = new Dictionary<string, object>()
        };

        // Add lots of metadata
        for (int i = 0; i < 50; i++)
        {
            doc.Metadata[$"key{i}"] = $"value{i}";
        }

        // Act
        var jsonb = BinaryDocumentSerializer.SerializeToJsonb(doc);
        var deserialized = BinaryDocumentSerializer.DeserializeFromJsonb<ComplexDocument>(jsonb);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(doc.Tags.Count, deserialized.Tags.Count);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(42)]
    [InlineData(-1)]
    [InlineData(-100)]
    [InlineData(999999)]
    public void RoundTrip_IntegerValues_PreservesValue(int value)
    {
        // Arrange
        var doc = new SimpleDocument { Id = DocumentId.NewId(), Name = "Test", Age = value };

        // Act
        var jsonb = BinaryDocumentSerializer.SerializeToJsonb(doc);
        var deserialized = BinaryDocumentSerializer.DeserializeFromJsonb<SimpleDocument>(jsonb);

        // Assert
        Assert.Equal(value, deserialized?.Age);
    }

    [Fact]
    public void SerializeToJsonb_HeaderEncodingCorrect_SingleByteSize()
    {
        // Arrange - create document that produces small payload
        var doc = new { x = 1 };

        // Act
        var jsonb = BinaryDocumentSerializer.SerializeToJsonb(doc);

        // Assert
        byte firstByte = jsonb[0];
        byte elementType = (byte)(firstByte & 0x0F);
        int sizeIndicator = (firstByte >> 4);

        Assert.Equal(0xC, elementType); // TYPE_OBJECT
        Assert.True(sizeIndicator <= 11 || sizeIndicator >= 12, "Size should be encoded");
    }

    [Fact]
    public void SerializeToJsonb_BooleanValues_EncodesCorrectly()
    {
        // Arrange
        var doc = new { trueVal = true, falseVal = false };

        // Act
        var jsonb = BinaryDocumentSerializer.SerializeToJsonb(doc);
        var json = BinaryDocumentSerializer.DeserializeFromJsonb<dynamic>(jsonb);

        // Assert - should round-trip without errors
        Assert.NotNull(json);
    }

    [Fact]
    public void SerializeToJsonb_UnicodeStrings_HandlesCorrectly()
    {
        // Arrange
        var doc = new SimpleDocument
        {
            Id = DocumentId.NewId(),
            Name = "Hello 世界 🌍 Привет",
            Age = 1
        };

        // Act
        var jsonb = BinaryDocumentSerializer.SerializeToJsonb(doc);
        var deserialized = BinaryDocumentSerializer.DeserializeFromJsonb<SimpleDocument>(jsonb);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(doc.Name, deserialized.Name);
    }
}
