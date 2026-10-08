using System.Globalization;
using System.Text;
using FbxToMua.Core.Export;
using FbxToMua.Core.Formats.Objects;
using FbxToMua.Core.Formats.Pat;

namespace FbxToMua.Core.Import;

public sealed record SpawnedEffect(string Effect, List<double[]> Origins);

public sealed record BakedLayer(string PatName, byte[] Pat, byte[] Objects, int Entries, int Patterns);

public struct CardPose
{
    public bool Shown;
    public double[] Position;
    public double[] Size;
    public double Turn;
}

public sealed class ParticleCard
{
    public int Cell { get; init; }
    public double[] Tint { get; init; } = new double[3];
    public List<CardPose> Frames { get; } = [];
}

public sealed class ParticleCards
{
    public int Side { get; set; }
    public byte[] Rgba { get; set; } = [];
    public List<double[]> Cells { get; } = [];
    public List<ParticleCard> Cards { get; } = [];
}

public static class ParticleBake
{
    private const uint Seed = 1;
    private const int Lives = 8;
    private const int Step = 3;
    private const int LanesPerBand = 2;
    private const int PatLimit = 8 * 1024 * 1024;
    private const double Nearest = 1.0;
    private const double Tau = 2.0 * Math.PI;
    private const int PrioBehind = 271;
    private const int PrioFront = 402;
    private const int SpritePriority = 10;
    private const byte Additive = 1;
    private const float HiddenZoom = 0.001f;
    private const int UvSpace = 256;
    private const int Unbounded = 1 << 30;
    private const string PatSuffix = "_particles.pat";
    private const uint CountDraw = 0xc0;
    private const uint LaneDraw = 0x100;
    private const uint KickDraw = 0x140;
    private static readonly int[] AtlasSides = [256, 512];

    private sealed class Cell
    {
        public int Part { get; set; }
        public string Name { get; set; } = string.Empty;
        public int[] Source { get; } = new int[4];
        public int[] Pretint { get; } = new int[3];
        public int X { get; set; }
        public int Y { get; set; }
    }

    private sealed class Plan
    {
        public required ParticleEffect Effect { get; init; }
        public List<double[]> Origins { get; init; } = [];
        public List<int> Lanes { get; } = [];
        public int Cell { get; init; }
    }

    private sealed record Lived(int Birth, ParticleLife Life);

    private sealed class Entry
    {
        public char Letter { get; init; }
        public int Index { get; init; }
        public int Effect { get; init; }
        public int Loop { get; init; }
        public int LayerZ { get; init; }
        public int Prio { get; init; }
        public List<List<Lived>> Lanes { get; } = [];
    }

    private sealed record Frame(PatWritePattern Pattern, int Wait);

    public static BakedLayer? Bake(IReadOnlyList<ParticleEffect> effects, IReadOnlyList<SpawnedEffect> spawned, ParticleSurface surface, string stage)
    {
        List<Cell> cells = [];
        List<Plan> plans = PlansOf(effects, spawned, cells, volume: true);

        if (plans.Count == 0 || surface.Bgra.Length == 0 || !PackCells(cells, out int side))
            return null;

        List<Entry> entries = BuildEntries(plans);

        if (entries.Count == 0 || entries.Count > ObjectList.MostEntries)
            return null;

        LayerCamera camera = new();
        List<List<Frame>> frames = [];
        List<PatWritePattern> patterns = [];

        foreach (Entry entry in entries)
        {
            Plan plan = plans[entry.Effect];
            bool facesMotion = (plan.Effect.Sprite.Flags & ParticleFlags.SpriteFacesMotion) != 0;
            List<Frame> entryFrames = EntryFrames(entry, cells[plan.Cell], camera, facesMotion);
            frames.Add(entryFrames);
            patterns.AddRange(entryFrames.Select(frame => frame.Pattern));
        }

        string patName = stage + PatSuffix;
        byte[] pat = PatWriter.Build(patterns, Cutouts(cells, side), AtlasFor(cells, side, surface, stage));

        if (pat.Length > PatLimit)
            return null;

        return new BakedLayer(patName, pat, Encoding.Latin1.GetBytes(ObjectText(stage, patName, entries, frames)), entries.Count, patterns.Count);
    }

