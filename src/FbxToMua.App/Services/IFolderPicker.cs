using Microsoft.Win32;

namespace FbxToMua.App.Services;

public interface IFolderPicker
{
    string? Pick(string title, string? start = null);
}

public sealed class FolderPicker : IFolderPicker
{
    public string? Pick(string title, string? start = null)
    {
        OpenFolderDialog dialog = new() { Title = title, InitialDirectory = start ?? string.Empty };

        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }
}
