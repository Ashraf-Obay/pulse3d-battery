// Pulse 3D Battery - tray icon showing the battery level of a Sony PULSE 3D wireless headset.
//
// Protocol (reverse-engineered read-only, 24-Oct-26): the USB dongle (VID 054C / PID 0D5E) sends an
// 8-byte input report on HID collection MI_03&COL04 whenever headset state changes:
//   [0] 0xB0 report id
//   [3] battery 0-100 in 10% steps, or 0x80 while charging
//   [4] bit 0x08 set = headset connected (cleared when powered off)
// Nothing is ever written to the device. Build with build.cmd (.NET Framework csc, C# 5).

using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;

static class Program
{
    [STAThread]
    static void Main()
    {
        bool created;
        using (new Mutex(true, "Pulse3DBattery.SingleInstance", out created))
        {
            if (!created) return;
            Native.SetProcessDPIAware(); // real icon size at any scaling, so Windows doesn't blur it
            Application.EnableVisualStyles();
            Application.Run(new TrayApp());
        }
    }
}

class TrayApp : ApplicationContext
{
    const int LowBattery = 20;
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string RunValue = "Pulse3DBattery";

    readonly NotifyIcon tray = new NotifyIcon();
    readonly SynchronizationContext ui;
    readonly ToolStripMenuItem startupItem;
    IntPtr iconHandle = IntPtr.Zero;

    // headset state, only touched on the UI thread
    int battery = -1;
    bool charging, headsetOn, dongleFound, lowWarned;
    DateTime lastUpdate;

    public TrayApp()
    {
        ui = new WindowsFormsSynchronizationContext();

        startupItem = new ToolStripMenuItem("Start with Windows", null, ToggleStartup);
        startupItem.Checked = StartupEnabled();
        var menu = new ContextMenuStrip();
        menu.Items.Add(startupItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, delegate { ExitThread(); });
        tray.ContextMenuStrip = menu;

        Render();
        tray.Visible = true;

        var reader = new Thread(ReadLoop);
        reader.IsBackground = true;
        reader.Start();
        Native.TrimWorkingSet();
    }

    protected override void ExitThreadCore()
    {
        tray.Visible = false;
        tray.Dispose();
        if (iconHandle != IntPtr.Zero) Native.DestroyIcon(iconHandle);
        base.ExitThreadCore();
    }

    // ---- HID reading (background thread; blocks in ReadFile, so idle CPU is 0%) ----

    void ReadLoop()
    {
        var buf = new byte[8];
        while (true)
        {
            string path = Native.FindHidPath("vid_054c&pid_0d5e&mi_03&col04");
            FileStream fs = path == null ? null : Native.OpenRead(path);
            if (fs == null)
            {
                Post(delegate { dongleFound = false; Render(); });
                Thread.Sleep(5000);
                continue;
            }
            Post(delegate { dongleFound = true; Render(); });
            try
            {
                using (fs)
                {
                    while (true)
                    {
                        int n = fs.Read(buf, 0, buf.Length);
                        if (n >= 5 && buf[0] == 0xB0)
                        {
                            byte level = buf[3], flags = buf[4];
                            Post(delegate { OnReport(level, flags); });
                        }
                    }
                }
            }
            catch (IOException) { }          // dongle unplugged: fall through and rediscover
            catch (OperationCanceledException) { }
            Thread.Sleep(1000);
        }
    }

    void Post(Action a) { ui.Post(delegate { a(); }, null); }

    void OnReport(byte level, byte flags)
    {
        headsetOn = (flags & 0x08) != 0;
        if (level == 0x80) charging = true;
        else if (level <= 100) { charging = false; battery = level; }
        lastUpdate = DateTime.Now;

        if (charging || battery > LowBattery) lowWarned = false;
        else if (headsetOn && battery >= 0 && !lowWarned)
        {
            lowWarned = true;
            tray.ShowBalloonTip(5000, "Pulse 3D battery low", "Battery at " + battery + "%. Charge the headset soon.", ToolTipIcon.Warning);
        }
        Render();
        Native.TrimWorkingSet();
    }

    // ---- tray icon ----

    void Render()
    {
        string text, tip;
        Color color;
        bool light = TaskbarIsLight();
        Color normal = light ? Color.Black : Color.White;
        Color grey = Color.FromArgb(light ? 140 : 150, 150, 150);
        string updated = lastUpdate == DateTime.MinValue ? "" : "\nUpdated " + lastUpdate.ToString("dd-MMM-yy HH:mm");

        if (!dongleFound) { text = "x"; color = grey; tip = "Pulse 3D: dongle not found"; }
        else if (battery < 0 && !charging) { text = "?"; color = grey; tip = "Pulse 3D: press a headset button to update"; }
        else
        {
            string pct = battery < 0 ? "?" : battery.ToString();
            text = pct;
            if (!headsetOn) { color = grey; tip = "Pulse 3D: off (last " + pct + "%)"; }
            else if (charging) { color = Color.FromArgb(60, 200, 90); tip = "Pulse 3D: charging (" + pct + "% before plugging in)"; }
            else
            {
                color = battery <= LowBattery ? Color.FromArgb(235, 70, 60)
                      : battery <= 30 ? Color.FromArgb(240, 170, 40) : normal;
                tip = "Pulse 3D: " + pct + "%";
            }
            tip += updated;
        }

        tray.Text = tip.Length > 63 ? tip.Substring(0, 63) : tip;
        SetIcon(text, color, charging && headsetOn && dongleFound);
    }

