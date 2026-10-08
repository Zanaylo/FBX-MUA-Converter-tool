namespace FbxToMua.Cli.Commands;

public static class Report
{
    public const int Ok = 0;
    public const int Failed = 1;
    public const int BadUsage = 2;

    public static int Usage(ICommand command)
    {
        Console.Error.WriteLine($"usage: FbxToMua {command.Usage}");
        return BadUsage;
    }

    public static int Failure(string message)
    {
        Console.Error.WriteLine(message);
        return Failed;
    }

    public static int Success(string message)
    {
        Console.WriteLine(message);
        return Ok;
    }
}
