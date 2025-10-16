using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Codezerg.DocumentStore.Serialization;

/// <summary>
/// Provides direct serialization to SQLite JSONB binary format without JSON text intermediate.
/// </summary>
/// <remarks>
/// ⚠️ WARNING: This serializer produces SQLite's internal JSONB format.
/// SQLite documentation states: "JSONB is not intended as an external format to be used by applications."
///
/// This implementation is EXPERIMENTAL and should be used with caution:
/// - The JSONB format may change in future SQLite versions
/// - Format compatibility is not guaranteed across SQLite versions
/// - This bypasses SQLite's official jsonb() conversion function
///
/// Recommended approach: Use DocumentSerializer (JSON text) + SQLite's jsonb() function
///
/// Performance note: The jsonb() conversion overhead is typically minimal (~20-50μs per document).
/// Direct JSONB encoding may not provide significant performance gains and adds complexity.
///
/// See: https://sqlite.org/jsonb.html
/// </remarks>
public static class BinaryDocumentSerializer
{
    // JSONB element type codes
    private const byte TYPE_NULL = 0x0;
    private const byte TYPE_TRUE = 0x1;
    private const byte TYPE_FALSE = 0x2;
    private const byte TYPE_INT = 0x3;
    private const byte TYPE_INT5 = 0x4;
    private const byte TYPE_FLOAT = 0x5;
    private const byte TYPE_FLOAT5 = 0x6;
    private const byte TYPE_TEXT = 0x7;
    private const byte TYPE_TEXTJ = 0x8;
    private const byte TYPE_TEXT5 = 0x9;
    private const byte TYPE_TEXTRAW = 0xA;
    private const byte TYPE_ARRAY = 0xB;
    private const byte TYPE_OBJECT = 0xC;

    /// <summary>
    /// Serializes a document to SQLite JSONB binary format.
    /// </summary>
    /// <typeparam name="T">The document type.</typeparam>
    /// <param name="document">The document to serialize.</param>
    /// <returns>JSONB binary blob.</returns>
    /// <exception cref="ArgumentNullException">Thrown when document is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when serialization fails.</exception>
    public static byte[] SerializeToJsonb<T>(T document)
    {
        if (document == null)
            throw new ArgumentNullException(nameof(document));

        try
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);

            WriteValue(writer, document, typeof(T));

            return stream.ToArray();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to serialize document to JSONB: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Deserializes a JSONB binary blob to a document.
    /// </summary>
    /// <typeparam name="T">The document type.</typeparam>
    /// <param name="jsonbBlob">The JSONB binary data.</param>
    /// <returns>The deserialized document.</returns>
    public static T? DeserializeFromJsonb<T>(byte[] jsonbBlob)
    {
        if (jsonbBlob == null || jsonbBlob.Length == 0)
            return default;

        try
        {
            using var stream = new MemoryStream(jsonbBlob);
            using var reader = new BinaryReader(stream);

            return (T?)ReadValue(reader, typeof(T));
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to deserialize JSONB to document: {ex.Message}", ex);
        }
    }

