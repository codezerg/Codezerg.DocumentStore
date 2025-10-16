using System;
using System.Security.Cryptography;

namespace Codezerg.DocumentStore;

/// <summary>
/// Compatibility helpers for netstandard2.0
/// </summary>
internal static class Compat
{
    private static readonly RandomNumberGenerator _rng = RandomNumberGenerator.Create();

    public static int GetRandomInt32(int minValue, int maxValue)
    {
        if (minValue >= maxValue)
            throw new ArgumentException("minValue must be less than maxValue");

        byte[] bytes = new byte[4];
        _rng.GetBytes(bytes);
        int value = BitConverter.ToInt32(bytes, 0) & int.MaxValue;
        return value % (maxValue - minValue) + minValue;
    }

    public static void FillRandom(byte[] data, int offset, int count)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        if (offset < 0 || offset >= data.Length) throw new ArgumentOutOfRangeException(nameof(offset));
        if (count < 0 || offset + count > data.Length) throw new ArgumentOutOfRangeException(nameof(count));

        byte[] temp = new byte[count];
        _rng.GetBytes(temp);
        Array.Copy(temp, 0, data, offset, count);
    }

    public static byte[] FromHexString(string hex)
    {
        if (hex == null) throw new ArgumentNullException(nameof(hex));
        if (hex.Length % 2 != 0) throw new ArgumentException("Hex string must have even length");

        byte[] bytes = new byte[hex.Length / 2];
        for (int i = 0; i < bytes.Length; i++)
        {
            bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
        }
        return bytes;
    }

    public static string ToHexString(byte[] bytes)
    {
        if (bytes == null) throw new ArgumentNullException(nameof(bytes));

        char[] chars = new char[bytes.Length * 2];
        for (int i = 0; i < bytes.Length; i++)
        {
            int b = bytes[i];
            chars[i * 2] = GetHexChar(b >> 4);
            chars[i * 2 + 1] = GetHexChar(b & 0x0F);
        }
        return new string(chars);
    }

    private static char GetHexChar(int value)
    {
        return (char)(value < 10 ? '0' + value : 'a' + (value - 10));
    }

    public static int GetHashCodeForBytes(byte[] bytes)
    {
        if (bytes == null) return 0;

        unchecked
        {
            int hash = 17;
            foreach (var b in bytes)
            {
                hash = hash * 31 + b;
            }
            return hash;
        }
    }
}
