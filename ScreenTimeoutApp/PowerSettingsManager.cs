using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace ScreenTimeoutApp
{
    internal static class PowerSettingsManager
    {
        // GUID_VIDEO_SUBGROUP: Display (Video)
        private static readonly Guid GuidVideoSubgroup = new Guid("7516b95f-f776-4464-8c53-06167f40cc99");
        // GUID_VIDEO_POWERDOWN_TIMEOUT: Turn off display after (Video idle timeout)
        private static readonly Guid GuidVideoIdleTimeout = new Guid("3c0bc021-c8a8-4e07-a973-6b14cbcb2b7e");

        /// <summary>
        /// 读取当前活动电源计划下“插电(AC)关闭显示器超时”，单位：秒。
        /// 0 表示“从不”。
        /// </summary>
        public static uint GetAcMonitorTimeoutSeconds()
        {
            var schemeGuid = GetActiveSchemeGuid();
            var subgroup = GuidVideoSubgroup;
            var setting = GuidVideoIdleTimeout;
            uint value;
            var res = PowerReadACValueIndex(
                IntPtr.Zero,
                ref schemeGuid,
                ref subgroup,
                ref setting,
                out value);
            ThrowIfFailed(res, "PowerReadACValueIndex");
            return value;
        }

        /// <summary>
        /// 读取当前活动电源计划下“电池(DC)关闭显示器超时”，单位：秒。
        /// 0 表示“从不”。
        /// </summary>
        public static uint GetDcMonitorTimeoutSeconds()
        {
            var schemeGuid = GetActiveSchemeGuid();
            var subgroup = GuidVideoSubgroup;
            var setting = GuidVideoIdleTimeout;
            uint value;
            var res = PowerReadDCValueIndex(
                IntPtr.Zero,
                ref schemeGuid,
                ref subgroup,
                ref setting,
                out value);
            ThrowIfFailed(res, "PowerReadDCValueIndex");
            return value;
        }

        /// <summary>
        /// 设置当前活动电源计划下“插电(AC)关闭显示器超时”，单位：秒。
        /// 0 表示“从不”。
        /// </summary>
        public static void SetAcMonitorTimeoutSeconds(uint seconds)
        {
            var schemeGuid = GetActiveSchemeGuid();
            var subgroup = GuidVideoSubgroup;
            var setting = GuidVideoIdleTimeout;
            var res = PowerWriteACValueIndex(
                IntPtr.Zero,
                ref schemeGuid,
                ref subgroup,
                ref setting,
                seconds);
            ThrowIfFailed(res, "PowerWriteACValueIndex");

            // 使改动立即生效
            res = PowerSetActiveScheme(IntPtr.Zero, ref schemeGuid);
            ThrowIfFailed(res, "PowerSetActiveScheme");
        }

        private static Guid GetActiveSchemeGuid()
        {
            IntPtr pActiveGuid;
            var res = PowerGetActiveScheme(IntPtr.Zero, out pActiveGuid);
            ThrowIfFailed(res, "PowerGetActiveScheme");

            try
            {
                if (pActiveGuid == IntPtr.Zero)
                    throw new Win32Exception("PowerGetActiveScheme returned NULL pointer.");

                return (Guid)Marshal.PtrToStructure(pActiveGuid, typeof(Guid));
            }
            finally
            {
                if (pActiveGuid != IntPtr.Zero)
                {
                    LocalFree(pActiveGuid);
                }
            }
        }

        private static void ThrowIfFailed(uint win32Error, string apiName)
        {
            // powrprof APIs return ERROR_SUCCESS (0) on success
            if (win32Error == 0) return;
            throw new Win32Exception(unchecked((int)win32Error), $"{apiName} failed.");
        }

        [DllImport("powrprof.dll", SetLastError = true)]
        private static extern uint PowerGetActiveScheme(IntPtr UserRootPowerKey, out IntPtr ActivePolicyGuid);

        [DllImport("powrprof.dll", SetLastError = true)]
        private static extern uint PowerReadACValueIndex(
            IntPtr RootPowerKey,
            ref Guid SchemeGuid,
            ref Guid SubGroupOfPowerSettingsGuid,
            ref Guid PowerSettingGuid,
            out uint AcValueIndex);

        [DllImport("powrprof.dll", SetLastError = true)]
        private static extern uint PowerReadDCValueIndex(
            IntPtr RootPowerKey,
            ref Guid SchemeGuid,
            ref Guid SubGroupOfPowerSettingsGuid,
            ref Guid PowerSettingGuid,
            out uint DcValueIndex);

        [DllImport("powrprof.dll", SetLastError = true)]
        private static extern uint PowerWriteACValueIndex(
            IntPtr RootPowerKey,
            ref Guid SchemeGuid,
            ref Guid SubGroupOfPowerSettingsGuid,
            ref Guid PowerSettingGuid,
            uint AcValueIndex);

        [DllImport("powrprof.dll", SetLastError = true)]
        private static extern uint PowerSetActiveScheme(IntPtr UserRootPowerKey, ref Guid SchemeGuid);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr LocalFree(IntPtr hMem);
    }
}