    private static void WriteValue(BinaryWriter writer, object? value, Type type)
    {
        if (value == null)
        {
            WriteHeader(writer, TYPE_NULL, 0);
            return;
        }

        // Handle primitive types
        if (type == typeof(bool))
        {
            WriteHeader(writer, (bool)value ? TYPE_TRUE : TYPE_FALSE, 0);
            return;
        }

        if (IsNumericType(type))
        {
            WriteNumber(writer, value);
            return;
        }

        if (type == typeof(string))
        {
            WriteString(writer, (string)value);
            return;
        }

        if (type == typeof(DocumentId))
        {
            WriteString(writer, value.ToString() ?? "");
            return;
        }

        if (type == typeof(DateTime))
        {
            WriteString(writer, ((DateTime)value).ToString("O"));
            return;
        }

        // Handle collections
        if (value is IEnumerable enumerable && type != typeof(string))
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>))
            {
                WriteDictionary(writer, (IDictionary)value);
                return;
            }

            WriteArray(writer, enumerable);
            return;
        }

        // Handle complex objects
        WriteObject(writer, value);
    }

    private static object? ReadValue(BinaryReader reader, Type targetType)
    {
        if (reader.BaseStream.Position >= reader.BaseStream.Length)
            throw new InvalidOperationException("Unexpected end of JSONB data");

        byte headerByte = reader.ReadByte();
        byte elementType = (byte)(headerByte & 0x0F);
        int sizeIndicator = (headerByte >> 4);

        int payloadSize = ReadPayloadSize(reader, sizeIndicator);

        switch (elementType)
        {
            case TYPE_NULL:
                return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;

            case TYPE_TRUE:
                return Convert.ChangeType(true, targetType);

            case TYPE_FALSE:
                return Convert.ChangeType(false, targetType);

            case TYPE_INT:
            case TYPE_FLOAT:
                return ReadNumber(reader, payloadSize, targetType);

            case TYPE_TEXT:
            case TYPE_TEXTJ:
            case TYPE_TEXT5:
            case TYPE_TEXTRAW:
                return ReadString(reader, payloadSize, targetType);

            case TYPE_ARRAY:
                return ReadArray(reader, payloadSize, targetType);

            case TYPE_OBJECT:
                return ReadObject(reader, payloadSize, targetType);

            default:
                throw new NotSupportedException($"Unknown JSONB element type: {elementType}");
        }
    }

    private static int ReadPayloadSize(BinaryReader reader, int sizeIndicator)
    {
        if (sizeIndicator <= 11)
        {
            return sizeIndicator;
        }
        else if (sizeIndicator == 12)
        {
            return reader.ReadByte();
        }
        else if (sizeIndicator == 13)
        {
            return (reader.ReadByte() << 8) | reader.ReadByte();
        }
        else if (sizeIndicator == 14)
        {
            return (reader.ReadByte() << 24) |
                   (reader.ReadByte() << 16) |
                   (reader.ReadByte() << 8) |
                   reader.ReadByte();
        }
        else
        {
            throw new NotSupportedException($"8-byte size encoding not supported");
        }
    }

    private static void WriteHeader(BinaryWriter writer, byte elementType, int payloadSize)
    {
        if (payloadSize < 0)
            throw new ArgumentException("Payload size cannot be negative", nameof(payloadSize));

        if (elementType > 0x0F)
            throw new ArgumentException("Element type must be 0-15", nameof(elementType));

        if (payloadSize <= 11)
        {
            byte header = (byte)((payloadSize << 4) | elementType);
            writer.Write(header);
        }
        else if (payloadSize <= 255)
        {
            writer.Write((byte)(0xC0 | elementType));
            writer.Write((byte)payloadSize);
        }
        else if (payloadSize <= 65535)
        {
            writer.Write((byte)(0xD0 | elementType));
            writer.Write((byte)(payloadSize >> 8));
            writer.Write((byte)(payloadSize & 0xFF));
        }
        else if (payloadSize <= 0x7FFFFFFF)
        {
            writer.Write((byte)(0xE0 | elementType));
            writer.Write((byte)(payloadSize >> 24));
            writer.Write((byte)((payloadSize >> 16) & 0xFF));
            writer.Write((byte)((payloadSize >> 8) & 0xFF));
            writer.Write((byte)(payloadSize & 0xFF));
        }
        else
        {
            throw new InvalidOperationException($"Payload size {payloadSize} exceeds maximum");
        }
    }

    private static bool IsNumericType(Type type)
    {
        return type == typeof(int) || type == typeof(long) || type == typeof(short) || type == typeof(byte) ||
               type == typeof(uint) || type == typeof(ulong) || type == typeof(ushort) || type == typeof(sbyte) ||
               type == typeof(float) || type == typeof(double) || type == typeof(decimal);
    }

    private static void WriteNumber(BinaryWriter writer, object value)
    {
        // SQLite JSONB stores numbers as ASCII text
        string numberText = Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? "0";

        var bytes = Encoding.ASCII.GetBytes(numberText);
        WriteHeader(writer, TYPE_INT, bytes.Length);
        writer.Write(bytes);
    }

    private static object ReadNumber(BinaryReader reader, int payloadSize, Type targetType)
    {
        var numberBytes = reader.ReadBytes(payloadSize);
        var numberText = Encoding.ASCII.GetString(numberBytes);

        if (targetType == typeof(int))
            return int.Parse(numberText);
        if (targetType == typeof(long))
            return long.Parse(numberText);
        if (targetType == typeof(double))
            return double.Parse(numberText, System.Globalization.CultureInfo.InvariantCulture);
        if (targetType == typeof(float))
            return float.Parse(numberText, System.Globalization.CultureInfo.InvariantCulture);
        if (targetType == typeof(decimal))
            return decimal.Parse(numberText, System.Globalization.CultureInfo.InvariantCulture);
        if (targetType == typeof(short))
            return short.Parse(numberText);
        if (targetType == typeof(byte))
            return byte.Parse(numberText);
        if (targetType == typeof(uint))
            return uint.Parse(numberText);
        if (targetType == typeof(ulong))
            return ulong.Parse(numberText);
        if (targetType == typeof(ushort))
            return ushort.Parse(numberText);
        if (targetType == typeof(sbyte))
            return sbyte.Parse(numberText);

        return Convert.ChangeType(numberText, targetType);
    }

    private static void WriteString(BinaryWriter writer, string value)
    {
        if (value == null)
        {
            WriteHeader(writer, TYPE_NULL, 0);
            return;
        }

        var bytes = Encoding.UTF8.GetBytes(value);
        WriteHeader(writer, TYPE_TEXT, bytes.Length);
        writer.Write(bytes);
    }

    private static object? ReadString(BinaryReader reader, int payloadSize, Type targetType)
    {
        var stringBytes = reader.ReadBytes(payloadSize);
        var stringValue = Encoding.UTF8.GetString(stringBytes);

        if (targetType == typeof(string))
            return stringValue;

        if (targetType == typeof(DocumentId))
            return DocumentId.Parse(stringValue);

        if (targetType == typeof(DateTime))
            return DateTime.Parse(stringValue);

        if (targetType == typeof(Guid))
            return Guid.Parse(stringValue);

        return stringValue;
    }

    private static void WriteArray(BinaryWriter writer, IEnumerable enumerable)
    {
        using var payloadStream = new MemoryStream();
        using var payloadWriter = new BinaryWriter(payloadStream);

        Type? elementType = null;
        var enumerableType = enumerable.GetType();
        if (enumerableType.IsGenericType)
        {
            var genericArgs = enumerableType.GetGenericArguments();
            if (genericArgs.Length > 0)
                elementType = genericArgs[0];
        }

        if (elementType == null)
            elementType = typeof(object);

        foreach (var item in enumerable)
        {
            WriteValue(payloadWriter, item, item?.GetType() ?? elementType);
        }

        var payload = payloadStream.ToArray();
        WriteHeader(writer, TYPE_ARRAY, payload.Length);
        writer.Write(payload);
    }

    private static object? ReadArray(BinaryReader reader, int payloadSize, Type targetType)
    {
        long endPosition = reader.BaseStream.Position + payloadSize;

        // Determine element type
        Type elementType = typeof(object);
        if (targetType.IsArray)
        {
            elementType = targetType.GetElementType()!;
        }
        else if (targetType.IsGenericType)
        {
            var genericArgs = targetType.GetGenericArguments();
            if (genericArgs.Length > 0)
                elementType = genericArgs[0];
        }

        var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType))!;

        while (reader.BaseStream.Position < endPosition)
        {
            var element = ReadValue(reader, elementType);
            list.Add(element);
        }

        // Convert to target type
        if (targetType.IsArray)
        {
            var array = Array.CreateInstance(elementType, list.Count);
            list.CopyTo(array, 0);
            return array;
        }

        return list;
    }

    private static void WriteDictionary(BinaryWriter writer, IDictionary dictionary)
    {
        using var payloadStream = new MemoryStream();
        using var payloadWriter = new BinaryWriter(payloadStream);

        foreach (DictionaryEntry entry in dictionary)
        {
            // Write key as string
            var keyStr = entry.Key?.ToString() ?? "";
            var keyBytes = Encoding.UTF8.GetBytes(keyStr);
            WriteHeader(payloadWriter, TYPE_TEXT, keyBytes.Length);
            payloadWriter.Write(keyBytes);

            // Write value
            WriteValue(payloadWriter, entry.Value, entry.Value?.GetType() ?? typeof(object));
        }

        var payload = payloadStream.ToArray();
        WriteHeader(writer, TYPE_OBJECT, payload.Length);
        writer.Write(payload);
    }

    private static void WriteObject(BinaryWriter writer, object obj)
    {
        using var payloadStream = new MemoryStream();
        using var payloadWriter = new BinaryWriter(payloadStream);

        var type = obj.GetType();
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                            .Where(p => p.CanRead)
                            .ToList();

        foreach (var property in properties)
        {
            var value = property.GetValue(obj);

            // Skip null values for cleaner JSON
            // if (value == null)
            //     continue;

            // Write property name (camelCase)
            var propertyName = ToCamelCase(property.Name);
            var keyBytes = Encoding.UTF8.GetBytes(propertyName);
            WriteHeader(payloadWriter, TYPE_TEXT, keyBytes.Length);
            payloadWriter.Write(keyBytes);

            // Write property value
            WriteValue(payloadWriter, value, property.PropertyType);
        }

        var payload = payloadStream.ToArray();
        WriteHeader(writer, TYPE_OBJECT, payload.Length);
        writer.Write(payload);
    }

    private static object? ReadObject(BinaryReader reader, int payloadSize, Type targetType)
    {
        long endPosition = reader.BaseStream.Position + payloadSize;

        var instance = Activator.CreateInstance(targetType);
        var properties = targetType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                   .Where(p => p.CanWrite)
                                   .ToDictionary(p => ToCamelCase(p.Name), p => p);

        while (reader.BaseStream.Position < endPosition)
        {
            // Read property name
            var keyObj = ReadValue(reader, typeof(string));
            var propertyName = keyObj as string ?? "";

            // Read property value
            if (properties.TryGetValue(propertyName, out var property))
            {
                var value = ReadValue(reader, property.PropertyType);
                property.SetValue(instance, value);
            }
            else
            {
                // Skip unknown property
                ReadValue(reader, typeof(object));
            }
        }

        return instance;
    }

    private static string ToCamelCase(string str)
    {
        if (string.IsNullOrEmpty(str) || char.IsLower(str[0]))
            return str;

        return char.ToLowerInvariant(str[0]) + str.Substring(1);
    }
}
