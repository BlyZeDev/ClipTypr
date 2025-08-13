namespace ClipTypr.Services;

using Jab;

[ServiceProvider]
[Singleton<StartupGuard>]
[Singleton<ServiceRunner>]
[Singleton<ConsolePal>]
[Singleton<HotKeyHandler>]
[Singleton<ConfigurationHandler>]
[Singleton<ClipboardHandler>]
[Singleton<InputSimulator>]
[Singleton<ILoggerTarget, ConsoleLogger>]
[Singleton<ILoggerTarget, FileLogger>]
[Singleton<ILogger, LoggerForwarder>]
[Singleton<ClipTyprContext>]
[Singleton<NativeMessageHandler>]
public sealed partial class ServiceProvider { }