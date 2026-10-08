using FbxToMua.Core.Formats.Evb;
using FbxToMua.Core.Import;
using FbxToMua.Core.Sources;

namespace FbxToMua.Core.Tests.Import;

public class ImStageNoteUnitTest
{
    private static ArcStageResult LitResult()
    {
        ArcStageResult result = new() { Fading = true };
        result.Flow.AddRange([0.0005f, -0.25f]);

        EvbLamp lamp = new() { Loop = 13, From = 6 };
        lamp.Ramp.AddRange([new EvbRamp(0, 1000, 5), new EvbRamp(5, 950, 1)]);
        result.Lamps.Add(lamp);

        EvbFlip flip = new();
        flip.Rects.AddRange([0.0f, 0.5f, 0.0f, 1.0f]);
        flip.Frame.AddRange([0, 0, 0, -1, -1]);
        result.Flips.Add(flip);
        result.Once.AddRange([3, 40]);

        return result;
    }

    [Fact]
    public void Build_LitStage_WritesTheIdentityThenTheRuntimeLines()
    {
        // Arrange
        string note = ImStageNote.Build("Snowtown (BBTAG)", @"D:\BBTAG", "bg_snowtown", LitResult(), string.Empty);

        // Act
        string[] lines = note.Split("\r\n");

        // Assert
        Assert.Equal("// UNI2 Improvement Mod", lines[0]);
        Assert.Equal("Name = \"Snowtown (BBTAG)\"", lines[1]);
        Assert.Equal(@"From = ""D:\BBTAG""", lines[2]);
        Assert.Equal("Source = \"bg_snowtown\"", lines[3]);
        Assert.Equal("Flow = [ 0.00050000, -0.25000000 ]", lines[4]);
        Assert.Equal("Lamp0 = [ 13, 0, 1000, 5, 5, 950, 1 ]", lines[5]);
        Assert.Equal("Lamp0From = 6", lines[6]);
        Assert.Equal("Flip0Rects = [ 0.000000, 0.500000, 0.000000, 1.000000 ]", lines[7]);
        Assert.Equal("Flip0 = [ 0, 3, -1, 2 ]", lines[8]);
        Assert.Equal("Once = [ 3, 40 ]", lines[9]);
        Assert.Equal("VertexAlpha = 1", lines[10]);
    }

    [Fact]
    public void Body_IndentedBlock_DropsOnlyTheLeadingBlankLines()
    {
        // Arrange
        string block = "\r\n\t\tName = \"Town\",\r\n\t\tDataFile = \"bg_town\",\r\n\r\n\tScale = [ 1.0, 1.0, 1.0 ],\r\n\t";

        // Act
        string body = ImStageNote.Body(block);

        // Assert
        Assert.Equal("\t\tName = \"Town\",\r\n\t\tDataFile = \"bg_town\",\r\n\r\n\tScale = [ 1.0, 1.0, 1.0 ],\r\n\t", body);
    }

    [Fact]
    public void Body_NoteWithIdentityAtTheStart_DropsTheHeaderAndIdentity()
    {
        // Arrange
        string note = "// UNI2 Improvement Mod\r\nName = \"Town\"\r\nFrom = \"D:\\BBTAG\"\r\n\r\nScale = [ 1.0 ]\r\n";

        // Act
        string body = ImStageNote.Body(note);

        // Assert
        Assert.Equal("Scale = [ 1.0 ]\r\n", body);
    }

    [Fact]
    public void Block_BbtagStageWithALook_ScalesByTheLook()
    {
        // Arrange
        string unknownStage = "bg_unknown";
        string stageWithALook = "bg_ring";

        // Act
        string plain = ArcLooks.Block(unknownStage, GameKind.Bbtag);
        string looked = ArcLooks.Block(stageWithALook, GameKind.Bbtag);

        // Assert
        Assert.StartsWith("\tScale = [ 12.1739, 12.1739, 12.1739 ],\r\n", plain);
        Assert.StartsWith("\tScale = [ 14.0000, 14.0000, 14.0000 ],\r\n", looked);
    }

    [Fact]
    public void Block_P4u2_UsesItsOwnLens()
    {
        // Arrange
        string stage = "bg_unknown";
        float tilt = 3.0f;

        // Act
        string block = ArcLooks.Block(stage, GameKind.P4u2, tilt);

        // Assert
        Assert.Contains("\tFOV = 41.7,\r\n", block);
        Assert.Contains("\tViewRotationX = 3.0,\r\n", block);
    }
}