    public static ParticleCards? Emitted(IReadOnlyList<ParticleEffect> effects, IReadOnlyList<SpawnedEffect> spawned, ParticleSurface surface)
    {
        List<Cell> cells = [];
        List<Plan> plans = PlansOf(effects, spawned, cells, volume: false);
        ParticleCards cards = new();

        if (plans.Count == 0 || !Packed(cells, surface, cards))
            return null;

        LayerCamera camera = new();

        for (int e = 0; e < plans.Count; ++e)
        {
            Plan plan = plans[e];
            ParticleEffect effect = plan.Effect;
            int grid = Curved(effect) ? Step : 1;
            int loop = LoopLength(effect.Group, grid);
            bool facesMotion = (effect.Sprite.Flags & ParticleFlags.SpriteFacesMotion) != 0;

            for (int o = 0; o < plan.Origins.Count; ++o)
            {
                for (int k = 0; k < plan.Lanes[o]; ++k)
                {
                    List<Lived> lane = MakeLane(plan, e, o, k, [0.0, 0.0], plan.Lanes[o], loop, grid);
                    ParticleCard card = new() { Cell = plan.Cell, Tint = TintOf(effect, cells[plan.Cell]) };
                    List<List<double>> turned = lane.Select(lived => Turns(lived.Life, camera, facesMotion)).ToList();

                    for (int when = 0; when < loop; ++when)
                        card.Frames.Add(PoseAt(lane, turned, when, loop));

                    cards.Cards.Add(card);
                }
            }
        }

        return cards.Cards.Count > 0 ? cards : null;
    }

    public static ParticleCards? Kicked(IReadOnlyList<ParticleEffect> effects, string effectName, ParticleSurface surface, int bursts)
    {
        ParticleEffect? kicked = Particles.Find(effects, effectName);

        if (kicked is null || kicked.Sprite.SharedAtlas != 0 || bursts <= 0)
            return null;

        List<ParticleEffect> parts = BurstParts(effects, kicked);
        List<Cell> cells = [];
        List<int> cellOf = [];
        int frames = 0;

        foreach (ParticleEffect part in parts)
        {
            LifeRange(part.Group, out _, out int high);
            frames = Math.Max(frames, high);
            cellOf.Add(CellFor(part, cells));
        }

        ParticleCards cards = new();

        if (!Packed(cells, surface, cards))
            return null;

        LayerCamera camera = new();

        for (int burst = 0; burst < bursts; ++burst)
        {
            for (int p = 0; p < parts.Count; ++p)
            {
                ParticleEffect part = parts[p];
                int lanes = Math.Max(1, Math.Max(part.Group.CountMin, part.Group.CountMax));
                bool facesMotion = (part.Sprite.Flags & ParticleFlags.SpriteFacesMotion) != 0;

                for (int lane = 0; lane < lanes; ++lane)
                {
                    Dice dice = new(Seed, KickDraw, (uint)burst, (uint)p, (uint)lane);
                    LifeRange(part.Group, out int low, out int high);
                    int length = dice.Count(low, high);
                    ParticleLife life = new(part, [0.0, 0.0, 0.0], dice, length, [part.Shape.BoxMin[2], part.Shape.BoxMax[2]], lane, lanes, 1);
                    List<double> turns = Turns(life, camera, facesMotion);
                    ParticleCard card = new() { Cell = cellOf[p], Tint = TintOf(part, cells[cellOf[p]]) };

                    for (int age = 0; age <= frames; ++age)
                        card.Frames.Add(Alive(life, age) ? PoseOf(life, turns, age) : Hidden());

                    cards.Cards.Add(card);
                }
            }
        }

        return cards.Cards.Count > 0 ? cards : null;
    }

