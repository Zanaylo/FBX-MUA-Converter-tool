namespace FbxToMua.Core.Formats.Evb;

internal sealed class EvbInstance
{
    private const int Free = 0;
    private const int Skipped = 1;
    private const int Taken = 2;
    private const int Done = 3;
    private const int Frames = 12000;
    private const int RolledFrames = 3600;
    private const double Full = 1000.0;
    private const uint LcgMultiply = 1103515245u;
    private const uint LcgAdd = 12345u;

    private readonly record struct State(long Frame, long Jump, long Pause, long Resume, long Label, long Cursor, long Held,
        bool Lit, uint R0, uint R1, uint R2, uint R3, uint R4, double Ramp, double Target, long Left, double Step, long Take);

    private readonly uint[][] _record;
    private readonly Dictionary<long, long> _labels = [];
    private uint _dice;
    private long _frame;
    private long _jump = -1;
    private long _pause;
    private long _resume;
    private long _label = -1;
    private long _cursor = -1;
    private long _held;
    private bool _lit;
    private readonly uint[] _rect = new uint[5];
    private double _ramp = Full;
    private double _target = Full;
    private long _left;
    private double _step;
    private long _take = -1;
    private long _since;

    public bool Rolled { get; private set; }

    public EvbInstance(uint[][] record, string label)
    {
        _record = record;
        _dice = Crc32(label);

        for (int at = 0; at < _record.Length; ++at)
        {
            if (_record[at][0] == EvbCode.Label)
                _labels.TryAdd(_record[at][1], at);
        }
    }

    public void Play(EvbPlayed played)
    {
        Interpret();
        Walk();

        Dictionary<State, int> seen = [];
        List<EvbSample> samples = [];
        played.Cyclic = false;
        played.From = 0;
        played.Sample = samples;

        for (int tick = 0; tick < Frames; ++tick)
        {
            if (tick != 0)
                Tick();

            samples.Add(Current());

            if (Rolled && !Ended())
            {
                if (tick + 1 < RolledFrames)
                    continue;

                played.Cyclic = true;
                return;
            }

            State state = Snapshot();

            if (seen.TryGetValue(state, out int first))
            {
                Repeating(played, first, tick);
                return;
            }

            seen[state] = tick;
        }

        played.From = samples.Count - 1;
    }

    private static uint Crc32(string text)
    {
        uint crc = 0xffffffffu;

        foreach (byte letter in System.Text.Encoding.Latin1.GetBytes(text))
        {
            crc ^= letter;

            for (int bit = 0; bit < 8; ++bit)
                crc = (crc >> 1) ^ (0xedb88320u & (0u - (crc & 1u)));
        }

        return ~crc;
    }

    private EvbSample Current()
    {
        if (_ramp <= 0.0)
            return new EvbSample(false, default, 0.0, _take, _since);

        EvbRect rect = _lit ? new EvbRect((int)_rect[0], (int)_rect[1], (int)_rect[2], (int)_rect[3], (int)_rect[4]) : default;

        return new EvbSample(_lit, rect, _ramp, _take, _since);
    }

    private bool Ended()
    {
        if (_jump >= 0)
            return false;

        if (_resume >= _record.Length)
            return true;

        uint code = _record[_resume][0];

        return code == EvbCode.Yield || code == EvbCode.None;
    }

    private State Snapshot()
    {
        uint[] rect = _lit ? _rect : new uint[5];

        return new State(Ended() ? -1 : _frame, _jump, _pause, _resume, _label, _cursor, _held, _lit,
            rect[0], rect[1], rect[2], rect[3], rect[4], _ramp, _target, _left, _step, _take);
    }

    private void Tick()
    {
        if (_pause >= 1)
        {
            --_pause;
        }
        else if (_jump < 0)
        {
            ++_frame;
        }
        else
        {
            _frame = _jump;
            _jump = -1;
            _resume = 0;
        }

        if (_label >= 0)
            ++_held;

        ++_since;

        if (_left < 1)
        {
            _ramp = _target;
        }
        else
        {
            _ramp += _step;
            --_left;
        }

        _ramp = Math.Min(Math.Max(_ramp, 0.0), Full);

        if (_pause < 1)
            Interpret();

        Walk();
    }

