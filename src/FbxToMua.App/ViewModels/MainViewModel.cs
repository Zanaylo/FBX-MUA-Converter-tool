using FbxToMua.App.Services;
using FbxToMua.Core.Install;

namespace FbxToMua.App.ViewModels;

public sealed class MainViewModel
{
    public MainViewModel()
    {
        SteamLibrary steam = new();
        FolderPicker picker = new();
        StageInstaller installer = new(new InstallRecords(InstallRecords.DefaultRoot), steam);

        Export = new ExportViewModel(installer, steam, picker, Log);
        Import = new ImportViewModel(steam, picker, Log);
    }

    public StatusLog Log { get; } = new();

    public ExportViewModel Export { get; }

    public ImportViewModel Import { get; }
}
