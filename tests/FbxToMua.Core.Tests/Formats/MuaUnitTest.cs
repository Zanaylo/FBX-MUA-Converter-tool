using FbxToMua.Core.Binary;
using FbxToMua.Core.Formats.Mua;
using FbxToMua.Core.Geometry;

namespace FbxToMua.Core.Tests.Formats;

public class MuaUnitTest
{
    private static readonly int[] Strides = [0x20, 0x130, 0xc0, 0x20, 0x50, 0x20, 0x10, 0x1c, 0x10, 0x20, 0x20,
        0x10, 0x20, 0x50, 2, 0x10, 1];

    public static MuaVertex Corner(float x, float y, float z, float u, float v, int bone)
    {
        MuaVertex vertex = MuaVertex.Unrigged();
        vertex.Position = new Float3(x, y, z);
        vertex.Normal = new Float3(0.0f, 0.0f, -1.0f);
        vertex.Tangent = new Float3(1.0f, 0.0f, 0.0f);
        vertex.U = u;
        vertex.V = v;
        vertex.Colour = new MuaColour(200, 100, 50, 255);

        return bone < 0 ? vertex : vertex.RiggedTo(bone);
    }

    public static MuaModel SmallModel()
    {
        MuaModel model = new();
        model.Textures.AddRange(["floor.dds", "lamp.dds"]);
        model.Materials.AddRange([0, 1]);
        model.Scripts.AddRange(["base.evb", "mesh_001.evb"]);

        model.Bones.Add(MuaBone.Root("Bone_setting"));
        model.Skeletons.Add(new MuaSkeleton(0, 1, 0, MuaBlend.Unset, MuaFlags.Scene));

        MuaBone floor = MuaBone.Root("mesh_000");
        floor.Kind = MuaBone.MeshKind;
        model.Bones.Add(floor);
        model.Skeletons.Add(new MuaSkeleton(1, 1, MuaSkeleton.NoScript, MuaBlend.Unset, MuaFlags.Static));

        model.Bones.Add(MuaBone.Root("mesh_001"));
        Pose moved = Pose.Rest;
        moved.Translation = new Float3(5.0f, 0.0f, 0.0f);
        model.Bones.Add(MuaBone.Joint("mesh_001_joint", PoseMath.Turned(moved)));
        MuaBone.Link(model.Bones, 2, 2);
        model.Skeletons.Add(new MuaSkeleton(2, 2, 1, MuaBlend.Add, MuaFlags.Animated));

        model.Vertices.AddRange([
            Corner(-10, 0, -10, 0, 0, -1), Corner(10, 0, -10, 1, 0, -1),
            Corner(10, 0, 10, 1, 1, -1), Corner(-10, 0, 10, 0, 1, -1),
            Corner(0, 5, 0, 0, 0, 1), Corner(1, 5, 0, 1, 0, 1), Corner(1, 6, 0, 1, 1, 1),
        ]);

        List<ushort> strip = TriangleStrip.Build([0, 1, 2, 0, 2, 3]);
        model.Parts.Add(new MuaPart(0, 0, strip.Count));
        model.Indices.AddRange(strip);

        strip = TriangleStrip.Build([0, 1, 2]);
        model.Parts.Add(new MuaPart(1, model.Indices.Count, strip.Count));
        model.Indices.AddRange(strip);

        model.Meshes.Add(new MuaMesh { Name = "mesh_000", Skeleton = 1, Partner = 1, FirstPart = 0, Parts = 1, FirstVertex = 0, Vertices = 4, Pivot = new Float3(0, 0, 2) });
        model.Meshes.Add(new MuaMesh { Name = "mesh_001", Skeleton = 2, Partner = 2, FirstPart = 1, Parts = 1, FirstVertex = 4, Vertices = 3, Pivot = new Float3(0, 0, 1) });

        return model;
    }

    [Fact]
    public void Build_SmallModel_HasMagicVersionAndSeventeenSections()
    {
        // Arrange
        MuaModel model = SmallModel();

        // Act
        byte[] blob = MuaWriter.Build(model, true);

        // Assert
        Assert.True(LittleEndian.Starts(blob, "MUA\0"));
        Assert.Equal(0x3eeu, LittleEndian.U32(blob, 4));
        Assert.Equal(17u, LittleEndian.U32(blob, 8));
    }

    [Fact]
    public void Build_SmallModel_ChainsTheSectionsTightlyToTheEnd()
    {
        // Arrange
        MuaModel model = SmallModel();
        long expected = 0xa8;
        bool chained = true;

        // Act
        byte[] blob = MuaWriter.Build(model, true);

        for (int i = 0; i < 17; ++i)
        {
            uint offset = LittleEndian.U32(blob, 0x20 + i * 8);
            uint count = LittleEndian.U32(blob, 0x20 + i * 8 + 4);
            chained = chained && offset == expected;
            expected = offset + count * Strides[i];
        }

        // Assert
        Assert.True(chained);
        Assert.Equal(blob.Length, expected);
    }

