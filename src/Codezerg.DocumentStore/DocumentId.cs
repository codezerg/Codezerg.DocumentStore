using System;
using System.Security.Cryptography;
using System.Threading;

namespace Codezerg.DocumentStore;

/// <summary>
/// Represents a unique identifier for a document.
/// </summary>
public readonly struct DocumentId : IEquatable<DocumentId>, IComparable<DocumentId>
{
    private static int _counter = Compatibility.GetRandomInt32(0, 0xFFFFFF);
    private readonly byte[] _value;

    /// <summary>
    /// Gets an empty DocumentId.
    /// </summary>
    public static DocumentId Empty => new(new byte[12]);

    /// <summary>
    /// Initializes a new instance of the <see cref="DocumentId"/> struct.
    /// </summary>
    public DocumentId()
    {
        _value = Generate();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DocumentId"/> struct from a byte array.
    /// </summary>
    /// <param name="value">The byte array representing the ID.</param>
    public DocumentId(byte[] value)
    {
        if (value == null || value.Length != 12)
            throw new ArgumentException("DocumentId must be exactly 12 bytes", nameof(value));

        _value = value;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DocumentId"/> struct from a hex string.
    /// </summary>
    /// <param name="hexString">The hex string representing the ID.</param>
    public DocumentId(string hexString)
    {
        if (string.IsNullOrEmpty(hexString) || hexString.Length != 24)
            throw new ArgumentException("DocumentId hex string must be exactly 24 characters", nameof(hexString));

        _value = Compatibility.FromHexString(hexString);
    }

    /// <summary>
    /// Gets the timestamp component of this DocumentId.
    /// </summary>
    public DateTime Timestamp
    {
        get
        {
            if (_value == null || _value.Length != 12)
                return DateTime.MinValue;

            var timestamp = _value[0] << 24 | _value[1] << 16 | _value[2] << 8 | _value[3];
            return DateTimeOffset.FromUnixTimeSeconds(timestamp).UtcDateTime;
        }
    }

    /// <summary>
    /// Generates a new DocumentId.
    /// </summary>
    /// <returns>A new unique DocumentId.</returns>
    public static DocumentId NewId() => new();

    /// <summary>
    /// Parses a hex string into a DocumentId.
    /// </summary>
    public static DocumentId Parse(string hexString) => new(hexString);

    /// <summary>
    /// Tries to parse a hex string into a DocumentId.
    /// </summary>
    public static bool TryParse(string? hexString, out DocumentId id)
    {
        if (string.IsNullOrEmpty(hexString) || hexString!.Length != 24)
        {
            id = Empty;
            return false;
        }

        try
        {
            id = new DocumentId(hexString);
            return true;
        }
        catch
        {
            id = Empty;
            return false;
        }
    }

    /// <summary>
    /// Converts the DocumentId to a hex string.
    /// </summary>
    public override string ToString() => _value == null ? string.Empty : Compatibility.ToHexString(_value);

    /// <summary>
    /// Gets the byte array representation of this DocumentId.
    /// </summary>
    public byte[] ToByteArray() => _value ?? Array.Empty<byte>();

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is DocumentId id && Equals(id);

    /// <inheritdoc/>
    public bool Equals(DocumentId other)
    {
        if (_value == null && other._value == null) return true;
        if (_value == null || other._value == null) return false;
        if (_value.Length != other._value.Length) return false;

        for (int i = 0; i < _value.Length; i++)
        {
            if (_value[i] != other._value[i]) return false;
        }
        return true;
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return Compatibility.GetHashCodeForBytes(_value);
    }

    /// <inheritdoc/>
    public int CompareTo(DocumentId other)
    {
        if (_value == null && other._value == null) return 0;
        if (_value == null) return -1;
        if (other._value == null) return 1;

        for (int i = 0; i < Math.Min(_value.Length, other._value.Length); i++)
        {
            var cmp = _value[i].CompareTo(other._value[i]);
            if (cmp != 0) return cmp;
        }
        return _value.Length.CompareTo(other._value.Length);
    }

    /// <summary>
    /// Determines whether two DocumentId instances are equal.
    /// </summary>
    public static bool operator ==(DocumentId left, DocumentId right) => left.Equals(right);

    /// <summary>
    /// Determines whether two DocumentId instances are not equal.
    /// </summary>
    public static bool operator !=(DocumentId left, DocumentId right) => !left.Equals(right);

    /// <summary>
    /// Determines whether one DocumentId is less than another.
    /// </summary>
    public static bool operator <(DocumentId left, DocumentId right) => left.CompareTo(right) < 0;

    /// <summary>
    /// Determines whether one DocumentId is greater than another.
    /// </summary>
    public static bool operator >(DocumentId left, DocumentId right) => left.CompareTo(right) > 0;

    /// <summary>
    /// Determines whether one DocumentId is less than or equal to another.
    /// </summary>
    public static bool operator <=(DocumentId left, DocumentId right) => left.CompareTo(right) <= 0;

    /// <summary>
    /// Determines whether one DocumentId is greater than or equal to another.
    /// </summary>
    public static bool operator >=(DocumentId left, DocumentId right) => left.CompareTo(right) >= 0;

    private static byte[] Generate()
    {
        var bytes = new byte[12];

        // Timestamp (4 bytes)
        var timestamp = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        bytes[0] = (byte)(timestamp >> 24);
        bytes[1] = (byte)(timestamp >> 16);
        bytes[2] = (byte)(timestamp >> 8);
        bytes[3] = (byte)timestamp;

        // Random value (5 bytes)
        Compatibility.FillRandom(bytes, 4, 5);

        // Counter (3 bytes)
        var counter = Interlocked.Increment(ref _counter) & 0xFFFFFF;
        bytes[9] = (byte)(counter >> 16);
        bytes[10] = (byte)(counter >> 8);
        bytes[11] = (byte)counter;

        return bytes;
    }
}