    private static List<Plan> PlansOf(IReadOnlyList<ParticleEffect> effects, IReadOnlyList<SpawnedEffect> spawned, List<Cell> cells, bool volume)
    {
        List<Plan> plans = [];

        foreach (SpawnedEffect one in spawned)
        {
            ParticleEffect? effect = Particles.Find(effects, one.Effect);

            if (effect is null || !Respawns(effect.Group) || effect.Sprite.SharedAtlas != 0 || VolumeBorn(effect) != volume)
                continue;

            Plan plan = new() { Effect = effect, Origins = [.. one.Origins], Cell = CellFor(effect, cells) };
            uint effectIndex = (uint)plans.Count;

            for (int o = 0; o < plan.Origins.Count; ++o)
            {
                Dice dice = new(Seed, effectIndex, (uint)o, CountDraw);
                plan.Lanes.Add(Math.Max(1, dice.Count(effect.Group.CountMin, effect.Group.CountMax)));
            }

            plans.Add(plan);
        }

        return plans;
    }

    private static int CeilDiv(int value, int grid) => (value + grid - 1) / grid;

    private static bool Respawns(ParticleGroup group) => (group.Flags & ParticleFlags.GroupRespawns) != 0;

    private static bool Staggered(ParticleGroup group) => (group.Flags & ParticleFlags.GroupStaggered) == ParticleFlags.GroupStaggered;

    private static bool VolumeBorn(ParticleEffect effect) => (effect.Shape.Flags & ParticleFlags.ShapeBox) != 0;

    private static bool Curved(ParticleEffect effect)
    {
        bool accelerates = effect.Move.Accel.Any(range => range.Min != 0.0 || range.Max != 0.0);
        ParticleSprite sprite = effect.Sprite;
        bool spinsUnevenly = (sprite.Flags & ParticleFlags.SpriteRotates) != 0
            && (sprite.RotationAccel.Min != 0.0 || sprite.RotationAccel.Max != 0.0 || sprite.RotationDamping != 1.0);

        return accelerates || effect.Move.Damping != 1.0 || spinsUnevenly || (sprite.Flags & ParticleFlags.SpriteFacesMotion) != 0;
    }

    private static void LifeRange(ParticleGroup group, out int low, out int high)
    {
        if (group.LifeMax <= 0)
        {
            low = high = 1;
            return;
        }

        low = Math.Max(1, Math.Min(group.LifeMin, group.LifeMax));
        high = Math.Max(group.LifeMin, group.LifeMax);
    }

    private static void GapRange(ParticleGroup group, out int low, out int high)
    {
        if (Staggered(group))
        {
            low = high = 0;
            return;
        }

        low = Math.Max(0, Math.Min(group.DelayMin, group.DelayMax));
        high = Math.Max(0, Math.Max(group.DelayMin, group.DelayMax));
    }

    private static int OnGrid(double value, int grid, int low, int high)
    {
        int lowest = CeilDiv(low, grid) * grid;
        int highest = Math.Max(lowest, high / grid * grid);

        return Math.Max(lowest, Math.Min(highest, ParticleLife.Rounded(value / grid) * grid));
    }

    private static int LoopLength(ParticleGroup group, int grid)
    {
        LifeRange(group, out int low, out int high);
        GapRange(group, out int gapLow, out int gapHigh);

        return Lives * OnGrid((low + high + gapLow + gapHigh) / 2.0, grid, grid, Unbounded);
    }

    private static void Fitted(Dice dice, ParticleGroup group, int loop, int grid, int[] lengths, int[] gaps)
    {
        LifeRange(group, out int low, out int high);
        GapRange(group, out int gapLow, out int gapHigh);
        int lowest = OnGrid(low, grid, low, high);
        int highest = OnGrid(high, grid, low, high);

        for (int i = 0; i < Lives; ++i)
            lengths[i] = OnGrid(dice.Count(low, high), grid, low, high);

        for (int i = 0; i < Lives; ++i)
            gaps[i] = gapHigh != 0 ? OnGrid(dice.Count(gapLow, gapHigh), grid, 0, Unbounded) : 0;

        int left = loop - lengths.Sum() - gaps.Sum();

        while (left != 0)
        {
            bool moved = false;

            for (int i = 0; i < Lives && left != 0; ++i)
            {
                int step = left > 0 ? grid : -grid;

                if (lengths[i] + step < lowest || lengths[i] + step > highest)
                    continue;

                lengths[i] += step;
                left -= step;
                moved = true;
            }

            if (!moved)
                break;
        }

        if (left != 0)
        {
            gaps[^1] = Math.Max(0, gaps[^1] + left);
            left = loop - lengths.Sum() - gaps.Sum();
        }

        if (left != 0)
            lengths[^1] = Math.Max(grid, lengths[^1] + left);
    }

