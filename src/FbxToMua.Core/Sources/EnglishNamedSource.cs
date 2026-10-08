using FbxToMua.Core.Sources.Tables;

namespace FbxToMua.Core.Sources;

public sealed class EnglishNamedSource(IStageSource source) : IStageSource
{
    public GameKind Game => source.Game;

    public IReadOnlyList<SourceStage> Stages() => source.Stages().Select(English).ToList();

    public IReadOnlyList<string> Files(string stage) => source.Files(stage);

    public byte[]? Read(string stage, string file) => source.Read(stage, file);

    public string BgList() => source.BgList();

    public void Dispose() => source.Dispose();

    private SourceStage English(SourceStage stage)
    {
        string? english = EnglishStageNames.Of(source.Game, stage.Folder);

        if (english is null)
            return stage;

        return stage with { Name = TextDisplay.Stored(english) };
    }
}
