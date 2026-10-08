using System.Collections.ObjectModel;
using System.Windows.Input;
using FbxToMua.App.Services;
using FbxToMua.Core.Import;
using FbxToMua.Core.Install;
using FbxToMua.Core.Sources;
using FbxToMua.Core.Workflows;

namespace FbxToMua.App.ViewModels;

public sealed class ImportViewModel : ObservableObject
{
    private readonly IFolderPicker _picker;
    private readonly StatusLog _log;
    private GameChoice? _source;
    private StageChoice? _stage;
    private string _uni2Folder = string.Empty;
    private string _name = string.Empty;

    public ImportViewModel(ISteamLibrary steam, IFolderPicker picker, StatusLog log)
    {
        _picker = picker;
        _log = log;

        foreach (GameChoice game in GameChoice.Detected(KnownInstalls.ArcSystem, steam))
            Sources.Add(game);

        KnownGame uni2 = KnownInstalls.FrenchBread.First(known => known.Game == GameKind.Uni2);
        _uni2Folder = KnownInstalls.Find(steam, uni2) ?? string.Empty;

        BrowseSource = new AsyncCommand(BrowseSourceAsync);
        BrowseUni2 = new AsyncCommand(BrowseUni2Async);
        InstallIntoMod = new AsyncCommand(InstallIntoModAsync, () => Stage is not null && new ImLibrary(Uni2Folder).Installed);
        ExportFolderOnly = new AsyncCommand(ExportFolderAsync, () => Stage is not null);
        Source = Sources.FirstOrDefault();
    }

    public ObservableCollection<GameChoice> Sources { get; } = [];
    public ObservableCollection<StageChoice> Stages { get; } = [];

    public ICommand BrowseSource { get; }
    public ICommand BrowseUni2 { get; }
    public ICommand InstallIntoMod { get; }
    public ICommand ExportFolderOnly { get; }

    public GameChoice? Source
    {
        get => _source;
        set
        {
            if (SetProperty(ref _source, value))
                _ = LoadStagesAsync();
        }
    }

    public StageChoice? Stage
    {
        get => _stage;
        set
        {
            if (SetProperty(ref _stage, value) && value is not null && Source is not null)
                StageName = value.Name + ImStageFolder.Tag(GameFolder.Detect(Source.Folder));
        }
    }

    public string StageName
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string Uni2Folder
    {
        get => _uni2Folder;
        set => SetProperty(ref _uni2Folder, value);
    }

    private async Task LoadStagesAsync()
    {
        Stages.Clear();
        Stage = null;

        if (Source is null)
            return;

        string folder = Source.Folder;
        List<StageChoice> found = await Task.Run(() => ArcSource.Open(folder)?.Stages().Select(StageChoice.Of).ToList() ?? []);

        foreach (StageChoice stage in found)
            Stages.Add(stage);

        _log.Write(found.Count == 0 ? $"No stages found in {folder}." : $"{found.Count} stage(s) in {Source.Title}.");
    }

    private Task BrowseSourceAsync()
    {
        string? folder = _picker.Pick("Pick a BBTAG, BBCF or P4U2 game folder");

        if (folder is null)
            return Task.CompletedTask;

        GameChoice choice = new($"{GameFolder.Title(GameFolder.Detect(folder))}: {folder}", folder);
        Sources.Add(choice);
        Source = choice;

        return Task.CompletedTask;
    }

    private Task BrowseUni2Async()
    {
        string? folder = _picker.Pick("Pick the UNDER NIGHT IN-BIRTH II folder (the one with UNI2-IM)");

        if (folder is not null)
            Uni2Folder = folder;

        return Task.CompletedTask;
    }

    private Task<ImStage> Imported()
    {
        string folder = Source!.Folder;
        string stage = Stage!.Folder;
        string name = StageName;

        return Task.Run(() => StageWorkflows.Import(folder, stage, name));
    }

    private async Task InstallIntoModAsync()
    {
        await _log.Guarded(async () =>
        {
            _log.Write($"Converting {StageName}...");
            ImStage stage = await Imported();
            string? target = await Task.Run(() => new ImLibrary(Uni2Folder).Install(stage));
            _log.Write(target is null ? "Every stage folder number is taken." : $"{stage.Name} installed in {target}. Start UNI2 and pick it in the stage list.");
        });
    }

    private async Task ExportFolderAsync()
    {
        string? output = _picker.Pick("Where should the stage folder go?");

        if (output is null)
            return;

        await _log.Guarded(async () =>
        {
            _log.Write($"Converting {StageName}...");
            ImStage stage = await Imported();
            string folder = Path.Combine(output, stage.Source);
            await Task.Run(() => ImStageFolder.Write(stage, folder));
            _log.Write($"{stage.Name} written to {folder}. Use the mod's Import a stage folder, or drop it into UNI2-IM\\Mods\\bg.");
        });
    }
}
