using FbxToMua.Core.Export;

namespace FbxToMua.Core.Import;

internal sealed class Dice
{
    private const uint FnvBasis = 0x811c9dc5u;
    private const uint FnvPrime = 0x01000193u;
    private const uint Fallback = 0x9e3779b9u;
    private const double Span = 4294967296.0;

    private uint _state;

    public Dice(params uint[] keys)
    {
        uint state = FnvBasis;

        foreach (uint key in keys)
            state = unchecked((state ^ key) * FnvPrime);

        _state = state != 0 ? state : Fallback;
    }

    public uint Next()
    {
        uint x = _state;
        x ^= x << 13;
        x ^= x >> 17;
        x ^= x << 5;
        _state = x;

        return x;
    }

    public double Unit() => Next() / Span;

    public double Between(double low, double high) => low == high ? low : low + Unit() * (high - low);

    public double Between(ParticleRange range) => Between(range.Min, range.Max);

    public int Count(int a, int b)
    {
        int low = Math.Min(a, b);
        int high = Math.Max(a, b);

        return low + Math.Min((int)(Unit() * (high - low + 1)), high - low);
    }
}

internal readonly record struct ParticleState(double[] Position, double[] Size, double[] Colour, double Angle);

internal static class ParticleFlags
{
    public const uint GroupRespawns = 0x10;
    public const uint GroupStaggered = 0x60;
    public const uint GroupChildAtStart = 0x80;
    public const uint SpriteDepthTest = 0x4;
    public const uint SpriteRotates = 0x40;
    public const uint SpriteHasMid = 0x800;
    public const uint SpriteFacesMotion = 0x20000;
    public const uint SpriteStartAspect = 0x40000;
    public const uint SpriteMidAspect = 0x80000;
    public const uint SpriteMidRelative = 0x4000000;
    public const uint SpriteHasEnd = 0x20000000;
    public const uint SpriteEndRelative = 0x80000000u;
    public const uint Sprite2EndAspect = 0x1;
    public const uint Sprite2MidOfEnd = 0x2;
    public const uint ShapeRound = 0x2;
    public const uint ShapeBox = 0x4;
    public const uint ShapeExactRadius = 0x8;
    public const uint ShapeCircle = 0x40;
    public const uint ShapePlaneXy = 0x80;
    public const uint ShapePlaneYz = 0x200;
    public const uint ShapeEven = 0x40000000;
    public const uint MoveVelocity = 0x2;
    public static readonly uint[] MoveFlips = [0x8, 0x10, 0x20];
}

internal sealed class ParticleLife
{
    private const double Tau = 2.0 * Math.PI;

    private readonly List<ParticleState> _states = [];

    public int Length { get; }
    public int Sunk { get; }
    public List<int> Keys { get; } = [];

    public int LastKey => Keys[^1];

    public ParticleState At(int age) => _states[age];

    public ParticleLife(ParticleEffect effect, double[] origin, Dice dice, int length, double[] band, int index, int count, int grid)
    {
        Length = length;
        Sunk = length;
        ParticleSprite sprite = effect.Sprite;
        double scale = effect.Shape.SizeScale != 0.0 ? effect.Shape.SizeScale : 1.0;
        double[] offset = BirthOffset(effect.Shape, dice, band, index, count);
        double[] position = [origin[0] + offset[0] * scale, origin[1] + offset[1] * scale, origin[2] + offset[2] * scale];
        (double[] velocity, double[] acceleration, double damping) = BirthMotion(effect.Move, dice);
        double[][] colours = [Channels(sprite.ColourStart), Channels(sprite.ColourMid), Channels(sprite.ColourEnd)];
        int colourSpan = Period(dice, sprite.ColourPeriodMin, sprite.ColourPeriodMax, length);
        double[][] sizes = SizeKeys(sprite, dice);
        int sizeSpan = Period(dice, sprite.ScalePeriodMin, sprite.ScalePeriodMax, length);
        (double angle, double speed, double accel, double spinDamping) = SpinKeys(sprite, dice);

        for (int age = 0; age < length; ++age)
        {
            double[] colour = KeyAt(colours[0], colours[1], colours[2], sprite.ColourMidAt, age % colourSpan / (double)colourSpan);
            double[] size = KeyAt(sizes[0], sizes[1], sizes[2], sprite.ScaleMidAt, age % sizeSpan / (double)sizeSpan);
            _states.Add(new ParticleState((double[])position.Clone(), [size[0] * scale, size[1] * scale], colour, angle));

            for (int axis = 0; axis < 3; ++axis)
            {
                velocity[axis] = damping * (velocity[axis] + acceleration[axis]);
                position[axis] = position[axis] + velocity[axis] * scale;
            }

            speed = (speed + accel) * spinDamping;
            angle += speed;
        }

        if ((sprite.Flags & ParticleFlags.SpriteDepthTest) != 0)
            Sunk = SunkAt();

        if (grid > 1)
        {
            for (int age = 0; age < length; age += grid)
                Keys.Add(age);

            return;
        }

        SortedSet<int> keys = [0, length - 1];
        Knots(length, colourSpan, sprite.ColourMidAt, keys);

        if (!sizes[0].SequenceEqual(sizes[1]) || !sizes[0].SequenceEqual(sizes[2]))
            Knots(length, sizeSpan, sprite.ScaleMidAt, keys);

        if (Sunk < length)
        {
            keys.Add(Sunk);
            keys.Add(Math.Max(Sunk - 1, 0));
        }

        Keys.AddRange(keys.Where(key => key >= 0 && key < length));
    }

