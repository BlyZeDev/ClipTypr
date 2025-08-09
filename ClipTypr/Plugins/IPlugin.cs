namespace ClipTypr.Plugins;

public interface IPlugin
{
    public string ScriptPath { get; }

    public PluginResult Execute(string filepath);
}