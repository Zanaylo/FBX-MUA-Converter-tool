namespace FbxToMua.Cli.Commands;

public interface ICommand
{
    string Name { get; }

    string Usage { get; }

    int Run(Arguments arguments);
}