    public static int Rounded(double value) => (int)Math.Floor(value + 0.5);

    public static double[] Channels(uint colour) => [(colour >> 24) & 0xff, (colour >> 16) & 0xff, (colour >> 8) & 0xff, colour & 0xff];

    private int SunkAt()
    {
        int index = _states.FindIndex(state => state.Position[1] < 0.0);

        return index < 0 ? _states.Count : index;
    }

    private static double[] Blend(double[] a, double[] b, double t)
    {
        double[] blended = new double[a.Length];

        for (int i = 0; i < a.Length; ++i)
            blended[i] = a[i] + (b[i] - a[i]) * t;

        return blended;
    }

    private static double[] KeyAt(double[] start, double[] mid, double[] end, double midAt, double t)
    {
        if (t < midAt)
            return Blend(start, mid, t / midAt);

        double span = 1.0 - midAt;

        return span <= 0.0 ? end : Blend(mid, end, (t - midAt) / span);
    }

    private static int Period(Dice dice, int low, int high, int life)
    {
        int value = dice.Count(low, high);

        return value <= 0 ? life : value;
    }

    private static double[] SpherePoint(Dice dice, double radius)
    {
        while (true)
        {
            double x = dice.Between(-1.0, 1.0);
            double y = dice.Between(-1.0, 1.0);
            double z = dice.Between(-1.0, 1.0);
            double length = Math.Sqrt(x * x + y * y + z * z);

            if (length > 0.0 && length <= 1.0)
                return [x / length * radius, y / length * radius, z / length * radius];
        }
    }

    private static double[] CirclePoint(ParticleShape shape, Dice dice, int index, int count, double radius)
    {
        double angle = shape.AngleOffset + dice.Unit() * shape.AngleRange;

        if ((shape.Flags & ParticleFlags.ShapeEven) != 0 && count != 0)
            angle += Tau * index / count;

        double a = Math.Cos(angle) * radius;
        double b = Math.Sin(angle) * radius;

        if ((shape.Flags & ParticleFlags.ShapePlaneXy) != 0)
            return [a, b, 0.0];

        return (shape.Flags & ParticleFlags.ShapePlaneYz) != 0 ? [0.0, a, b] : [a, 0.0, b];
    }

    private static double[] BirthOffset(ParticleShape shape, Dice dice, double[] band, int index, int count)
    {
        if ((shape.Flags & ParticleFlags.ShapeBox) != 0)
        {
            double x = dice.Between(shape.BoxMin[0], shape.BoxMax[0]);
            double y = dice.Between(shape.BoxMin[1], shape.BoxMax[1]);

            return [x, y, dice.Between(band[0], band[1])];
        }

        if ((shape.Flags & ParticleFlags.ShapeRound) == 0)
            return [0.0, 0.0, 0.0];

        double radius = (shape.Flags & ParticleFlags.ShapeExactRadius) != 0 ? shape.Radius : dice.Unit() * shape.Radius;

        return (shape.Flags & ParticleFlags.ShapeCircle) != 0 ? CirclePoint(shape, dice, index, count, radius) : SpherePoint(dice, radius);
    }

