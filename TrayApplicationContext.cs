using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using IWshRuntimeLibrary;

namespace TaskbarAutoHider
{
    public class TrayApplicationContext : ApplicationContext, IDisposable
    {
        /* ───────────────────── Constants ───────────────────── */
        private const int TIMER_CHECK_INTERVAL = 1000;     // 1 second
        private const int USER_ACTIVE_THRESHOLD = 5;       // 5 seconds
        private const int DEFAULT_TIMEOUT_SECONDS = 30;   // 30 seconds
        private const int BALLOON_TIP_DURATION = 2000;     // 2 seconds

        /* ───────────────────── Fields ───────────────────── */
        private NotifyIcon trayIcon;
        private System.Windows.Forms.Timer idleTimer;
        private SettingsForm settingsForm;
        private readonly ToolStripMenuItem startWithWindowsMenuItem;

        private int idleTimeoutSeconds = DEFAULT_TIMEOUT_SECONDS;
        private bool taskbarCurrentlyHidden = false;
        private bool manuallyHidden = false; // Track manual override
        private bool disposed = false;

        /* ───────────────────── Constructor ───────────────────── */
        public TrayApplicationContext()
        {
            startWithWindowsMenuItem = new ToolStripMenuItem("Start with Windows")
            {
                CheckOnClick = true,
                Checked = IsStartupShortcutPresent()
            };
            startWithWindowsMenuItem.CheckedChanged += StartWithWindowsMenuItem_CheckedChanged;

            InitializeTrayIcon();
            InitializeTimer();
        }

        /* ───────────────────── Tray Icon / Menu ───────────────────── */
        private void InitializeTrayIcon()
        {
            // Load custom icon from embedded resource
            Icon customIcon = LoadIconFromResource("taskbar_icon.ico");

            trayIcon = new NotifyIcon
            {
                Icon = customIcon ?? SystemIcons.Application, // Fallback to system icon if loading fails
                Text = "Taskbar Auto-Hider",
                Visible = true,
                ContextMenuStrip = new ContextMenuStrip()
            };

            var m = trayIcon.ContextMenuStrip.Items;
            m.Add(startWithWindowsMenuItem);
            m.Add("Settings", null, ShowSettings);
            m.Add("Show Taskbar", null, ShowTaskbar);
            m.Add("Hide Taskbar", null, HideTaskbar);
            m.Add("-");
            m.Add("Exit", null, Exit);

            trayIcon.DoubleClick += ShowSettings;
        }

