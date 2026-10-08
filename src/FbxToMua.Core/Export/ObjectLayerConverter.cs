using FbxToMua.Core.Formats.Mua;
using FbxToMua.Core.Formats.Objects;
using FbxToMua.Core.Formats.Pat;
using FbxToMua.Core.Geometry;

namespace FbxToMua.Core.Export;

internal sealed class ObjectLayerConverter(PatDocument sheet, string stem, ObjectLayer layer)
{
    private const double Tau = 2.0 * Math.PI;
    private const float UvSpace = 256.0f;
    private const float HiddenScale = 1e-4f;
    private const float BindableZoom = 1e-2f;
    private const float HalfTurn = 0.5f;
    private const float WholeTurn = 1.0f;
    private const int Channels = 4;
    private const string AtlasInfix = "_atlas";
    private const string AtlasSuffix = ".dds";

    private readonly record struct Key(PatPattern Pattern, int Start, int Wait);

    private readonly record struct Track(int Id, int Part, int Priority, int Blend);

    private struct State
    {
        public float X;
        public float Y;
        public float ZoomX;
        public float ZoomY;
        public float Turns;
        public float Pitch;
        public float Yaw;
        public Float4 Colour;
    }

    private readonly Dictionary<int, int> _atlases = [];

    public void Add(ObjectEntry entry)
    {
        List<Key> keys = Keys(entry);

        if (keys.Count == 0)
            return;

        int span = keys[^1].Start + keys[^1].Wait;
        List<Track> tracks = Tracks(keys);
        List<int> blends = tracks.Select(track => track.Blend).Distinct().ToList();

        foreach (int blend in blends)
            AddGroup(entry, keys, tracks, span, blend);
    }

    private List<Key> Keys(ObjectEntry entry)
    {
        List<Key> keys = [];
        int start = 0;

        foreach (ObjectFrame frame in entry.Frames)
        {
            PatPattern? pattern = sheet.Find(frame.Name);

            if (pattern is null)
            {
                if (!layer.Missing.Contains(frame.Name))
                    layer.Missing.Add(frame.Name);

                continue;
            }

            int wait = Math.Max(1, frame.Wait);
            keys.Add(new Key(pattern, start, wait));
            start += wait;
        }

        return keys;
    }

    private static bool Same(PatSprite sprite, Track track) => sprite.Id == track.Id && sprite.Part == track.Part;

    private static PatSprite? SpriteIn(PatPattern pattern, Track track) => pattern.Sprites.FirstOrDefault(sprite => Same(sprite, track));

    private static List<Track> Tracks(List<Key> keys)
    {
        List<Track> tracks = [];

        foreach (Key key in keys)
        {
            foreach (PatSprite sprite in key.Pattern.Sprites)
            {
                if (!tracks.Any(track => Same(sprite, track)))
                    tracks.Add(new Track(sprite.Id, sprite.Part, sprite.Priority, sprite.Blend));
            }
        }

        return tracks.OrderBy(track => track.Priority).ToList();
    }

    private static State StateOf(PatSprite sprite)
    {
        return new State
        {
            X = sprite.X,
            Y = sprite.Y,
            ZoomX = sprite.ZoomX,
            ZoomY = sprite.ZoomY,
            Turns = sprite.Turns,
            Pitch = sprite.Pitch,
            Yaw = sprite.Yaw,
            Colour = new Float4((sprite.Tint >> 16) & 0xff, (sprite.Tint >> 8) & 0xff, sprite.Tint & 0xff, sprite.Tint >> 24),
        };
    }

    private static float Mix(float from, float to, float t) => from + (to - from) * t;

    private static float ShortestTurn(float from, float to)
    {
        float step = to - from;

        if (step > HalfTurn)
            return step - WholeTurn;

        if (step <= -HalfTurn)
            return step + WholeTurn;

        return step;
    }

    private static float MixTurns(float from, float to, float t) => from + ShortestTurn(from, to) * t;

    private static State Between(State from, State to, float t)
    {
        State between = new()
        {
            X = Mix(from.X, to.X, t),
            Y = Mix(from.Y, to.Y, t),
            ZoomX = Mix(from.ZoomX, to.ZoomX, t),
            ZoomY = Mix(from.ZoomY, to.ZoomY, t),
            Turns = MixTurns(from.Turns, to.Turns, t),
            Pitch = MixTurns(from.Pitch, to.Pitch, t),
            Yaw = MixTurns(from.Yaw, to.Yaw, t),
        };

        for (int c = 0; c < Channels; ++c)
            between.Colour[c] = Mix(from.Colour[c], to.Colour[c], t);

        return between;
    }

    private static int KeyAt(List<Key> keys, int time)
    {
        for (int i = 0; i < keys.Count; ++i)
        {
            if (time < keys[i].Start + keys[i].Wait)
                return i;
        }

        return keys.Count - 1;
    }

    private static bool StateAt(List<Key> keys, Track track, int time, out State state)
    {
        state = default;
        int index = KeyAt(keys, time);
        Key key = keys[index];
        PatSprite? now = SpriteIn(key.Pattern, track);

        if (now is null)
            return false;

        state = StateOf(now);
        PatSprite? next = SpriteIn(keys[(index + 1) % keys.Count].Pattern, track);

        if (next is not null)
            state = Between(state, StateOf(next), (float)(time - key.Start) / key.Wait);

        return true;
    }

    private static Matrix Turn(int first, int second, double angle)
    {
        Matrix turn = Matrix.Identity;
        float cosine = (float)Math.Cos(angle);
        float sine = (float)Math.Sin(angle);

        turn[first * 4 + first] = cosine;
        turn[first * 4 + second] = sine;
        turn[second * 4 + first] = -sine;
        turn[second * 4 + second] = cosine;

        return turn;
    }