    [Fact]
    public void Build_MeshWithTwoParts_WritesOneOrderEntryPerPart()
    {
        // Arrange
        MuaModel model = SmallModel();
        model.Parts.Add(model.Parts[^1]);
        model.Meshes[^1].Parts = 2;

        // Act
        byte[] blob = MuaWriter.Build(model, false);
        uint at = LittleEndian.U32(blob, 0x20 + 12 * 8);
        uint count = LittleEndian.U32(blob, 0x20 + 12 * 8 + 4);
        HashSet<(int, int)> order = [];

        for (uint i = 0; i < count; ++i)
            order.Add((LittleEndian.I32(blob, at + i * 0x20), LittleEndian.I32(blob, at + i * 0x20 + 4)));

        // Assert
        Assert.Equal((uint)model.Parts.Count, count);
        Assert.Equal(new HashSet<(int, int)> { (0, 0), (1, 0), (1, 1) }, order);
    }

    [Fact]
    public void Read_WrittenModel_GivesMeshesTexturesAndScriptsBack()
    {
        // Arrange
        byte[] blob = MuaWriter.Build(SmallModel(), true);

        // Act
        MuaReader read = MuaReader.Read(blob)!;

        // Assert
        Assert.True(read.HasGeometry);
        Assert.Equal(2, read.Meshes.Count);
        Assert.Equal("lamp.dds", read.Textures[1]);
        Assert.Equal("mesh_001.evb", read.Scripts[1]);
        Assert.Equal("mesh_001", read.Meshes[1].Name);
        Assert.Equal(2, read.Meshes[1].Skeleton);
        Assert.Equal(2, read.Meshes[1].Partner);
        Assert.Equal(2, read.Meshes[1].Bone);
    }

    [Fact]
    public void Read_WrittenModel_GivesSkeletonsBonesAndMaterialsBack()
    {
        // Arrange
        byte[] blob = MuaWriter.Build(SmallModel(), true);

        // Act
        MuaReader read = MuaReader.Read(blob)!;

        // Assert
        Assert.Equal((int)MuaBlend.Add, read.Skeletons[2].Blend);
        Assert.Equal(1, read.Skeletons[2].Script);
        Assert.Equal(MuaFlags.Static, read.Skeletons[1].Flags);
        Assert.Equal(0, read.Bones[3].Parent);
        Assert.Equal(5.0f, read.Bones[3].Matrix[12]);
        Assert.Equal([1], read.Materials[1]);
        Assert.True(Tolerance.Near(read.Meshes[0].Pivot[2], 2.0f, 1e-6f));
    }

    [Fact]
    public void Read_WrittenModel_GivesTheVertexBackAsRgba()
    {
        // Arrange
        byte[] blob = MuaWriter.Build(SmallModel(), true);
        MuaReader read = MuaReader.Read(blob)!;

        // Act
        MuaReadVertex vertex = read.VertexAt(5);

        // Assert
        Assert.Equal(1.0f, vertex.Position[0]);
        Assert.Equal(5.0f, vertex.Position[1]);
        Assert.Equal(new MuaColour(200, 100, 50, 255), vertex.Colour);
        Assert.Equal(1, vertex.Bone);
        Assert.Equal(50, blob[LittleEndian.U32(blob, 0x20 + 13 * 8) + 5 * 0x50 + 0x34]);
    }

    [Fact]
    public void Triangles_FirstPart_UnrollsWithItsWinding()
    {
        // Arrange
        MuaReader read = MuaReader.Read(MuaWriter.Build(SmallModel(), true))!;

        // Act
        List<MuaTriangle> triangles = read.Triangles(read.Parts[0]);

        // Assert
        Assert.Equal(2, triangles.Count);
        Assert.Equal(new MuaTriangle(0, 1, 2), triangles[0]);
    }

    [Fact]
    public void Build_SmallModel_WritesTheBounds()
    {
        // Arrange
        MuaModel model = SmallModel();

        // Act
        byte[] blob = MuaWriter.Build(model, true);
        uint mesh = LittleEndian.U32(blob, 0x20 + 2 * 8);

        // Assert
        Assert.True(Tolerance.Near(LittleEndian.F32(blob, mesh + 0x18), 0.0f, 1e-6f));
        Assert.True(Tolerance.Near(LittleEndian.F32(blob, mesh + 0x20), 0.0f, 1e-6f));
        Assert.True(Tolerance.Near(LittleEndian.F32(blob, mesh + 0x24), MathF.Sqrt(200.0f), 1e-5f));
    }

    [Fact]
    public void Build_WithoutGeometry_LeavesSectionsThirteenAndFourteenEmpty()
    {
        // Arrange
        byte[] bare = MuaWriter.Build(SmallModel(), false);

        // Act
        MuaReader? read = MuaReader.Read(bare);

        // Assert
        Assert.NotNull(read);
        Assert.False(read.HasGeometry);
        Assert.Equal(0u, LittleEndian.U32(bare, 0x20 + 13 * 8 + 4));
        Assert.Equal(0u, LittleEndian.U32(bare, 0x20 + 14 * 8 + 4));
    }
}
