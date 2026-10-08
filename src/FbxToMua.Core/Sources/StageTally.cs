namespace FbxToMua.Core.Sources;

internal static class StageTally
{
    public const string BgListFile = "BgList.txt";

    public static List<SourceStage> Compose(IEnumerable<(string Stage, string File, uint Size)> files, string bgList)
    {
        SortedDictionary<string, (long Bytes, bool Model)> tallies = new(StringComparer.Ordinal);

        foreach ((string stage, string file, uint size) in files)
        {
            (long bytes, bool model) = tallies.GetValueOrDefault(stage);
            tallies[stage] = (bytes + size, model || string.Equals(file, StageFolder.ModelFile, StringComparison.OrdinalIgnoreCase));
        }

        List<SourceStage> stages = [];

        foreach ((string folder, (long bytes, bool model)) in tallies)
        {
            if (!model)
                continue;

            string? block = BgListText.Block(bgList, folder);
            string? name = block is null ? null : BgListText.Field(block, "Name");
            stages.Add(new SourceStage(folder, name is null ? string.Empty : BgListText.Unquoted(name), bytes));
        }

        return stages;
    }
}
