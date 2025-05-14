namespace StrokeMyKeys.Common;

using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Sequential)]
[SkipLocalsInit]
public ref struct SpanBuilder : IDisposable
{
    private int currentPos;
    private Span<char> buffer;
    private char[]? arrayFromPool;

    public int Length
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        readonly get => currentPos;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => currentPos = value;
    }

    public readonly int Capacity
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => buffer.Length;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SpanBuilder(scoped in ReadOnlySpan<char> initialText, int additionalCapacity = 0)
    {
        EnsureCapacity(Capacity + additionalCapacity);
        Append(initialText);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SpanBuilder(int capacity) => EnsureCapacity(capacity);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SpanBuilder() => EnsureCapacity(32);

    public readonly ref char this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref buffer[index];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Append(char value)
    {
        var newSize = currentPos + 1;
        if (newSize > buffer.Length) EnsureCapacity(newSize);

        buffer[currentPos] = value;
        currentPos++;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Append(scoped in ReadOnlySpan<char> text)
    {
        if (text.IsEmpty) return;

        var newSize = text.Length + currentPos;
        if (newSize > buffer.Length) EnsureCapacity(newSize);

        ref var strRef = ref MemoryMarshal.GetReference(text);
        ref var bufferRef = ref MemoryMarshal.GetReference(buffer[currentPos..]);
        Unsafe.CopyBlock(
            ref Unsafe.As<char, byte>(ref bufferRef),
            ref Unsafe.As<char, byte>(ref strRef),
            (uint)(text.Length * sizeof(char)));
        
        currentPos += text.Length;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AppendLine() => Append(Environment.NewLine);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void EnsureCapacity(int newCapacity)
    {
        if (Capacity >= newCapacity) return;

        var newSize = (int)BitOperations.RoundUpToPowerOf2((uint)newCapacity);

        var rented = ArrayPool<char>.Shared.Rent(newSize);

        if (currentPos > 0)
        {
            ref var sourceRef = ref MemoryMarshal.GetReference(buffer);
            ref var destinationRef = ref MemoryMarshal.GetReference(rented.AsSpan());

            Unsafe.CopyBlock(
                ref Unsafe.As<char, byte>(ref destinationRef),
                ref Unsafe.As<char, byte>(ref sourceRef),
                (uint)currentPos * sizeof(char));
        }

        if (arrayFromPool is not null)
            ArrayPool<char>.Shared.Return(arrayFromPool);

        buffer = rented;
        arrayFromPool = rented;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly ReadOnlySpan<char> AsSpan() => buffer[..currentPos];

    public void Dispose()
    {
        if (arrayFromPool is not null) ArrayPool<char>.Shared.Return(arrayFromPool);

        this = default;
    }
}