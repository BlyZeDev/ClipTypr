namespace ClipTypr.Services;

using Jab;

[ServiceProvider]
[Singleton<StartupGuard>]
[Singleton<ServiceRunner>]
[Singleton<ConsolePal>]
[Singleton<HotKeyHandler>]
[Singleton<ConfigurationHandler>]
[Singleton<ClipboardService>]
[Singleton<InputSimulator>]
[Singleton<KeyboardTranslator>]
[Singleton<ILogger, ConsoleLogger>]
public sealed partial class ServiceProvider { }