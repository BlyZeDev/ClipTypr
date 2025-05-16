namespace ClipTypr.COM;

using ClipTypr.Common;
using ClipTypr.NATIVE;
using System.Runtime.InteropServices;

public static class Com
{
    public const string ShellLinkCLSID = "00021401-0000-0000-C000-000000000046";
    public const string IID_IShellLinkW = "000214F9-0000-0000-C000-000000000046";
    public const string IID_IPersistFile = "0000010b-0000-0000-C000-000000000046";

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "CA1416:Plattformkompatibilität überprüfen", Justification = "<Ausstehend>")]
    public static void CreateShortcut(string targetPath, string shortcutPath, string description)
    {
        Logger.LogDebug("Creating a STAThread for creating a .lnk");

        var staThread = new Thread(() =>
        {
            var result = Native.CoInitializeEx(nint.Zero, Native.COINIT_APARTMENTTHREADED);
            if (result < 0)
            {
                Logger.LogError("Could not initialize Ole32", Native.GetError());
                return;
            }

            var clsId = new Guid(ShellLinkCLSID);
            var shellLinkId = new Guid(IID_IShellLinkW);

            result = Native.CoCreateInstance(ref clsId, nint.Zero, Native.CLSCTX_INPROC_SERVER, ref shellLinkId, out var ppv);
            if (result != 0)
            {
                Logger.LogError("Could not initialize required COM objects", Native.GetError());
                Native.CoUninitialize();
                return;
            }

            try
            {
                var link = Marshal.GetObjectForIUnknown(ppv);
                var shellLink = (IShellLinkW)link;
                shellLink.SetPath(targetPath);
                shellLink.SetArguments("");
                shellLink.SetDescription(description);

                var file = (IPersistFile)link;
                file.Save(shortcutPath, false);
            }
            finally
            {
                if (ppv != nint.Zero) Marshal.Release(ppv);
                Native.CoUninitialize();
            }
        });

        staThread.SetApartmentState(ApartmentState.STA);
        staThread.Start();
        staThread.Join();
    }
}