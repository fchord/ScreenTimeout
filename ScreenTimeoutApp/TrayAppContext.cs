using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace ScreenTimeoutApp
{
    internal sealed class TrayAppContext : ApplicationContext
    {
        private readonly NotifyIcon _notifyIcon;
        private readonly ContextMenuStrip _menu;
        private readonly List<(TimeOption Option, ToolStripMenuItem Item)> _timeItems;
        private readonly Mutex _singleInstanceMutex;
        // 旧的文件加载字段已移除，使用嵌入资源图标
        private readonly System.Threading.SynchronizationContext _syncContext;
        // 用于接收电源设置变更通知的消息窗体和句柄
        private MessageWindow _messageWindow;
        private IntPtr _powerNotifyHandle = IntPtr.Zero;
        // 记录上次已知的 AC 值，避免重复刷新
        private uint _lastKnownAcSeconds = uint.MaxValue;
        // 当前动态插入的自定义项（若存在）
        private TimeOption _customOption;
        private ToolStripMenuItem _customItem;

        public TrayAppContext()
        {
            _singleInstanceMutex = new Mutex(initiallyOwned: true, name: @"Global\ScreenTimeoutApp_SingleInstance", createdNew: out var createdNew);
            if (!createdNew)
            {
                MessageBox.Show("程序已经在运行中（托盘）。", "ScreenTimeout", MessageBoxButtons.OK, MessageBoxIcon.Information);
                Environment.Exit(0);
            }

            _menu = new ContextMenuStrip();
            _timeItems = new List<(TimeOption, ToolStripMenuItem)>();

            BuildMenu();

            // 使用嵌入资源中的 Icon（Resources.resx 中的 Logo），回退到 SystemIcons.Application
            Icon iconToUse = SystemIcons.Application;
            try
            { 
                var resIcon = Properties.Resources.ResourceManager.GetObject("Logo", Properties.Resources.Culture) as Icon;
                if (resIcon != null)
                {
                    iconToUse = resIcon;
                }
            }
            catch
            {
                iconToUse = SystemIcons.Application;
            }

            // 如果资源中未能正确取得图标，尝试从可执行文件中提取（应用程序图标）作为回退
            if (iconToUse == SystemIcons.Application)
            {
                try
                {
                    var exePath = Application.ExecutablePath;
                    var exeIcon = Icon.ExtractAssociatedIcon(exePath);
                    if (exeIcon != null)
                        iconToUse = exeIcon;
                }
                catch
                {
                    // ignore
                }
            }

            _notifyIcon = new NotifyIcon
            {
                Icon = iconToUse,
                Text = "ScreenTimeout - 息屏时间",
                Visible = true,
                ContextMenuStrip = _menu
            };

            // 确保托盘图标在系统托盘中刷新（某些情况下首次设置可能未立即生效）
            try
            {
                _notifyIcon.Icon = iconToUse;
                // 通过切换 Visible 强制刷新托盘显示（短暂隐藏/显示）
                _notifyIcon.Visible = false;
                _notifyIcon.Visible = true;
            }
            catch
            {
                // 忽略刷新错误
            }

            // 左键点击也弹出菜单（可选体验）
            _notifyIcon.MouseUp += (_, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    typeof(NotifyIcon).GetMethod("ShowContextMenu", BindingFlags.Instance | BindingFlags.NonPublic)
                        ?.Invoke(_notifyIcon, null);
                }
            };

            RefreshCheckedStateFromSystem();

            // 保存当前 UI 线程的 SynchronizationContext，用于从消息窗口切换回 UI 线程更新界面
            _syncContext = System.Threading.SynchronizationContext.Current;

            // 创建消息窗口并注册电源设置通知
            try
            {
                _messageWindow = new MessageWindow(this);
            }
            catch
            {
                // 注册失败不阻止应用启动
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                _menu.Dispose();
                // 现在使用资源图标，不需要手动释放通过 GetHicon 创建的句柄

                _singleInstanceMutex.ReleaseMutex();
                _singleInstanceMutex.Dispose();

                // 注销电源通知并销毁消息窗体
                try
                {
                    if (_messageWindow != null)
                    {
                        _messageWindow.Dispose();
                        _messageWindow = null;
                    }
                }
                catch
                {
                }
            }

            base.Dispose(disposing);
        }

        private void BuildMenu()
        {
            var options = GetTimeOptions();

            foreach (var opt in options)
            {
                var item = new ToolStripMenuItem(opt.Label)
                {
                    Checked = false,
                    CheckOnClick = false
                };

                item.Click += (_, __) => ApplyOption(opt);
                _menu.Items.Add(item);
                _timeItems.Add((opt, item));
            }

            _menu.Items.Add(new ToolStripSeparator());

            var about = new ToolStripMenuItem("关于");
            about.Click += (_, __) => ShowAbout();
            _menu.Items.Add(about);

            var exit = new ToolStripMenuItem("退出");
            exit.Click += (_, __) => Exit();
            _menu.Items.Add(exit);
        }

        private static IReadOnlyList<TimeOption> GetTimeOptions()
        {
            return new[]
            {
                new TimeOption("1 分钟", 1),
                new TimeOption("2 分钟", 2),
                new TimeOption("3 分钟", 3),
                new TimeOption("5 分钟", 5),
                new TimeOption("10 分钟", 10),
                new TimeOption("15 分钟", 15),
                new TimeOption("20 分钟", 20),
                new TimeOption("25 分钟", 25),
                new TimeOption("30 分钟", 30),
                new TimeOption("45 分钟", 45),
                new TimeOption("1 小时", 60),
                new TimeOption("2 小时", 120),
                new TimeOption("3 小时", 180),
                new TimeOption("4 小时", 240),
                new TimeOption("5 小时", 300),
                new TimeOption("从不", null),
            };
        }

        private void ApplyOption(TimeOption opt)
        {
            try
            {
                PowerSettingsManager.SetAcMonitorTimeoutSeconds(opt.ToSeconds());
                RefreshCheckedStateFromSystem();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"设置失败：{ex.Message}\r\n\r\n你可以尝试以管理员身份运行。",
                    "ScreenTimeout",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void RefreshCheckedStateFromSystem()
        {
            try
            {
                var seconds = PowerSettingsManager.GetAcMonitorTimeoutSeconds();

                // 使用统一的更新逻辑（会处理自定义项创建/删除）
                UpdateMenuForSeconds(seconds);

                // 记录为已知的当前 AC 值，避免后续通知重复处理
                _lastKnownAcSeconds = seconds;
            }
            catch
            {
                // 读取失败时不弹窗，避免启动打扰；勾选保持不变
            }
        }

        private void ShowAbout()
        {
            var ver = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";
            MessageBox.Show(
                $"ScreenTimeout\r\n版本：{ver}\r\n\r\n用于快速设置：插电时关闭屏幕超时（息屏时间）。",
                "关于",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private void Exit()
        {
            ExitThread();
        }

        protected override void ExitThreadCore()
        {
            _notifyIcon.Visible = false;
            base.ExitThreadCore();
        }

        private void OnPowerSettingChangedNotification()
        {
            try
            {
                // 读取 AC 与 DC 值以确认变化属于 AC
                var acSeconds = PowerSettingsManager.GetAcMonitorTimeoutSeconds();
                var dcSeconds = PowerSettingsManager.GetDcMonitorTimeoutSeconds();

                // 只有当 AC 值与上次不同才更新 UI（以 AC 为准）
                if (acSeconds == _lastKnownAcSeconds)
                    return;

                _lastKnownAcSeconds = acSeconds;
                UpdateMenuForSeconds(acSeconds);
            }
            catch
            {
                // 忽略读取失败，避免影响 UI
            }
        }

        private void UpdateMenuForSeconds(uint seconds)
        {
            // 取消所有勾选
                foreach (var tuple in _timeItems)
                    tuple.Item.Checked = false;

            // 查找是否为已知项
            var matched = _timeItems.FirstOrDefault(x => x.Option.ToSeconds() == seconds);
            if (matched.Item != null)
            {
                matched.Item.Checked = true;
                // 如果之前存在自定义项且与已知项不同，则移除自定义项
                RemoveCustomItemIfExists();
                // 更新托盘提示
                _notifyIcon.Text = seconds == 0 ? "ScreenTimeout - 当前：从不" : $"ScreenTimeout - 当前：{FormatSecondsLabel(seconds)}";
                return;
            }

            // 非预设值，生成自定义条目并插入到 "5 小时" 与 "从不" 之间
            var label = seconds == 0 ? "从不" : FormatSecondsLabel(seconds);

            // 如果已有自定义项且秒数相同，则仅勾选
            if (_customOption != null && _customOption.SecondsOverride.HasValue && _customOption.SecondsOverride.Value == seconds)
            {
                if (_customItem != null)
                {
                    _customItem.Checked = true;
                }
                _notifyIcon.Text = $"ScreenTimeout - 当前：{label}";
                return;
            }

            // 移除旧自定义项
            RemoveCustomItemIfExists();

            // 创建新自定义项
            var custom = new TimeOption(label, null, seconds);
            var customMenuItem = new ToolStripMenuItem(label) { Checked = true, CheckOnClick = false };
            customMenuItem.Click += (_, __) => ApplyOption(custom);

            // 寻找 5 小时 项的位置
            var idx = _timeItems.FindIndex(x => x.Option.Minutes.HasValue && x.Option.Minutes.Value == 300);
            var insertIndex = idx >= 0 ? idx + 1 : _timeItems.Count;

            // 插入到菜单和内部列表

            _menu.Items.Insert(insertIndex, customMenuItem);
            _timeItems.Insert(insertIndex, (custom, customMenuItem));

            _customOption = custom;
            _customItem = customMenuItem;

            _notifyIcon.Text = $"ScreenTimeout - 当前：{label}";
        }

        private void RemoveCustomItemIfExists()
        {
            if (_customItem != null)
            {
                try
                {
                    _menu.Items.Remove(_customItem);
                }
                catch
                {
                }
                _timeItems.RemoveAll(x => x.Item == _customItem);
                _customItem = null;
                _customOption = null;
            }
        }

        private static string FormatSecondsLabel(uint seconds)
        {
            if (seconds == 0) return "从不";
            var s = seconds % 60;
            var totalMinutes = seconds / 60;
            var m = totalMinutes % 60;
            var h = totalMinutes / 60;
            var parts = new List<string>();
            if (h > 0) parts.Add($"{h} 小时");
            if (m > 0) parts.Add($"{m} 分钟");
            if (s > 0) parts.Add($"{s} 秒");
            return string.Join(string.Empty, parts);
        }

        // 隐藏的消息窗口，用于接收 RegisterPowerSettingNotification 的回调
        private sealed class MessageWindow : NativeWindow, IDisposable
        {
            private readonly TrayAppContext _parent;
            private IntPtr _regHandle = IntPtr.Zero;
            private static readonly Guid GuidVideoIdleTimeout = new Guid("3c0bc021-c8a8-4e07-a973-6b14cbcb2b7e");
            private const int WM_POWERBROADCAST = 0x0218;
            private const int PBT_POWERSETTINGCHANGE = 0x8013;
            private const int DEVICE_NOTIFY_WINDOW_HANDLE = 0x00000000;

            public MessageWindow(TrayAppContext parent)
            {
                _parent = parent ?? throw new ArgumentNullException(nameof(parent));
                CreateHandle(new CreateParams());
                try
                {
                    // RegisterPowerSettingNotification 的 GUID 参数不能使用 ref 对静态只读字段，复制到局部变量
                    var localGuid = GuidVideoIdleTimeout;
                    _regHandle = RegisterPowerSettingNotification(this.Handle, ref localGuid, DEVICE_NOTIFY_WINDOW_HANDLE);
                }
                catch
                {
                    _regHandle = IntPtr.Zero;
                }
            }

            protected override void WndProc(ref Message m)
            {
                if (m.Msg == WM_POWERBROADCAST && m.WParam.ToInt32() == PBT_POWERSETTINGCHANGE)
                {
                    try
                    {
                        // 前 16 字节为 GUID
                        var changedGuid = (Guid)Marshal.PtrToStructure(m.LParam, typeof(Guid));
                        if (changedGuid == GuidVideoIdleTimeout)
                        {
                            // 在 UI 线程调用父级更新逻辑
                            if (_parent._syncContext != null)
                            {
                                _parent._syncContext.Post(_ => _parent.OnPowerSettingChangedNotification(), null);
                            }
                            else
                            {
                                // 退回到线程池（可能不是 UI 线程，但在正常 WinForms app 中 syncContext 不应为 null）
                                System.Threading.ThreadPool.QueueUserWorkItem(_ => _parent.OnPowerSettingChangedNotification());
                            }
                        }
                    }
                    catch
                    {
                    }
                }

                base.WndProc(ref m);
            }

            public void Dispose()
            {
                try
                {
                    if (_regHandle != IntPtr.Zero)
                    {
                        UnregisterPowerSettingNotification(_regHandle);
                        _regHandle = IntPtr.Zero;
                    }
                }
                catch
                {
                }

                try
                {
                    if (this.Handle != IntPtr.Zero)
                        DestroyHandle();
                }
                catch
                {
                }
            }

            [DllImport("user32.dll", SetLastError = true)]
            private static extern IntPtr RegisterPowerSettingNotification(IntPtr hRecipient, ref Guid PowerSettingGuid, int Flags);

            [DllImport("user32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            private static extern bool UnregisterPowerSettingNotification(IntPtr Handle);
        }

        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);
    }
}

