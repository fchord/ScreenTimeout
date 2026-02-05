namespace ScreenTimeoutApp
{
    internal sealed class TimeOption
    {
        // minutes: null 表示“从不”。
        // secondsOverride: 如果不为 null，则 ToSeconds() 返回该值（用于自定义秒数项）。
        public TimeOption(string label, int? minutes, uint? secondsOverride = null)
        {
            Label = label;
            Minutes = minutes;
            SecondsOverride = secondsOverride;
        }

        /// <summary>
        /// 菜单显示文本。
        /// </summary>
        public string Label { get; }

        /// <summary>
        /// 分钟数；null 表示“从不”。
        /// </summary>
        public int? Minutes { get; }

        /// <summary>
        /// 可选的秒数覆盖（用于自定义时长），当不为 null 时优先返回。
        /// </summary>
        public uint? SecondsOverride { get; }

        public uint ToSeconds()
        {
            if (SecondsOverride.HasValue) return SecondsOverride.Value;
            if (Minutes is null) return 0;
            var secs = Minutes.Value * 60;
            if (secs < 0) return 0;
            return (uint)secs;
        }
    }
}

