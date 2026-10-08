using FbxToMua.Core.Sources;

namespace FbxToMua.Cli.Commands;

public sealed class ExtractCommand : ICommand
{
    public string Name => "extract";

    public string Usage => "extract <game folder> <stage> --out <folder>";

    public int Run(Arguments arguments)
    {
        string? folder = arguments.At(0);
        string? stage = arguments.At(1);
        string? output = arguments.Option("out");

        if (folder is null || stage is null || output is null)
            return Report.Usage(this);

        using IStageSource? source = StageSources.Open(folder);

        if (source is null)
            return Report.Failure($"No French-Bread stages found in {folder}.");

        Directory.CreateDirectory(output);
        int written = 0;

        foreach (string file in source.Files(stage))
        {
            byte[]? data = source.Read(stage, file);

            if (data is null)
                continue;

            File.WriteAllBytes(FbxToMua.Core.Binary.DiskName.Path(output, file), data);
            ++written;
        }

        return written == 0 ? Report.Failure($"{stage} has no files.") : Report.Success($"Extracted {written} file(s) of {stage} to {output}.");
    }
}