    private static float Guarded(float zoom)
    {
        if (MathF.Abs(zoom) >= HiddenScale)
            return zoom;

        return zoom < 0.0f ? -HiddenScale : HiddenScale;
    }

    private static State Bindable(State state)
    {
        State bindable = state;
        bindable.ZoomX = MathF.Abs(state.ZoomX) < BindableZoom ? 1.0f : state.ZoomX;
        bindable.ZoomY = MathF.Abs(state.ZoomY) < BindableZoom ? 1.0f : state.ZoomY;

        return bindable;
    }

    private static Pose JointPose(State state)
    {
        Matrix matrix = Matrix.Identity;
        matrix[0] = Guarded(state.ZoomX);
        matrix[5] = Guarded(state.ZoomY);
        matrix = MatrixMath.Multiply(matrix, Turn(0, 1, state.Turns * Tau));
        matrix = MatrixMath.Multiply(matrix, Turn(1, 2, -state.Pitch * Tau));
        matrix = MatrixMath.Multiply(matrix, Turn(2, 0, -state.Yaw * Tau));
        matrix[12] = state.X;
        matrix[13] = state.Y;

        return PoseMath.Split(matrix);
    }

    private static Pose FramePose(Float3 start)
    {
        float lateral = (float)(1.0 / BattleCamera.LayerUnits());
        float depth = (float)(BattleCamera.EyeDistance / BattleCamera.LayerFocal);

        Matrix matrix = Matrix.Identity;
        matrix[0] = lateral;
        matrix[5] = -lateral;
        matrix[10] = -depth;
        matrix[12] = start[0] * lateral;
        matrix[13] = -start[1] * lateral;
        matrix[14] = start[2] * depth;

        return PoseMath.Split(matrix);
    }

    private static LayerCorner[] Corners(PatPart part)
    {
        float left = -part.PivotX;
        float top = -part.PivotY;
        float right = left + part.Width;
        float bottom = top + part.Height;
        float u0 = part.U / UvSpace;
        float v0 = part.V / UvSpace;
        float u1 = (part.U + part.W) / UvSpace;
        float v1 = (part.V + part.H) / UvSpace;

        return
        [
            new LayerCorner(new Float3(left, top, 0.0f), u0, v0),
            new LayerCorner(new Float3(right, top, 0.0f), u1, v0),
            new LayerCorner(new Float3(right, bottom, 0.0f), u1, v1),
            new LayerCorner(new Float3(left, bottom, 0.0f), u0, v1),
        ];
    }

    private void AddGroup(ObjectEntry entry, List<Key> keys, List<Track> tracks, int span, int blend)
    {
        LayerGroup group = new() { Entry = entry.Number, Prio = entry.Prio, Blend = blend, Span = span, Frame = FramePose(entry.Start) };

        foreach (Track track in tracks)
        {
            if (track.Blend != blend)
                continue;

            LayerSprite? sprite = Describe(track);

            if (sprite is null)
                continue;

            Animate(keys, track, span, entry.Delay, sprite);
            group.Moves = group.Moves || Moves(sprite);
            group.Sprites.Add(sprite);
        }

        if (group.Sprites.Count == 0)
            return;

        layer.Sprites += group.Sprites.Count;
        layer.Front += group.Prio >= ObjectLayer.FrontPrio ? group.Sprites.Count : 0;
        layer.Groups.Add(group);
    }

    private LayerSprite? Describe(Track track)
    {
        PatPart? part = sheet.PartOf(track.Part);

        if (part is null)
            return null;

        int atlas = AtlasIndex(part.Atlas);

        return atlas < 0 ? null : new LayerSprite { Atlas = atlas, Corners = Corners(part) };
    }

    private int AtlasIndex(int id)
    {
        if (_atlases.TryGetValue(id, out int known))
            return known;

        PatAtlas? atlas = sheet.AtlasOf(id);

        if (atlas is null)
            return -1;

        int index = layer.Atlases.Count;
        layer.Atlases.Add(new LayerImage($"{stem}{AtlasInfix}{id}{AtlasSuffix}", atlas.Dds));
        _atlases[id] = index;

        return index;
    }

    private static void Animate(List<Key> keys, Track track, int span, int delay, LayerSprite sprite)
    {
        State reference = StateOf(SpriteIn(KeyWith(keys, track).Pattern, track)!);
        sprite.Rest = JointPose(Bindable(reference));
        double[] colour = new double[Channels];
        int shown = 0;

        for (int frame = 0; frame < span; ++frame)
        {
            int time = ((frame - delay) % span + span) % span;
            bool visible = StateAt(keys, track, time, out State state);
            Pose pose = JointPose(visible ? state : reference);

            if (visible)
            {
                for (int c = 0; c < Channels; ++c)
                    colour[c] += state.Colour[c];

                ++shown;
            }
            else
            {
                pose.Scale = Float3.All(HiddenScale);
            }

            if (sprite.Poses.Count > 0 && sprite.Poses[^1].Turn.Dot(pose.Turn) < 0.0f)
                pose.Turn = pose.Turn.Negated();

            sprite.Poses.Add(pose);
        }

        sprite.Poses.Add(sprite.Poses[0]);

        byte Channel(int c) => (byte)Std.RoundHalfAway(shown > 0 ? colour[c] / shown : 0.0);

        sprite.Colour = new MuaColour(Channel(0), Channel(1), Channel(2), Channel(3));
    }

    private static Key KeyWith(List<Key> keys, Track track)
    {
        foreach (Key key in keys)
        {
            if (SpriteIn(key.Pattern, track) is not null)
                return key;
        }

        return keys[0];
    }

    private static bool Moves(LayerSprite sprite) => sprite.Poses.Any(pose => !pose.SameAs(sprite.Poses[0]));
}
