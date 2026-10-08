using FbxToMua.Core.Import;
using FbxToMua.Core.Workflows;

namespace FbxToMua.Cli.Commands;

public sealed class ImportCommand : ICommand
{
    public string Name => "import";

    public string Usage => "import <BBTAG|BBCF|P4U2 folder> <bg_stage> (--out <folder> | --uni2 <UNI2 folder>) [--name text]";

    public int Run(Arguments arguments)
    {
        string? folder = arguments.At(0);
        string? stage = arguments.At(1);
        string? output = arguments.Option("out");
        string? uni2 = arguments.Option("uni2");

        if (folder is null || stage is null || (output is null && uni2 is null))
            return Report.Usage(this);

        ImStage imported = StageWorkflows.Import(folder, stage, arguments.Option("name"));

        if (output is not null)
        {
            ImStageFolder.Write(imported, output);
            return Report.Success($"{imported.Name}: {imported.Result.Images.Count} texture(s), {imported.Result.Layer.Count} layer file(s). Written to {output}.");
        }

        ImLibrary library = new(uni2!);

        if (!library.Installed)
            return Report.Failure($"{uni2} has no UNI2-IM folder. The UNI2 Improvement Mod is what plays these stages.");

        string? target = library.Install(imported);

        return target is null
            ? Report.Failure("Every stage folder number is taken.")
            : Report.Success($"{imported.Name} installed in {target}. Start UNI2 and it shows up in the stage list.");
    }
}
