using System.Text;
using FbxToMua.Cli.Commands;
using FbxToMua.Core.Export;

Console.OutputEncoding = Encoding.UTF8;

ICommand[] commands =
[
    new StagesCommand(),
    new ExportCommand(),
    new ExtractCommand(),
    new TargetsCommand(),
    new GameFolderCommand(),
    new InstallCommand(),
    new RestoreCommand(),
    new ImportCommand(),
];

if (args.Length == 0)
{
    Console.WriteLine("FbxToMua - French-Bread stages to BlazBlue and back");
    Console.WriteLine();

    foreach (ICommand known in commands)
        Console.WriteLine($"  FbxToMua {known.Usage}");

    return Report.BadUsage;
}

ICommand? command = commands.FirstOrDefault(one => string.Equals(one.Name, args[0], StringComparison.OrdinalIgnoreCase));

if (command is null)
    return Report.Failure($"Unknown command {args[0]}. Run without arguments to list them.");

try
{
    return command.Run(new Arguments(args[1..]));
}
catch (StageConversionException failure)
{
    return Report.Failure($"Could not convert: {failure.Message}.");
}
catch (IOException failure)
{
    return Report.Failure(failure.Message);
}
