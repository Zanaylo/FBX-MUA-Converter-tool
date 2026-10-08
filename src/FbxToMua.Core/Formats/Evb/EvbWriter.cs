using FbxToMua.Core.Binary;

namespace FbxToMua.Core.Formats.Evb;

public static class EvbWriter
{
    private const uint Version = 0x1001;
    private const int Blocks = 0x30;
    private const int Stride = 0x20;
    private const int Operands = 7;
    private const int CountsAt = 0x20;

    public static byte[] Build(EvbScript script)
    {
        int commands = Blocks + (script.Sheets.Count + script.Names.Count) * Stride;
        int last = commands + script.Records.Count * Stride;

        ByteSink sink = new();
        sink.Text("EVT0");
        sink.Dword(Version);
        sink.Int(Blocks);
        sink.Int(last);
        sink.Int(commands);
        sink.Int(Stride);
        sink.PadTo(CountsAt);
        sink.Word((ushort)script.Sheets.Count);
        sink.Word((ushort)script.Names.Count);
        sink.PadTo(Blocks);

        PutNames(sink, script.Sheets);
        PutNames(sink, script.Names);

        foreach (EvbRecord record in script.Records)
            PutRecord(sink, record.Code, record.Operands);

        PutRecord(sink, EvbCode.None, []);

        return sink.ToArray();
    }

    private static void PutNames(ByteSink sink, IReadOnlyList<string> names)
    {
        foreach (string name in names)
        {
            int start = sink.Size;
            sink.Text(name.Length < Stride ? name : name[..(Stride - 1)]);
            sink.Pad(start, Stride);
        }
    }

    private static void PutRecord(ByteSink sink, uint code, int[] operands)
    {
        sink.Dword(code);

        for (int i = 0; i < Operands; ++i)
            sink.Dword(i < operands.Length ? unchecked((uint)operands[i]) : EvbCode.None);
    }
}
