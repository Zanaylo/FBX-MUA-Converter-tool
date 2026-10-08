using System.Collections.ObjectModel;
using System.Windows;
using FbxToMua.Core.Export;

namespace FbxToMua.App.Services;

public sealed class StatusLog
{
    private const int MostLines = 200;

    public ObservableCollection<string> Lines { get; } = [];

    public void Write(string line)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            Lines.Insert(0, $"{DateTime.Now:HH:mm:ss}  {line}");

            while (Lines.Count > MostLines)
                Lines.RemoveAt(Lines.Count - 1);
        });
    }

    public async Task Guarded(Func<Task> work)
    {
        try
        {
            await work();
        }
        catch (StageConversionException failure)
        {
            Write($"Could not convert: {failure.Message}.");
        }
        catch (IOException failure)
        {
            Write(failure.Message);
        }
        catch (UnauthorizedAccessException failure)
        {
            Write(failure.Message);
        }
    }
}