    private static int Modulo(int value, int loop) => (value % loop + loop) % loop;

    private static List<Lived> MakeLane(Plan plan, int effectIndex, int originIndex, int laneIndex, double[] band, int count, int loop, int grid)
    {
        Dice dice = new(Seed, (uint)effectIndex, (uint)originIndex, LaneDraw + (uint)laneIndex);
        int[] lengths = new int[Lives];
        int[] gaps = new int[Lives];
        Fitted(dice, plan.Effect.Group, loop, grid, lengths, gaps);

        int birth = -dice.Count(0, loop / grid - 1) * grid;
        List<Lived> lane = [];

        for (int i = 0; i < Lives; ++i)
        {
            int start = Modulo(birth, loop);
            lane.Add(new Lived(start, new ParticleLife(plan.Effect, plan.Origins[originIndex], dice, lengths[i], band, laneIndex, count, grid)));
            birth += lengths[i] + gaps[i];
        }

        return lane;
    }

    private static int Bands(Plan plan, int lanes, int perBand) => VolumeBorn(plan.Effect) ? CeilDiv(lanes, perBand) : 1;

    private static int EntriesWanted(List<Plan> plans, int perBand) => plans.Sum(plan => plan.Lanes.Sum(lanes => Bands(plan, lanes, perBand)));

    private static int LayerDepth(double z) => ParticleLife.Rounded(z * BattleCamera.LayerFocal / BattleCamera.EyeDistance);

    private static double EntryScale(int layerZ) => BattleCamera.LayerFocal / Math.Max(BattleCamera.LayerFocal + layerZ, Nearest);

    private static List<Entry> BuildEntries(List<Plan> plans)
    {
        List<Entry> entries = [];
        int perBand = LanesPerBand;

        while (EntriesWanted(plans, perBand) > ObjectList.MostEntries)
            ++perBand;

        for (int e = 0; e < plans.Count; ++e)
        {
            Plan plan = plans[e];
            ParticleShape shape = plan.Effect.Shape;
            int grid = Curved(plan.Effect) ? Step : 1;
            int loop = LoopLength(plan.Effect.Group, grid);
            double scale = shape.SizeScale != 0.0 ? shape.SizeScale : 1.0;
            bool volume = VolumeBorn(plan.Effect);

            for (int o = 0; o < plan.Origins.Count; ++o)
            {
                int lanes = plan.Lanes[o];
                int bands = Bands(plan, lanes, perBand);
                double low = volume ? shape.BoxMin[2] : 0.0;
                double high = volume ? shape.BoxMax[2] : 0.0;
                double width = (high - low) / bands;

                for (int band = 0; band < bands; ++band)
                {
                    double[] span = [low + band * width, low + (band + 1) * width];
                    double depth = plan.Origins[o][2] + (span[0] + span[1]) * 0.5 * scale;
                    Entry entry = new()
                    {
                        Letter = (char)('a' + e),
                        Index = entries.Count,
                        Effect = e,
                        Loop = loop,
                        LayerZ = LayerDepth(depth),
                        Prio = depth >= 0.0 ? PrioBehind : PrioFront,
                    };

                    for (int k = band * lanes / bands; k < (band + 1) * lanes / bands; ++k)
                        entry.Lanes.Add(MakeLane(plan, e, o, k, span, lanes, loop, grid));

                    entries.Add(entry);
                }
            }
        }

        return entries;
    }

