using FbxToMua.Core.Binary;
using FbxToMua.Core.Formats.Evb;

namespace FbxToMua.Core.Tests.Formats;

public class EvbUnitTest
{
    private static EvbScript Picking()
    {
        return new EvbScript([], ["mesh_001_000.mmot"], [
            new EvbRecord(EvbCode.Begin), new EvbRecord(EvbCode.Wait, 0), new EvbRecord(EvbCode.Pick, 0),
            new EvbRecord(EvbCode.Close), new EvbRecord(EvbCode.Yield),
        ]);
    }

    private static byte[] PickingScript() => EvbWriter.Build(Picking());

    [Fact]
    public void Build_Script_HasMagicStrideAndTerminator()
    {
        // Arrange
        EvbScript script = Picking();

        // Act
        byte[] blob = EvbWriter.Build(script);

        // Assert
        Assert.True(LittleEndian.Starts(blob, "EVT0"));
        Assert.Equal(0x20u, LittleEndian.U32(blob, 0x14));
        Assert.Equal((uint)blob.Length, LittleEndian.U32(blob, 0x0c) + 0x20);
    }

    [Fact]
    public void Play_PickingScript_ReadsTheNameBlock()
    {
        // Arrange
        byte[] script = PickingScript();

        // Act
        EvbPlayed? played = EvbPlayer.Play(script, "mesh_001");

        // Assert
        Assert.NotNull(played);
        Assert.Equal(["mesh_001_000.mmot"], played.Named);
    }

    [Fact]
    public void Motions_PickingScript_PicksTheTakeAtFrameZero()
    {
        // Arrange
        EvbPlayed played = EvbPlayer.Play(PickingScript(), "mesh_001")!;

        // Act
        EvbRun? run = EvbPlayer.Motions(played);

        // Assert
        Assert.NotNull(run);
        Assert.Equal("mesh_001_000.mmot", run.Frame[0].Take);
    }

    [Fact]
    public void Tilt_SceneScript_ReadsTheDegreesBack()
    {
        // Arrange
        EvbScript scene = new([], [], [
            new EvbRecord(EvbCode.SceneOpen), new EvbRecord(EvbCode.SceneTilt, 7, 0, 0), new EvbRecord(EvbCode.SceneClose),
        ]);

        // Act
        float? tilt = EvbPlayer.Tilt(EvbWriter.Build(scene));

        // Assert
        Assert.Equal(7.0f, tilt);
    }
}