        private static Icon LoadIconFromResource(string iconFileName)
        {
            try
            {
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                var resourceName = $"TaskbarAutoHider.{iconFileName}";

                using (var stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream != null)
                    {
                        return new Icon(stream);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading icon: {ex.Message}");
            }

            return null; // Return null if loading fails, will use fallback
        }

        /* ───────────────────── Idle Timer ───────────────────── */
        private void InitializeTimer()
        {
            idleTimer?.Dispose(); // Clean up any existing timer
            idleTimer = new System.Windows.Forms.Timer();
            idleTimer.Interval = TIMER_CHECK_INTERVAL;
            idleTimer.Tick += CheckIdleTime;
            idleTimer.Start();
        }

        private void CheckIdleTime(object sender, EventArgs e)
        {
            var idle = GetIdleTime();

            // Only auto-hide if not manually controlled
            if (idle.TotalSeconds >= idleTimeoutSeconds && !taskbarCurrentlyHidden && !manuallyHidden)
            {
                SetTaskbarAutoHide(true);
                taskbarCurrentlyHidden = true;
                ShowBalloonTipSafe("Taskbar Auto-Hider",
                    $"Taskbar auto-hidden after {idleTimeoutSeconds} s of inactivity",
                    ToolTipIcon.Info);
            }
            else if (idle.TotalSeconds < USER_ACTIVE_THRESHOLD && taskbarCurrentlyHidden && !manuallyHidden)
            {
                // Only auto-show if it was auto-hidden (not manually hidden)
                SetTaskbarAutoHide(false);
                taskbarCurrentlyHidden = false;
            }
        }

        /* ───────────────────── Settings Form ───────────────────── */
        private void ShowSettings(object sender, EventArgs e)
        {
            if (settingsForm == null || settingsForm.IsDisposed)
            {
                settingsForm = new SettingsForm(idleTimeoutSeconds);
                settingsForm.TimeoutChanged += SetIdleTimeout;
            }

            settingsForm.Show();
            settingsForm.BringToFront();
        }

        public void SetIdleTimeout(int seconds)
        {
            if (seconds < 5 || seconds > 3600)
                throw new ArgumentOutOfRangeException(nameof(seconds), "Timeout must be between 5 and 3600 seconds");

            idleTimeoutSeconds = seconds;
        }

        /* ───────────────────── Manual Taskbar Control ───────────────────── */
        private void ShowTaskbar(object sender, EventArgs e)
        {
            SetTaskbarAutoHide(false);
            taskbarCurrentlyHidden = false;
            manuallyHidden = false; // Reset manual state
            ShowBalloonTipSafe("Taskbar Auto-Hider", "Taskbar shown", ToolTipIcon.Info);
        }

        private void HideTaskbar(object sender, EventArgs e)
        {
            SetTaskbarAutoHide(true);
            taskbarCurrentlyHidden = true;
            manuallyHidden = true; // Mark as manually hidden
            ShowBalloonTipSafe("Taskbar Auto-Hider", "Taskbar hidden", ToolTipIcon.Info);
        }

        /* ───────────────────── Thread-Safe UI Operations ───────────────────── */
        private void ShowBalloonTipSafe(string title, string text, ToolTipIcon icon)
        {
            try
            {
                if (trayIcon != null && !disposed)
                {
                    trayIcon.ShowBalloonTip(BALLOON_TIP_DURATION, title, text, icon);
                }
            }
            catch (Exception ex)
            {
                // Log error but don't crash the application
                System.Diagnostics.Debug.WriteLine($"Error showing balloon tip: {ex.Message}");
            }
        }

        /* ───────────────────── Exit and Disposal ───────────────────── */
        private void Exit(object sender, EventArgs e)
        {
            Dispose();
            Application.Exit();
        }

        protected override void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    // Restore taskbar state before disposing
                    try
                    {
                        SetTaskbarAutoHide(false);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error restoring taskbar on exit: {ex.Message}");
                    }

                    // Dispose of custom icon if it's not a system icon
                    if (trayIcon?.Icon != null && trayIcon.Icon != SystemIcons.Application)
                    {
                        trayIcon.Icon.Dispose();
                    }

                    idleTimer?.Stop();
                    idleTimer?.Dispose();
                    trayIcon?.Dispose();
                    settingsForm?.Dispose();
                }
                disposed = true;
            }
            base.Dispose(disposing);
        }

        /* ───────────────────── Start-with-Windows Handling ───────────────────── */
        private void StartWithWindowsMenuItem_CheckedChanged(object sender, EventArgs e)
        {
            try
            {
                if (startWithWindowsMenuItem.Checked)
                {
                    CreateStartupShortcut();
                    ShowBalloonTipSafe("Taskbar Auto-Hider", "Enabled start with Windows", ToolTipIcon.Info);
                }
                else
                {
                    RemoveStartupShortcut();
                    ShowBalloonTipSafe("Taskbar Auto-Hider", "Disabled start with Windows", ToolTipIcon.Info);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Startup-option error:\n{ex.Message}", "Error",
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
                // Revert checkbox so UI matches reality
                startWithWindowsMenuItem.CheckedChanged -= StartWithWindowsMenuItem_CheckedChanged;
                startWithWindowsMenuItem.Checked = !startWithWindowsMenuItem.Checked;
                startWithWindowsMenuItem.CheckedChanged += StartWithWindowsMenuItem_CheckedChanged;
            }
        }

        /* ───────────────────── Shortcut Helpers ───────────────────── */
        private static string StartupFolderPath =>
            Environment.GetFolderPath(Environment.SpecialFolder.Startup);

        private static string ShortcutPath =>
            Path.Combine(StartupFolderPath, "TaskbarAutoHider.lnk");

        private static bool IsStartupShortcutPresent() =>
            System.IO.File.Exists(ShortcutPath);

        private static void CreateStartupShortcut()
        {
            try
            {
                if (System.IO.File.Exists(ShortcutPath)) return;

                var startupDir = Path.GetDirectoryName(ShortcutPath);
                if (!Directory.Exists(startupDir))
                {
                    Directory.CreateDirectory(startupDir);
                }

                var shell = new WshShell();
                var link = (IWshShortcut)shell.CreateShortcut(ShortcutPath);
                string exe = Application.ExecutablePath;

                link.TargetPath = exe;
                link.WorkingDirectory = Path.GetDirectoryName(exe) ?? "";
                link.Description = "Taskbar Auto-Hider";
                link.Save();

                // Release COM objects
                Marshal.ReleaseComObject(link);
                Marshal.ReleaseComObject(shell);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to create startup shortcut: {ex.Message}", ex);
            }
        }

        private static void RemoveStartupShortcut()
        {
            try
            {
                if (System.IO.File.Exists(ShortcutPath))
                    System.IO.File.Delete(ShortcutPath);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to remove startup shortcut: {ex.Message}", ex);
            }
        }

        /* ───────────────────── Idle-Time Detection (WinAPI) ───────────────────── */
        [DllImport("user32.dll")]
        private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

        [StructLayout(LayoutKind.Sequential)]
        private struct LASTINPUTINFO
        {
            public uint cbSize;
            public uint dwTime;
        }

        private static TimeSpan GetIdleTime()
        {
            try
            {
                var lii = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };

                if (GetLastInputInfo(ref lii))
                {
                    uint elapsed = (uint)Environment.TickCount - lii.dwTime;
                    return TimeSpan.FromMilliseconds(elapsed);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting idle time: {ex.Message}");
            }

            return TimeSpan.Zero;
        }

        /* ───────────────────── Taskbar Show/Hide (Shell API) ───────────────────── */
        [DllImport("shell32.dll")]
        private static extern IntPtr SHAppBarMessage(uint msg, ref APPBARDATA data);

        [StructLayout(LayoutKind.Sequential)]
        private struct APPBARDATA
        {
            public uint cbSize;
            public IntPtr hWnd;
            public uint uCallbackMessage;
            public uint uEdge;
            public RECT rc;
            public int lParam;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int left, top, right, bottom; }

        private const uint ABM_SETSTATE = 0x0000000A;
        private const int ABS_AUTOHIDE = 0x1;
        private const int ABS_ALWAYSONTOP = 0x2;

        private static void SetTaskbarAutoHide(bool hide)
        {
            try
            {
                var taskbarHandle = FindWindow("Shell_TrayWnd", null);
                if (taskbarHandle == IntPtr.Zero)
                {
                    throw new InvalidOperationException("Could not find taskbar window");
                }

                var data = new APPBARDATA
                {
                    cbSize = (uint)Marshal.SizeOf<APPBARDATA>(),
                    hWnd = taskbarHandle,
                    lParam = hide ? ABS_AUTOHIDE : ABS_ALWAYSONTOP
                };

                var result = SHAppBarMessage(ABM_SETSTATE, ref data);
                if (result == IntPtr.Zero)
                {
                    throw new InvalidOperationException("Failed to set taskbar state");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error controlling taskbar: {ex.Message}", "Taskbar Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        [DllImport("user32.dll")]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
    }
}