    private static (double[] Velocity, double[] Acceleration, double Damping) BirthMotion(ParticleMove move, Dice dice)
    {
        double[] velocity = new double[3];
        double[] acceleration = new double[3];

        if ((move.Flags & ParticleFlags.MoveVelocity) == 0)
            return (velocity, acceleration, 1.0);

        for (int axis = 0; axis < 3; ++axis)
            velocity[axis] = dice.Between(move.Velocity[axis]);

        for (int axis = 0; axis < 3; ++axis)
        {
            if ((move.Flags & ParticleFlags.MoveFlips[axis]) != 0 && dice.Unit() < 0.5)
                velocity[axis] = -velocity[axis];
        }

        for (int axis = 0; axis < 3; ++axis)
            acceleration[axis] = dice.Between(move.Accel[axis]);

        return (velocity, acceleration, move.Damping);
    }

    private static double[] KeyedSize(ParticleSize size, Dice dice, bool aspect)
    {
        double widthMax = size.Width.Max;
        double width = dice.Between(size.Width.Min, widthMax);

        if (aspect && widthMax != 0.0)
            return [width, width * size.Height.Max / widthMax];

        return [width, dice.Between(size.Height)];
    }

    private static double[] Scaled(double[] a, double[] b) => [a[0] * b[0], a[1] * b[1]];

    private static double[][] SizeKeys(ParticleSprite sprite, Dice dice)
    {
        double[] start = KeyedSize(sprite.ScaleStart, dice, (sprite.Flags & ParticleFlags.SpriteStartAspect) != 0);
        double[] end = start;
        double[] mid = start;

        if ((sprite.Flags & ParticleFlags.SpriteHasEnd) != 0)
        {
            end = KeyedSize(sprite.ScaleEnd, dice, (sprite.Flags2 & ParticleFlags.Sprite2EndAspect) != 0);

            if ((sprite.Flags & ParticleFlags.SpriteEndRelative) != 0)
                end = Scaled(end, start);
        }

        if ((sprite.Flags & ParticleFlags.SpriteHasMid) == 0)
            return [start, mid, end];

        mid = KeyedSize(sprite.ScaleMid, dice, (sprite.Flags & ParticleFlags.SpriteMidAspect) != 0);

        if ((sprite.Flags & ParticleFlags.SpriteMidRelative) != 0)
            mid = Scaled(mid, start);

        if ((sprite.Flags & ParticleFlags.SpriteHasEnd) != 0 && (sprite.Flags2 & ParticleFlags.Sprite2MidOfEnd) != 0)
            mid = Scaled(mid, end);

        return [start, mid, end];
    }

    private static (double Angle, double Speed, double Accel, double Damping) SpinKeys(ParticleSprite sprite, Dice dice)
    {
        if ((sprite.Flags & ParticleFlags.SpriteRotates) == 0)
            return (0.0, 0.0, 0.0, 1.0);

        double angle = dice.Between(sprite.RotationStart);
        double speed = dice.Between(sprite.RotationSpeed);
        double accel = dice.Between(sprite.RotationAccel);

        return (angle, speed, accel, sprite.RotationDamping);
    }

    private static void Knots(int length, int span, double midAt, SortedSet<int> keys)
    {
        for (int start = 0; start < length; start += span)
        {
            keys.Add(start);

            if (start > 0)
                keys.Add(start - 1);

            int mid = start + Rounded(midAt * span);

            if (mid < length)
                keys.Add(mid);
        }
    }
}

internal sealed class LayerCamera
{
    private const double Nearest = 1.0;

    private readonly double _focal;

    public double Ground { get; }

    public LayerCamera()
    {
        double pixels = BattleCamera.LayerUnits();
        _focal = pixels * BattleCamera.EyeDistance;
        Ground = BattleCamera.EyeHeight * pixels;
    }

    public double Factor(double[] position) => _focal / Math.Max(BattleCamera.EyeDistance + position[2], Nearest);

    public double[] Project(double[] position)
    {
        double factor = Factor(position);

        return [position[0] * factor, -((position[1] - BattleCamera.EyeHeight) * factor + Ground)];
    }
}
