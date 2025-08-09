namespace ClipTypr.Plugins;

using Lua;
using Lua.Standard;

public sealed class LuaPlugin : IPlugin
{
    public const string FileExtension = ".lua";

    public string ScriptPath { get; }

    public LuaPlugin(string scriptPath) => ScriptPath = scriptPath;

    public PluginResult Execute(string filepath)
    {
        if (!File.Exists(ScriptPath))
        {
            return new PluginResult
            {
                Error = new ScriptException("The original file does not exist"),
                FilePath = null
            };
        }

        if (!Path.GetExtension(ScriptPath).Equals(FileExtension, StringComparison.OrdinalIgnoreCase))
        {
            return new PluginResult
            {
                Error = new ScriptException($"The script is not a valid lua file ({FileExtension})"),
                FilePath = null
            };
        }

        var cts = new CancellationTokenSource();
        try
        {

            var state = LuaState.Create();
            state.OpenStandardLibraries();

            var scriptTask = state.DoFileAsync(ScriptPath, cts.Token);

            Span<LuaValue> luaReturnValues;
            if (scriptTask.IsCompleted) luaReturnValues = scriptTask.Result;
            else
            {
                cts.CancelAfter(TimeSpan.FromSeconds(10));
                luaReturnValues = scriptTask.AsTask().GetAwaiter().GetResult();
            }

            if (!luaReturnValues[0].TryRead<LuaFunction>(out var luaFunction))
            {
                return new PluginResult
                {
                    Error = new ScriptException("The script didn't contain a function"),
                    FilePath = null
                };
            }

            if (!cts.TryReset())
            {
                cts.Dispose();
                cts = new CancellationTokenSource();
            }

            var functionTask = luaFunction.InvokeAsync(state, [filepath], cts.Token);
            if (functionTask.IsCompleted) luaReturnValues = functionTask.Result;
            else
            {
                cts.CancelAfter(TimeSpan.FromSeconds(10));
                luaReturnValues = functionTask.AsTask().GetAwaiter().GetResult();
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
        catch (OperationCanceledException)
        {
            return new PluginResult
            {
                Error = new ScriptException("The script took too long to execute"),
                FilePath = null
            };
        }
        catch (LuaException ex)
        {
            return new PluginResult
            {
                Error = new ScriptException("Something went wrong inside the script", ex),
                FilePath = null
            };
        }
        finally
        {
            cts.Dispose();
        }
    }

    public override string ToString() => ScriptPath;
}