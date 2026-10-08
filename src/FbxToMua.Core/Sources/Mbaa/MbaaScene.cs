namespace FbxToMua.Core.Sources.Mbaa;

internal readonly record struct MbaaShown(int Instance, int Object, int Image, float X, float Y, int Blend, int Alpha);

internal sealed class MbaaScene
{
    private const int Slots = 2000;
    private const int Subpixels = 128;
    private const int Starting = 1;
    private const int Running = 2;
    private const int Axes = 2;
    private const ulong FnvBasis = 0xcbf29ce484222325ul;
    private const ulong FnvPrime = 0x100000001b3ul;

    private sealed class Actor
    {
        public MbaaLayer? Layer;
        public int State;
        public int Frame;
        public int Next;
        public int Counter;
        public int Timer;
        public int[] Position = new int[Axes];
        public int[] Velocity = new int[Axes];
        public int[] Acceleration = new int[Axes];
        public bool Fired;
        public bool Pushed;
        public int Instance;

        public MbaaFrame Current => Layer!.Frames[Frame];
    }

    private readonly MbaaBg _file;
    private readonly Actor[] _actors;
    private uint _seed;

    public int Instances { get; private set; }
    public bool Rolled { get; private set; }

    public MbaaScene(MbaaBg file, uint seed)
    {
        _file = file;
        _actors = Enumerable.Range(0, Slots).Select(_ => new Actor()).ToArray();
        _seed = seed == 0 ? 1 : seed;

        foreach (MbaaLayer layer in file.Layers)
        {
            if (layer.Placed && layer.Object is >= 0 and < Slots)
                Start(_actors[layer.Object], layer);
        }
    }

    public void Step()
    {
        foreach (Actor actor in _actors.Where(actor => actor.State != 0))
            Advance(actor);

        foreach (Actor actor in _actors.Where(actor => actor.State == Running))
            Move(actor);

        for (int i = 0; i < _actors.Length; ++i)
        {
            if (_actors[i].State == Running)
                Fire(_actors[i]);
        }

        foreach (Actor actor in _actors.Where(actor => actor.State == Running))
            Check(actor);
    }

    public List<MbaaShown> Visible()
    {
        List<MbaaShown> shown = [];

        foreach (Actor actor in _actors)
        {
            if (actor.State != Running || !actor.Current.IsSprite)
                continue;

            int alpha = AlphaNow(actor);

            if (alpha <= 0)
                continue;

            MbaaFrame frame = actor.Current;
            shown.Add(new MbaaShown(actor.Instance, actor.Layer!.Object, frame.Image, frame.X + (float)actor.Position[0] / Subpixels,
                frame.Y + (float)actor.Position[1] / Subpixels, frame.Blend, alpha));
        }

        return shown;
    }

    public List<int> Living() => _actors.Where(actor => actor.State != 0).Select(actor => actor.Instance).ToList();

    public ulong Hash()
    {
        ulong hash = FnvBasis;

        for (int i = 0; i < _actors.Length; ++i)
        {
            Actor actor = _actors[i];

            if (actor.State == 0)
                continue;

            int[] values = [i, actor.Layer!.Object, actor.State, actor.Frame, actor.Counter, actor.Timer, actor.Fired ? 1 : 0, actor.Pushed ? 1 : 0];

            foreach (int value in values)
                hash = Mix(hash, value);

            for (int axis = 0; axis < Axes; ++axis)
            {
                hash = Mix(hash, actor.Position[axis]);
                hash = Mix(hash, actor.Velocity[axis]);
                hash = Mix(hash, actor.Acceleration[axis]);
            }
        }

        return hash;
    }

    private static ulong Mix(ulong hash, int value) => unchecked((hash ^ (uint)value) * FnvPrime);

    private static int NextOf(MbaaFrame frame, int current, int counter)
    {
        return frame.Op switch
        {
            MbaaOp.Next or MbaaOp.NextToo => current + 1,
            MbaaOp.Jump or MbaaOp.JumpToo => frame.Jump,
            MbaaOp.Count => counter > 1 ? frame.Jump : frame.Exit,
            _ => current,
        };
    }

    private void Start(Actor actor, MbaaLayer layer)
    {
        actor.Layer = layer;
        actor.State = Starting;
        actor.Frame = 0;
        actor.Next = 0;
        actor.Counter = 0;
        actor.Timer = 0;
        actor.Position = new int[Axes];
        actor.Velocity = new int[Axes];
        actor.Acceleration = new int[Axes];
        actor.Fired = false;
        actor.Pushed = false;
        actor.Instance = ++Instances;
        Enter(actor);
    }

    private static void Enter(Actor actor)
    {
        actor.Timer = 0;
        actor.Fired = false;
        actor.Pushed = false;

        if (actor.Frame < 0 || actor.Frame >= actor.Layer!.Frames.Count)
            return;

        MbaaFrame frame = actor.Current;

        if (frame.Loops != 0)
            actor.Counter = frame.Loops;

        actor.Next = NextOf(frame, actor.Frame, actor.Counter);
    }

