namespace ClipTypr.Plugins;

public interface IPlugin
{
    public PluginResult Execute(string filepath);
}