    private static List<double> Turns(ParticleLife life, LayerCamera camera, bool facesMotion)
    {
        List<double> turns = new(life.Length);

        if (!facesMotion)
        {
            for (int age = 0; age < life.Length; ++age)
                turns.Add(life.At(age).Angle / Tau);

            return turns;
        }

        bool known = false;
        double last = 0.0;

        for (int age = 0; age < life.Length; ++age)
        {
            int ahead = age + 1 < life.Length ? age + 1 : age;
            int behind = ahead - 1;
            double dx = 0.0;
            double dy = 0.0;

            if (behind >= 0)
            {
                double[] to = camera.Project(life.At(ahead).Position);
                double[] from = camera.Project(life.At(behind).Position);
                dx = to[0] - from[0];
                dy = to[1] - from[1];
            }

            double turn = dx != 0.0 || dy != 0.0 ? Math.Atan2(dx, -dy) / Tau : last;

            if (known)
                turn -= Math.Floor(turn - last + 0.5);

            turns.Add(turn);
            last = turn;
            known = true;
        }

        return turns;
    }

    private static int[] Pretint(ParticleSprite sprite)
    {
        double[][] keys = [ParticleLife.Channels(sprite.ColourStart), ParticleLife.Channels(sprite.ColourMid), ParticleLife.Channels(sprite.ColourEnd)];
        int[] pretint = new int[3];

        for (int channel = 0; channel < 3; ++channel)
        {
            double peak = Math.Max(keys[0][channel + 1], Math.Max(keys[1][channel + 1], keys[2][channel + 1]));
            pretint[channel] = peak != 0.0 ? (int)peak : 255;
        }

        return pretint;
    }

    private static PatWriteSprite SpriteFor(int ident, ParticleLife life, int age, double turn, Entry entry, Cell cell, LayerCamera camera)
    {
        ParticleState state = life.At(age);
        double factor = camera.Factor(state.Position);
        double[] point = camera.Project(state.Position);
        double fit = EntryScale(entry.LayerZ);
        double centre = -camera.Ground;
        bool visible = age < life.Sunk;
        byte[] tint = new byte[4];
        tint[3] = (byte)(visible ? Math.Max(0, Math.Min(255, ParticleLife.Rounded(state.Colour[0]))) : 0);

        for (int channel = 0; channel < 3; ++channel)
            tint[channel] = (byte)Math.Min(255, ParticleLife.Rounded(state.Colour[channel + 1] * 255.0 / cell.Pretint[channel]));

        return new PatWriteSprite
        {
            Id = ident,
            X = ParticleLife.Rounded(point[0] / fit),
            Y = ParticleLife.Rounded(centre + (point[1] - centre) / fit),
            Additive = Additive,
            ZoomX = (float)(state.Size[0] * factor / (cell.Source[2] * fit)),
            ZoomY = (float)(state.Size[1] * factor / (cell.Source[3] * fit)),
            Priority = SpritePriority,
            Part = cell.Part,
            Tint = tint,
            Turn = (float)turn,
        };
    }

    private static PatWriteSprite HiddenSprite(Cell cell)
    {
        return new PatWriteSprite { Additive = Additive, ZoomX = HiddenZoom, ZoomY = HiddenZoom, Priority = SpritePriority, Part = cell.Part, Tint = new byte[4] };
    }

    private static List<Frame> EntryFrames(Entry entry, Cell cell, LayerCamera camera, bool facesMotion)
    {
        SortedSet<int> times = [0];
        Dictionary<ParticleLife, List<double>> turned = new(ReferenceEqualityComparer.Instance);

        foreach (Lived lived in entry.Lanes.SelectMany(lane => lane))
        {
            foreach (int age in lived.Life.Keys)
                times.Add(Modulo(lived.Birth + age, entry.Loop));

            turned[lived.Life] = Turns(lived.Life, camera, facesMotion);
        }

        List<int> sorted = [.. times];
        List<Frame> frames = [];

        for (int k = 0; k < sorted.Count; ++k)
        {
            int when = sorted[k];
            string name = $"{entry.Letter}{entry.Index:D2}_{k:D4}";
            int wait = (k + 1 < sorted.Count ? sorted[k + 1] : entry.Loop) - when;
            List<PatWriteSprite> sprites = [];

            for (int number = 0; number < entry.Lanes.Count; ++number)
            {
                List<Lived> lane = entry.Lanes[number];

                for (int which = 0; which < lane.Count; ++which)
                {
                    ParticleLife life = lane[which].Life;
                    int age = Modulo(when - lane[which].Birth, entry.Loop);

                    if (age > life.LastKey)
                        continue;

                    sprites.Add(SpriteFor(2 * number + which % 2, life, age, turned[life][age], entry, cell, camera));
                    break;
                }
            }

            if (sprites.Count == 0)
                sprites.Add(HiddenSprite(cell));

            frames.Add(new Frame(new PatWritePattern(name, sprites), wait));
        }

        return frames;
    }

