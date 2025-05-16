namespace ClipTypr.Common;

public static class Util
{
    public const int StackSizeBytes = 1024;

    public static unsafe bool AllowStack<T>(int size) where T : unmanaged
        => sizeof(T) * size <= StackSizeBytes;
}