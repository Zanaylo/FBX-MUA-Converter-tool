namespace FbxToMua.Core.Sources;

public sealed record SourceStage(string Folder, string Name, long Bytes, bool Backdrop = false);

public interface IStageSource : IDisposable
{
    GameKind Game { get; }

    IReadOnlyList<SourceStage> Stages();

    IReadOnlyList<string> Files(string stage);

    byte[]? Read(string stage, string file);

    string BgList();
}