    private void Interpret()
    {
        long at = _resume;
        bool live = false;
        long opened = 0;
        long chosen = -1;
        int rolling = Free;

        for (; at < _record.Length; ++at)
        {
            uint[] fields = _record[at];
            uint code = fields[0];

            if (code == EvbCode.Yield || code == EvbCode.None)
                break;

            if (code == EvbCode.Wait && _frame < fields[1])
                break;

            if (code == EvbCode.Wait)
            {
                if (_frame != fields[1])
                    continue;

                live = true;
                opened = fields[1];
            }
            else if (code == EvbCode.Close)
            {
                live = false;
            }
            else if (code == EvbCode.Random)
            {
                rolling = Roll(rolling, fields[1]);
            }
            else if (code == EvbCode.RandomEnd)
            {
                rolling = Free;
            }
            else if (live && (rolling == Free || rolling == Taken))
            {
                chosen = Command(fields, opened, chosen);
            }
        }

        _resume = at;

        if (!_labels.TryGetValue(chosen, out long cursor))
            return;

        _label = chosen;
        _cursor = cursor;
    }

    private int Roll(int rolling, uint percent)
    {
        if (rolling == Taken || rolling == Done)
            return Done;

        Rolled = true;
        _dice = unchecked(_dice * LcgMultiply + LcgAdd);

        return ((_dice >> 16) & 0x7fffu) % 100u < percent ? Taken : Skipped;
    }

    private static long Signed(uint value) => unchecked((int)value);

    private long Command(uint[] fields, long opened, long chosen)
    {
        uint code = fields[0];

        if (code == EvbCode.Group)
        {
            _held = _frame - opened;
            return fields[1];
        }

        if (code == EvbCode.Pick)
        {
            _take = Signed(fields[1]);
            _since = 0;
        }
        else if (code == EvbCode.Jump)
        {
            _jump = Signed(fields[1]);
        }
        else if (code == EvbCode.Ramp)
        {
            _target = fields[1];
            _left = Signed(fields[2]) == 0 ? 1 : Signed(fields[2]);
            _step = (_target - _ramp) / _left;
        }
        else if (code == EvbCode.Pause)
        {
            _pause = Signed(fields[1]);
        }

        return chosen;
    }

    private void Walk()
    {
        if (_label < 0)
            return;

        long start = _held;
        long at = _cursor;
        long count = _record.Length;

        for (long guard = 0; guard < 2 * count; ++guard, ++at)
        {
            if (at < 0 || at >= count || _record[at][0] == EvbCode.None)
                return;

            uint[] fields = _record[at];

            if (fields[0] == EvbCode.Rect && _held < fields[1])
            {
                _lit = true;
                Array.Copy(fields, 2, _rect, 0, 5);
                _cursor = at;
                return;
            }

            if (fields[0] == EvbCode.Rect)
            {
                _held -= fields[1];
                continue;
            }

            if (fields[0] != EvbCode.End)
                continue;

            long span = start - _held;

            if (span <= 0 || span == _held)
                return;

            if (span < _held)
                _held %= span;

            at = _labels[_label];
        }
    }

    private static void Repeating(EvbPlayed played, int first, int now)
    {
        List<EvbSample> samples = played.Sample;
        int period = now - first;
        int start = first + 1;

        while (start > 0 && samples[start - 1].SameAs(samples[start - 1 + period]))
            --start;

        played.From = 0;

        if (start == 0)
        {
            played.Sample = samples.GetRange(0, period);
            played.Cyclic = true;
            return;
        }

        if (period == 1)
        {
            played.Sample = samples.GetRange(0, start + 1);
            played.Cyclic = false;
            played.From = start;
            return;
        }

        if (start <= period)
        {
            List<EvbSample> steady = [];

            for (int tick = 0; tick < period; ++tick)
            {
                int lift = tick < start ? (start - tick + period - 1) / period : 0;
                steady.Add(samples[tick + period * lift]);
            }

            played.Sample = steady;
            played.Cyclic = true;
            return;
        }

        played.Sample = samples.GetRange(0, start + period);
        played.Cyclic = false;
        played.From = start;
    }
}
