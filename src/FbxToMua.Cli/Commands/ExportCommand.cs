using FbxToMua.Core.Export;
using FbxToMua.Core.Workflows;

namespace FbxToMua.Cli.Commands;

public sealed class ExportCommand : ICommand
{
    public string Name => "export";

    public string Usage => "export <game folder | stage folder> [stage] --out <folder> [--name text]";

    public int Run(Arguments arguments)
    {
        string? folder = arguments.At(0);
        string? output = arguments.Option("out");

        if (folder is null || output is null)
            return Report.Usage(this);

        ExportedStage exported = StageWorkflows.Export(folder, arguments.At(1), arguments.Option("name"));
        ExportFolder.Write(exported.Result, output);

        return Report.Success(ExportSummary.Of(exported.Result) + $" Written to {output}.");
    }
}
