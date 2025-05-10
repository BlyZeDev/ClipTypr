namespace StrokeMyKeys;

sealed class Program
{
    static void Main(string[] args)
    {
        using (var runner = ServiceRunner.Initialize(args))
        {
            runner.RunAndBlock();
        }
    }
}