    private static List<(int X, int Y)>? ShelfPack(List<Cell> cells, int side, int grain)
    {
        List<(int, int)> places = [];
        int x = 0;
        int y = 0;
        int row = 0;

        foreach (Cell cell in cells)
        {
            int width = cell.Source[2];
            int height = cell.Source[3];

            if (x + width > side)
            {
                x = 0;
                y += row;
                row = 0;
            }

            if (y + height > side || width > side)
                return null;

            places.Add((x, y));
            x += CeilDiv(width, grain) * grain;
            row = Math.Max(row, CeilDiv(height, grain) * grain);
        }

        return places;
    }

    private static bool PackCells(List<Cell> cells, out int side)
    {
        side = 0;

        foreach (int candidate in AtlasSides)
        {
            List<(int X, int Y)>? places = ShelfPack(cells, candidate, candidate / UvSpace);

            if (places is null)
                continue;

            for (int i = 0; i < cells.Count; ++i)
                (cells[i].X, cells[i].Y) = places[i];

            side = candidate;
            return true;
        }

        return false;
    }

    private static PatWriteAtlas AtlasFor(List<Cell> cells, int side, ParticleSurface surface, string stage)
    {
        byte[] rgba = new byte[side * side * 4];

        foreach (Cell cell in cells)
        {
            for (int yy = 0; yy < cell.Source[3]; ++yy)
            {
                for (int xx = 0; xx < cell.Source[2]; ++xx)
                {
                    long at = ((long)(cell.Source[1] + yy) * surface.Width + cell.Source[0] + xx) * 4;
                    int to = ((cell.Y + yy) * side + cell.X + xx) * 4;

                    if (at < 0 || at + 3 >= surface.Bgra.Length)
                        continue;

                    rgba[to] = (byte)(surface.Bgra[at + 2] * cell.Pretint[0] / 255);
                    rgba[to + 1] = (byte)(surface.Bgra[at + 1] * cell.Pretint[1] / 255);
                    rgba[to + 2] = (byte)(surface.Bgra[at] * cell.Pretint[2] / 255);
                    rgba[to + 3] = surface.Bgra[at + 3];
                }
            }
        }

        return new PatWriteAtlas(stage, side, side, rgba);
    }

    private static List<PatWriteCutout> Cutouts(List<Cell> cells, int side)
    {
        int grain = side / UvSpace;

        return cells.Select(cell => new PatWriteCutout(cell.Part, cell.Name, cell.Source[2] / 2, cell.Source[3] / 2, cell.X / grain, cell.Y / grain,
            cell.Source[2] / grain, cell.Source[3] / grain, cell.Source[2], cell.Source[3])).ToList();
    }

    private static string ObjectText(string stage, string patName, List<Entry> entries, List<List<Frame>> frames)
    {
        StringBuilder text = new($"BgObject <-\r\n{{\r\n\tpanidata = \"./bg/{stage}/{patName}\",\r\n\r\n");

        for (int i = 0; i < entries.Count; ++i)
        {
            text.Append(CultureInfo.InvariantCulture, $"\tdata{i + 1:D3} =\r\n\t[\r\n");

            foreach (Frame frame in frames[i])
                text.Append(CultureInfo.InvariantCulture, $"\t\t{{ tag=\"frm\", name=\"{frame.Pattern.Name}\", wait={frame.Wait} }},\r\n");

            text.Append(CultureInfo.InvariantCulture, $"\t\t{{ tag=\"prio\", val={entries[i].Prio} }},\r\n\t\t{{ tag=\"startpos\", x=0, y=0, z={entries[i].LayerZ} }},\r\n");
            text.Append("\t\t{ tag=\"startdelay\", val=0 },\r\n\t]\r\n\r\n");
        }

        return text.Append("}\r\n").ToString();
    }