    void SetIcon(string text, Color color, bool chargeBar)
    {
        int size = SystemInformation.SmallIconSize.Width;
        using (var bmp = IconArt.Render(text, color, chargeBar, size))
        {
            IntPtr h = bmp.GetHicon();
            tray.Icon = Icon.FromHandle(h);
            if (iconHandle != IntPtr.Zero) Native.DestroyIcon(iconHandle);
            iconHandle = h;
        }
    }

    static bool TaskbarIsLight()
    {
        using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
        {
            object v = k == null ? null : k.GetValue("SystemUsesLightTheme");
            return v is int && (int)v == 1;
        }
    }

    // ---- start with Windows (HKCU Run key, current user only) ----

    static bool StartupEnabled()
    {
        using (var k = Registry.CurrentUser.OpenSubKey(RunKey))
            return k != null && k.GetValue(RunValue) != null;
    }

    void ToggleStartup(object sender, EventArgs e)
    {
        using (var k = Registry.CurrentUser.CreateSubKey(RunKey))
        {
            if (StartupEnabled()) k.DeleteValue(RunValue, false);
            else k.SetValue(RunValue, "\"" + Application.ExecutablePath + "\"");
        }
        startupItem.Checked = StartupEnabled();
    }
}

static class Native
{
    [DllImport("hid.dll")] static extern void HidD_GetHidGuid(out Guid guid);
    [DllImport("setupapi.dll", SetLastError = true)] static extern IntPtr SetupDiGetClassDevs(ref Guid cls, IntPtr enumerator, IntPtr parent, int flags);
    [DllImport("setupapi.dll", SetLastError = true)] static extern bool SetupDiEnumDeviceInterfaces(IntPtr set, IntPtr devInfo, ref Guid cls, int index, ref SP_DEVICE_INTERFACE_DATA data);
    [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)] static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr set, ref SP_DEVICE_INTERFACE_DATA data, IntPtr detail, int size, out int required, IntPtr devInfo);
    [DllImport("setupapi.dll")] static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)] static extern SafeFileHandle CreateFile(string name, uint access, uint share, IntPtr sec, uint disp, uint flags, IntPtr tmpl);
    [DllImport("kernel32.dll")] static extern IntPtr GetCurrentProcess();
    [DllImport("kernel32.dll")] static extern bool SetProcessWorkingSetSize(IntPtr proc, IntPtr min, IntPtr max);
    [DllImport("user32.dll")] public static extern bool DestroyIcon(IntPtr icon);
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();

    [StructLayout(LayoutKind.Sequential)]
    struct SP_DEVICE_INTERFACE_DATA { public int cbSize; public Guid InterfaceClassGuid; public int Flags; public IntPtr Reserved; }

    const int DIGCF_PRESENT = 0x2, DIGCF_DEVICEINTERFACE = 0x10;

    public static string FindHidPath(string match)
    {
        Guid hid; HidD_GetHidGuid(out hid);
        IntPtr set = SetupDiGetClassDevs(ref hid, IntPtr.Zero, IntPtr.Zero, DIGCF_PRESENT | DIGCF_DEVICEINTERFACE);
        if (set == new IntPtr(-1)) return null;
        try
        {
            var data = new SP_DEVICE_INTERFACE_DATA();
            data.cbSize = Marshal.SizeOf(data);
            for (int i = 0; SetupDiEnumDeviceInterfaces(set, IntPtr.Zero, ref hid, i, ref data); i++)
            {
                int required;
                SetupDiGetDeviceInterfaceDetail(set, ref data, IntPtr.Zero, 0, out required, IntPtr.Zero);
                IntPtr detail = Marshal.AllocHGlobal(required);
                try
                {
                    Marshal.WriteInt32(detail, IntPtr.Size == 8 ? 8 : 6); // SP_DEVICE_INTERFACE_DETAIL_DATA_W.cbSize
                    if (!SetupDiGetDeviceInterfaceDetail(set, ref data, detail, required, out required, IntPtr.Zero)) continue;
                    string path = Marshal.PtrToStringUni(new IntPtr(detail.ToInt64() + 4));
                    if (path != null && path.ToLowerInvariant().Contains(match)) return path;
                }
                finally { Marshal.FreeHGlobal(detail); }
            }
            return null;
        }
        finally { SetupDiDestroyDeviceInfoList(set); }
    }

    public static FileStream OpenRead(string path)
    {
        // GENERIC_READ, share read/write, OPEN_EXISTING, synchronous
        var h = CreateFile(path, 0x80000000, 3, IntPtr.Zero, 3, 0, IntPtr.Zero);
        if (h.IsInvalid) { h.Dispose(); return null; }
        return new FileStream(h, FileAccess.Read, 8, false);
    }

    public static void TrimWorkingSet()
    {
        SetProcessWorkingSetSize(GetCurrentProcess(), new IntPtr(-1), new IntPtr(-1));
    }
}
