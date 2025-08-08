namespace ClipTypr.Plugins;

using Lua;
using Lua.Standard;

public sealed class LuaPlugin : IPlugin
{
    public const string FileExtension = ".lua";

    private readonly string _scriptPath;

    public LuaPlugin(string scriptPath) => _scriptPath = scriptPath;

    public PluginResult Execute(string filepath)
    {
        if (!File.Exists(_scriptPath))
        {
            return new PluginResult
            {
                Error = new ScriptException("The original file does not exist"),
                FilePath = null
            };
        }

        if (!Path.GetExtension(_scriptPath).Equals(FileExtension, StringComparison.OrdinalIgnoreCase))
        {
            return new PluginResult
            {
                Error = new ScriptException($"The script is not a valid lua file ({FileExtension})"),
                FilePath = null
            };
        }

        try
        {
            using (var cts = new CancellationTokenSource())
            {
                var state = LuaState.Create();
                state.OpenStandardLibraries();

                var task = state.DoFileAsync(_scriptPath, cts.Token);

                Span<LuaValue> luaReturnValues;
                if (task.IsCompleted) luaReturnValues = task.Result;
                else
                {
                    cts.CancelAfter(TimeSpan.FromSeconds(10));
                    luaReturnValues = task.AsTask().GetAwaiter().GetResult();
                }

                if (!luaReturnValues[0].TryRead<string>(out var returnValue))
                {
                    return new PluginResult
                    {
                        Error = new ScriptException("The script didn't return a value of type string"),
                        FilePath = null
                    };
                }

                return new PluginResult
                {
                    Error = null,
                    FilePath = returnValue
                };
            }
        }
        catch (OperationCanceledException)
        {
            return new PluginResult
            {
                Error = new ScriptException("The script took too long to execute"),
                FilePath = null
            };
        }
    }
}