    private static int CellFor(ParticleEffect effect, List<Cell> cells)
    {
        Cell cell = new();
        int x = (int)effect.Sprite.Uv[0];
        int y = (int)effect.Sprite.Uv[1];
        cell.Source[0] = x;
        cell.Source[1] = y;
        cell.Source[2] = (int)effect.Sprite.Uv[2] - x;
        cell.Source[3] = (int)effect.Sprite.Uv[3] - y;
        Pretint(effect.Sprite).CopyTo(cell.Pretint, 0);

        int known = cells.FindIndex(one => one.Source.SequenceEqual(cell.Source) && one.Pretint.SequenceEqual(cell.Pretint));

        if (known >= 0)
            return known;

        cell.Part = cells.Count + 1;
        cell.Name = effect.Name;
        cells.Add(cell);

        return cells.Count - 1;
    }

    private static bool Alive(ParticleLife life, int age) => age >= 0 && age < life.Length && age < life.Sunk;

    private static CardPose Hidden() => new() { Position = new double[3], Size = new double[2] };

    private static CardPose PoseOf(ParticleLife life, List<double> turns, int age)
    {
        ParticleState state = life.At(age);
        double alpha = Math.Max(0.0, Math.Min(1.0, state.Colour[0] / 255.0));

        return new CardPose
        {
            Shown = alpha > 0.0,
            Turn = turns[age] * Tau,
            Position = (double[])state.Position.Clone(),
            Size = [state.Size[0] * alpha, state.Size[1] * alpha],
        };
    }

    private static CardPose PoseAt(List<Lived> lane, List<List<double>> turned, int when, int loop)
    {
        for (int which = 0; which < lane.Count; ++which)
        {
            ParticleLife life = lane[which].Life;
            int age = Modulo(when - lane[which].Birth, loop);

            if (Alive(life, age))
                return PoseOf(life, turned[which], age);
        }

        return Hidden();
    }

    private static double[] TintOf(ParticleEffect effect, Cell cell)
    {
        double[][] keys = [ParticleLife.Channels(effect.Sprite.ColourStart), ParticleLife.Channels(effect.Sprite.ColourMid), ParticleLife.Channels(effect.Sprite.ColourEnd)];

        return Enumerable.Range(0, 3).Select(channel => Math.Min(1.0, (keys[0][channel + 1] + keys[1][channel + 1] + keys[2][channel + 1]) / 3.0 / cell.Pretint[channel])).ToArray();
    }

    private static bool Packed(List<Cell> cells, ParticleSurface surface, ParticleCards cards)
    {
        if (cells.Count == 0 || surface.Bgra.Length == 0 || !PackCells(cells, out int side))
            return false;

        cards.Side = side;
        cards.Rgba = AtlasFor(cells, side, surface, string.Empty).Rgba;

        foreach (Cell cell in cells)
            cards.Cells.Add([(double)cell.X / side, (double)cell.Y / side, (double)(cell.X + cell.Source[2]) / side, (double)(cell.Y + cell.Source[3]) / side]);

        return true;
    }

    private static List<ParticleEffect> BurstParts(IReadOnlyList<ParticleEffect> effects, ParticleEffect effect)
    {
        List<ParticleEffect> parts = [effect];
        int child = effect.Group.ChildStart;

        if ((effect.Group.Flags & ParticleFlags.GroupChildAtStart) != 0 && child >= 0 && child < effects.Count && effects[child].Sprite.SharedAtlas == 0)
            parts.Add(effects[child]);

        return parts;
    }
}
