using System.Runtime.InteropServices;
using System.Text;

namespace FbxToMua.Core.Binary;

public static class DiskName
{
    private static readonly Encoding Ansi = CreateAnsi();

    public static string Of(string bytes) => Ansi.GetString(Encoding.Latin1.GetBytes(bytes));

    public static string Path(string folder, string bytes) => System.IO.Path.Combine(folder, Of(bytes));

    public static string FromDisk(string name) => Encoding.Latin1.GetString(Ansi.GetBytes(name));

    [DllImport("kernel32.dll")]
    private static extern uint GetACP();

    private static Encoding CreateAnsi()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        if (!OperatingSystem.IsWindows())
            return Encoding.Latin1;

        return Encoding.GetEncoding((int)GetACP());
    }
}
