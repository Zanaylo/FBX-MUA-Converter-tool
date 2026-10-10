using FbxToMua.Core.Export;
using FbxToMua.Core.Workflows;

namespace FbxToMua.Core.Tests.Workflows;

public sealed class ReframeRecordsUnitTest : IDisposable
{
    private const string Uni2 = @"D:\Steam\UNDER NIGHT IN-BIRTH II Sys Celes";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "FbxToMuaReframes", Guid.NewGuid().ToString("N"));

    public ReframeRecords InstanciarReframeRecords()
    {
        return new ReframeRecords(
            _root
        );
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, true);
    }

    [Fact]
    public void Of_NeverKept_IsNone()
    {
        // Arrange
        ReframeRecords records = InstanciarReframeRecords();

        // Act
        Reframe reframe = records.Of(Uni2, "bg001");

        // Assert
        Assert.Equal(Reframe.None, reframe);
    }

    [Fact]
    public void Keep_ThenReopened_GivesTheSameReframe()
    {
        // Arrange
        Reframe kept = new(12.5f, -8.0f, 30.0f, 4.0f, 1.25f, 2.0f);
        InstanciarReframeRecords().Keep(Uni2, "bg001", kept);

        // Act
        Reframe reframe = InstanciarReframeRecords().Of(Uni2, "bg001");

        // Assert
        Assert.Equal(kept, reframe);
    }

    [Fact]
    public void Of_OtherCaseAndTrailingSlash_FindsTheSameStage()
    {
        // Arrange
        ReframeRecords records = InstanciarReframeRecords();
        Reframe kept = Reframe.None with { Height = 20.0f };
        records.Keep(Uni2, "bg001", kept);

        // Act
        Reframe reframe = records.Of(Uni2.ToUpperInvariant() + @"\", "BG001");

        // Assert
        Assert.Equal(kept, reframe);
    }

    [Fact]
    public void Keep_None_ForgetsTheStage()
    {
        // Arrange
        ReframeRecords records = InstanciarReframeRecords();
        records.Keep(Uni2, "bg001", Reframe.None with { Side = 5.0f });

        // Act
        records.Keep(Uni2, "bg001", Reframe.None);

        // Assert
        Assert.Equal(Reframe.None, InstanciarReframeRecords().Of(Uni2, "bg001"));
    }

    [Fact]
    public void Of_StageFolderWithoutStage_IsKeyedOnTheFolder()
    {
        // Arrange
        ReframeRecords records = InstanciarReframeRecords();
        Reframe kept = Reframe.None with { Turn = -10.0f };
        records.Keep(@"D:\Stages\bg900", null, kept);

        // Act
        Reframe reframe = records.Of(@"D:\Stages\bg900", null);

        // Assert
        Assert.Equal(kept, reframe);
    }
}
