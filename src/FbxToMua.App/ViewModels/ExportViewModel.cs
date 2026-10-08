using System.Collections.ObjectModel;
using System.Windows.Input;
using FbxToMua.App.Services;
using FbxToMua.Core.Export;
using FbxToMua.Core.Install;
using FbxToMua.Core.Sources;
using FbxToMua.Core.Workflows;

namespace FbxToMua.App.ViewModels;

public sealed class ExportViewModel : ObservableObject
{
    private readonly StageInstaller _installer;
    private readonly IFolderPicker _picker;
    private readonly StatusLog _log;
    private GameChoice? _source;
    private StageChoice? _stage;
    private ArcGame _target = ArcGame.Bbcf;
    private string _targetFolder = string.Empty;
    private TargetChoice? _replace;
    private string _name = string.Empty;

    public ExportViewModel(StageInstaller installer, ISteamLibrary steam, IFolderPicker picker, StatusLog log)
    {
        _installer = installer;
        _picker = picker;
        _log = log;

        foreach (GameChoice game in GameChoice.Detected(KnownInstalls.FrenchBread, steam))
            Sources.Add(game);

        BrowseSource = new AsyncCommand(BrowseSourceAsync);
        BrowseTarget = new AsyncCommand(BrowseTargetAsync);
        ExportFiles = new AsyncCommand(ExportFilesAsync, () => Ready);
        Install = new AsyncCommand(InstallAsync, () => Ready && Replace is not null);
        Restore = new AsyncCommand(RestoreAsync, () => Replace is not null);

        RefreshTargets();
        Source = Sources.FirstOrDefault();
    }

    public ObservableCollection<GameChoice> Sources { get; } = [];
    public ObservableCollection<StageChoice> Stages { get; } = [];
    public ObservableCollection<TargetChoice> Targets { get; } = [];
    public IReadOnlyList<ArcGameChoice> Games { get; } = ArcGameChoice.All;

    public ICommand BrowseSource { get; }
    public ICommand BrowseTarget { get; }
    public ICommand ExportFiles { get; }
    public ICommand Install { get; }
    public ICommand Restore { get; }

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
            if (SetProperty(ref _stage, value))
                StageName = value?.Name.Length > 0 ? value.Name : value?.Folder ?? string.Empty;
        }
    }

    public string StageName
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public ArcGame Target
    {
        get => _target;
        set
        {
            if (SetProperty(ref _target, value))
                RefreshTargets();
        }
    }

    public string TargetFolder
    {
        get => _targetFolder;
        private set => SetProperty(ref _targetFolder, value);
    }

    public TargetChoice? Replace
    {
        get => _replace;
        set => SetProperty(ref _replace, value);
    }

    private bool Ready => Source is not null && (Stage is not null || StageFolder.Holds(Source.Folder));

    private async Task LoadStagesAsync()
    {
        Stages.Clear();
        Stage = null;

        if (Source is null)
            return;

        string folder = Source.Folder;

        if (StageFolder.Holds(folder))
        {
            StageName = Path.GetFileName(folder);
            _log.Write($"{folder} is a stage folder; it exports as it is.");
            return;
        }

        List<StageChoice> found = await Task.Run(() =>
        {
            using IStageSource? source = StageSources.Open(folder);

            return source?.Stages().Select(StageChoice.Of).ToList() ?? [];
        });

        foreach (StageChoice stage in found)
            Stages.Add(stage);

        _log.Write(found.Count == 0 ? $"No stages found in {folder}." : $"{found.Count} stage(s) in {Source.Title}.");
    }

    private void RefreshTargets()
    {
        Targets.Clear();
        TargetFolder = _installer.GameFolder(Target) ?? "not found - choose the game folder";

        foreach (StageTarget target in _installer.Targets(Target))
            Targets.Add(new TargetChoice(target, _installer.Installed(Target, target)));
    }

    private Task BrowseSourceAsync()
    {
        string? folder = _picker.Pick("Pick a French-Bread game folder, or one stage folder with bg.fbx.bin");

        if (folder is null)
            return Task.CompletedTask;

        GameChoice choice = new(StageFolder.Holds(folder) ? $"Stage folder: {Path.GetFileName(folder)}" : $"{GameFolder.Title(GameFolder.Detect(folder))}: {folder}", folder);
        Sources.Add(choice);
        Source = choice;

        return Task.CompletedTask;
    }

    private Task BrowseTargetAsync()
    {
        string? folder = _picker.Pick($"Pick the {Target} game folder");

        if (folder is null)
            return Task.CompletedTask;

        if (!_installer.ChooseGameFolder(Target, folder))
            _log.Write($"{folder} is not a {Target} install.");

        RefreshTargets();

        return Task.CompletedTask;
    }

    private Task<ExportedStage> Exported()
    {
        string folder = Source!.Folder;
        string? stage = Stage?.Folder;
        string name = StageName;

        return Task.Run(() => StageWorkflows.Export(folder, stage, name));
    }

    private async Task ExportFilesAsync()
    {
        string? output = _picker.Pick("Where should the exported files go?");

        if (output is null)
            return;

        await _log.Guarded(async () =>
        {
            _log.Write($"Exporting {StageName}...");
            ExportedStage exported = await Exported();
            string folder = Path.Combine(output, exported.Result.Stage);
            await Task.Run(() => ExportFolder.Write(exported.Result, folder));
            _log.Write(ExportSummary.Of(exported.Result) + $" Written to {folder}.");
        });
    }

    private async Task InstallAsync()
    {
        TargetChoice replace = Replace!;

        await _log.Guarded(async () =>
        {
            _log.Write($"Exporting {StageName} over {replace.Target.Stem}...");
            ExportedStage exported = await Exported();
            InstallReport report = await Task.Run(() => _installer.Install(Target, replace.Target, exported.Result, exported.Name));
            _log.Write(ExportSummary.Of(exported.Result) + " " + report.Message);
            RefreshTargets();
        });
    }

    private async Task RestoreAsync()
    {
        TargetChoice replace = Replace!;
        InstallReport report = await Task.Run(() => _installer.Restore(Target, replace.Target));
        _log.Write(report.Message);
        RefreshTargets();
    }
}
