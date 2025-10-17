using Codezerg.DocumentStore;

namespace Codezerg.DocumentStore.Tests;

public class DocumentIdTests
{
    [Fact]
    public void NewId_ShouldGenerateUniqueIds()
    {
        var id1 = DocumentId.NewId();
        var id2 = DocumentId.NewId();

        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public void NewId_ShouldNotBeEmpty()
    {
        var id = DocumentId.NewId();

        Assert.NotEqual(DocumentId.Empty, id);
        Assert.NotEqual(string.Empty, id.ToString());
    }

    [Fact]
    public void ToString_ShouldReturnHexString()
    {
        var id = DocumentId.NewId();
        var str = id.ToString();

        Assert.NotNull(str);
        Assert.Equal(24, str.Length);
        Assert.Matches("^[0-9a-f]{24}$", str);
    }

    [Fact]
    public void Parse_ShouldParseValidHexString()
    {
        var original = DocumentId.NewId();
        var str = original.ToString();

        var parsed = DocumentId.Parse(str);

        Assert.Equal(original, parsed);
    }

    [Fact]
    public void Parse_ShouldThrowOnInvalidString()
    {
        Assert.Throws<ArgumentException>(() => DocumentId.Parse("invalid"));
        Assert.Throws<ArgumentException>(() => DocumentId.Parse("123"));
    }

    [Fact]
    public void TryParse_ShouldReturnTrueForValidString()
    {
        var original = DocumentId.NewId();
        var str = original.ToString();

        var success = DocumentId.TryParse(str, out var parsed);

        Assert.True(success);
        Assert.Equal(original, parsed);
    }

    [Fact]
    public void TryParse_ShouldReturnFalseForInvalidString()
    {
        var success = DocumentId.TryParse("invalid", out var parsed);

        Assert.False(success);
        Assert.Equal(DocumentId.Empty, parsed);
    }

    [Fact]
    public void Equals_ShouldReturnTrueForSameId()
    {
        var id1 = DocumentId.NewId();
        var id2 = DocumentId.Parse(id1.ToString());

        Assert.Equal(id1, id2);
        Assert.True(id1.Equals(id2));
        Assert.True(id1 == id2);
        Assert.False(id1 != id2);
    }

    [Fact]
    public void Equals_ShouldReturnFalseForDifferentIds()
    {
        var id1 = DocumentId.NewId();
        var id2 = DocumentId.NewId();

        Assert.NotEqual(id1, id2);
        Assert.False(id1.Equals(id2));
        Assert.True(id1 != id2);
        Assert.False(id1 == id2);
    }

    [Fact]
    public void GetHashCode_ShouldBeSameForEqualIds()
    {
        var id1 = DocumentId.NewId();
        var id2 = DocumentId.Parse(id1.ToString());

        Assert.Equal(id1.GetHashCode(), id2.GetHashCode());
    }

    [Fact]
    public void CompareTo_ShouldCompareCorrectly()
    {
        var id1 = DocumentId.Parse("000000000000000000000001");
        var id2 = DocumentId.Parse("000000000000000000000002");

        Assert.True(id1.CompareTo(id2) < 0);
        Assert.True(id2.CompareTo(id1) > 0);
        Assert.Equal(0, id1.CompareTo(id1));
    }

    [Fact]
    public void ComparisonOperators_ShouldWorkCorrectly()
    {
        var id1 = DocumentId.Parse("000000000000000000000001");
        var id2 = DocumentId.Parse("000000000000000000000002");

        Assert.True(id1 < id2);
        Assert.True(id1 <= id2);
        Assert.False(id1 > id2);
        Assert.False(id1 >= id2);
        Assert.True(id2 > id1);
        Assert.True(id2 >= id1);
    }

    [Fact]
    public void Empty_ShouldReturnEmptyId()
    {
        var empty = DocumentId.Empty;

        Assert.Equal("000000000000000000000000", empty.ToString());
    }
}
