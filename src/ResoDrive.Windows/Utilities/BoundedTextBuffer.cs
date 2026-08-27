namespace ResoDrive.Windows;

/// <summary>Keeps only the newest characters without repeatedly shifting retained text.</summary>
internal sealed class BoundedTextBuffer
{
    private readonly char[] _buffer;
    private int _start;
    private int _count;

    internal BoundedTextBuffer(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        _buffer = new char[capacity];
    }

    internal void Append(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Append(value.AsSpan());
    }

    internal void Append(ReadOnlySpan<char> value)
    {
        if (value.Length >= _buffer.Length)
        {
            value[^_buffer.Length..].CopyTo(_buffer);
            _start = 0;
            _count = _buffer.Length;
            return;
        }

        var discarded = Math.Max(0, _count + value.Length - _buffer.Length);
        _start = (_start + discarded) % _buffer.Length;
        _count -= discarded;

        var end = (_start + _count) % _buffer.Length;
        var firstLength = Math.Min(value.Length, _buffer.Length - end);
        value[..firstLength].CopyTo(_buffer.AsSpan(end));
        value[firstLength..].CopyTo(_buffer);
        _count += value.Length;
    }

    public override string ToString()
    {
        if (_count == 0)
            return string.Empty;

        return string.Create(_count, this, static (destination, source) =>
        {
            var firstLength = Math.Min(source._count, source._buffer.Length - source._start);
            source._buffer.AsSpan(source._start, firstLength).CopyTo(destination);
            source._buffer.AsSpan(0, source._count - firstLength).CopyTo(destination[firstLength..]);
        });
    }
}
