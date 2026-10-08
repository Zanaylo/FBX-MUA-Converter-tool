using System.Buffers.Binary;
using FbxToMua.Core.Geometry;

namespace FbxToMua.Core.Binary;

public sealed class ByteSink
{
    private readonly List<byte> _bytes = [];

    public int Size => _bytes.Count;

    public void Byte(byte value) => _bytes.Add(value);

    public void Word(ushort value)
    {
        Span<byte> bytes = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
        _bytes.AddRange(bytes);
    }

    public void Dword(uint value)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        _bytes.AddRange(bytes);
    }

    public void Int(int value) => Dword(unchecked((uint)value));

    public void Float(float value) => Dword(BitConverter.SingleToUInt32Bits(value));

    public void Floats(ReadOnlySpan<float> values)
    {
        foreach (float value in values)
            Float(value);
    }

    public void Floats(Float3 values) => Floats((ReadOnlySpan<float>)values);

    public void Floats(Float4 values) => Floats((ReadOnlySpan<float>)values);

    public void Floats(Matrix values) => Floats((ReadOnlySpan<float>)values);

    public void Bytes(ReadOnlySpan<byte> values) => _bytes.AddRange(values);

    public void Text(string text) => Bytes(LittleEndian.Latin1(text));

    public void Zeros(int count)
    {
        for (int i = 0; i < count; ++i)
            _bytes.Add(0);
    }

    public void PadTo(int size)
    {
        while (_bytes.Count < size)
            _bytes.Add(0);
    }

    public void Pad(int start, int stride) => PadTo(start + stride);

    public void Record(int stride, params uint[] values)
    {
        int start = Size;

        foreach (uint value in values)
            Dword(value);

        Pad(start, stride);
    }

    public byte[] ToArray() => [.. _bytes];
}