    private static void Advance(Actor actor)
    {
        if (actor.State == Starting)
            actor.State = Running;

        ++actor.Timer;
        MbaaFrame frame = actor.Current;

        if (frame.Duration > actor.Timer)
            return;

        switch (frame.Op)
        {
            case MbaaOp.End:
                actor.State = 0;
                return;
            case MbaaOp.Next:
            case MbaaOp.NextToo:
                ++actor.Frame;
                break;
            case MbaaOp.Jump:
            case MbaaOp.JumpToo:
                actor.Frame = frame.Jump;
                actor.Counter -= actor.Counter > 0 ? 1 : 0;
                break;
            case MbaaOp.Count:
                actor.Counter -= actor.Counter > 0 ? 1 : 0;
                actor.Frame = actor.Counter != 0 ? frame.Jump : frame.Exit;
                break;
        }

        if (actor.Frame >= actor.Layer!.Frames.Count)
        {
            actor.State = 0;
            return;
        }

        Enter(actor);
    }

    private static void Move(Actor actor)
    {
        if (actor.Pushed)
        {
            for (int axis = 0; axis < Axes; ++axis)
            {
                actor.Position[axis] += actor.Velocity[axis];
                actor.Velocity[axis] += actor.Acceleration[axis];
            }

            return;
        }

        MbaaFrame frame = actor.Current;
        bool[] stop = [frame.StopX, frame.StopY];
        bool[] push = [frame.PushX, frame.PushY];

        for (int axis = 0; axis < Axes; ++axis)
        {
            if (stop[axis])
                actor.Velocity[axis] = actor.Acceleration[axis] = 0;

            if (!push[axis])
                continue;

            actor.Velocity[axis] = frame.Velocity[axis];
            actor.Acceleration[axis] = frame.Acceleration[axis];
        }

        actor.Pushed = true;
    }

    private int Roll(int low, int high)
    {
        Rolled = true;

        if (high <= low)
            return low;

        _seed ^= _seed << 13;
        _seed ^= _seed >> 17;
        _seed ^= _seed << 5;

        return low + (int)(_seed % (uint)(high - low));
    }

    private void Spawn(Actor source, int slot, int x, int y)
    {
        MbaaLayer? layer = _file.LayerOf(slot);

        if (layer is null || layer.Frames.Count == 0)
            return;

        Actor? free = _actors.FirstOrDefault(actor => actor.State == 0);

        if (free is null)
            return;

        Start(free, layer);
        int share = source.Layer!.Parallax;
        free.Position[0] = source.Position[0] * share / MbaaBg.FullParallax + x * Subpixels;
        free.Position[1] = source.Position[1] * share / MbaaBg.FullParallax + y * Subpixels;
    }

    private void Velocity(Actor actor, MbaaRecord record)
    {
        if (record.Target != 0)
            return;

        int axis = record.Values[4] != 0 ? 1 : 0;
        actor.Velocity[axis] = Roll(record.Values[0], record.Values[1]);
        actor.Acceleration[axis] = Roll(record.Values[2], record.Values[3]);
    }

    private void Fire(Actor actor)
    {
        if (actor.Fired)
            return;

        actor.Fired = true;

        foreach (int index in actor.Current.Events)
        {
            if (index >= actor.Layer!.Events.Count)
                continue;

            MbaaRecord record = actor.Layer.Events[index];

            if (record.Kind == MbaaEvent.Spawn)
            {
                Spawn(actor, record.Target, record.Values[0], record.Values[1]);
                continue;
            }

            if (record.Kind == MbaaEvent.SpawnAnywhere)
            {
                int y = Roll(record.Values[1], record.Values[3] + 1);
                int x = Roll(record.Values[0], record.Values[2] + 1);
                int slot = record.Target + Roll(0, record.Values[4] & 0xffff);
                Spawn(actor, slot, x, y);
                continue;
            }

            if (record.Kind == MbaaEvent.Velocity)
                Velocity(actor, record);
        }
    }

    private static void Check(Actor actor)
    {
        foreach (int index in actor.Current.Conditions)
        {
            if (index >= actor.Layer!.Conditions.Count)
                continue;

            MbaaRecord condition = actor.Layer.Conditions[index];

            if (condition.Kind != MbaaEvent.Position)
                continue;

            int position = actor.Position[condition.Values[2] != 0 ? 1 : 0];
            int threshold = condition.Values[1];
            bool crossed = condition.Values[3] == 0 ? position > threshold : position < threshold;

            if (!crossed)
                continue;

            if (condition.Values[0] < 0 || condition.Values[0] >= actor.Layer.Frames.Count)
            {
                actor.State = 0;
                return;
            }

            actor.Frame = condition.Values[0];
            Enter(actor);
            return;
        }
    }

    private static int AlphaNow(Actor actor)
    {
        List<MbaaFrame> frames = actor.Layer!.Frames;
        MbaaFrame frame = actor.Current;
        int alpha = frame.AlphaValue;

        if (frame.Tween == 0 || frame.Duration == 0 || actor.Next < 0 || actor.Next >= frames.Count)
            return alpha;

        int target = frames[actor.Next].AlphaValue;

        return alpha + (target - alpha) * actor.Timer / frame.Duration;
    }
}
