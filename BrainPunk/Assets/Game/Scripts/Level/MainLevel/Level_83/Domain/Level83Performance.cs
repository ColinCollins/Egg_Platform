using System;

namespace Game.Level083
{
    /// <summary>
    /// Level 083 可选择的演奏乐器。
    /// </summary>
    public enum Level83Instrument
    {
        Guitar,
        Harp,
        Saxophone
    }

    /// <summary>
    /// Level 083 按键谱使用的三种既有玩家输入。
    /// </summary>
    public enum Level83PerformanceInput
    {
        Left,
        Right,
        Jump
    }

    /// <summary>
    /// 单次按键谱输入的判定结果。
    /// </summary>
    public enum Level83InputResult
    {
        Ignored,
        Correct,
        RoundCompleted,
        Incorrect
    }

    /// <summary>
    /// Level 083 演奏规则状态机；只负责固定谱序、逐格判定和轮次门禁。
    /// </summary>
    public sealed class Level83Performance
    {
        #region Constants

        public const int BeatCount = 8;

        private static readonly Level83PerformanceInput[] GuitarPattern =
        {
            Level83PerformanceInput.Left,
            Level83PerformanceInput.Jump,
            Level83PerformanceInput.Left,
            Level83PerformanceInput.Left,
            Level83PerformanceInput.Left,
            Level83PerformanceInput.Jump,
            Level83PerformanceInput.Left,
            Level83PerformanceInput.Jump
        };

        private static readonly Level83PerformanceInput[] HarpPattern =
        {
            Level83PerformanceInput.Left,
            Level83PerformanceInput.Jump,
            Level83PerformanceInput.Jump,
            Level83PerformanceInput.Left,
            Level83PerformanceInput.Right,
            Level83PerformanceInput.Right,
            Level83PerformanceInput.Left,
            Level83PerformanceInput.Right
        };

        private static readonly Level83PerformanceInput[] SaxophonePattern =
        {
            Level83PerformanceInput.Right,
            Level83PerformanceInput.Jump,
            Level83PerformanceInput.Right,
            Level83PerformanceInput.Right,
            Level83PerformanceInput.Right,
            Level83PerformanceInput.Jump,
            Level83PerformanceInput.Right,
            Level83PerformanceInput.Jump
        };

        #endregion

        #region Runtime State

        private Level83PerformanceInput[] pattern;

        public Level83Instrument Instrument { get; private set; }
        public int CurrentBeatIndex { get; private set; }
        public bool IsStarted { get; private set; }
        public bool IsAwaitingRoundReset { get; private set; }
        public bool IsAwaitingReselection { get; private set; }
        public bool IsFailed { get; private set; }

        #endregion

        #region Public API

        /// <summary>
        /// 选定乐器并从第一个谱面格开始演奏；同一次演奏只允许调用一次。
        /// </summary>
        public bool TryStart(Level83Instrument instrument)
        {
            if (IsStarted)
                return false;

            Instrument = instrument;
            pattern = GetPattern(instrument);
            CurrentBeatIndex = 0;
            IsStarted = true;
            IsAwaitingRoundReset = false;
            IsAwaitingReselection = false;
            IsFailed = false;
            return true;
        }

        /// <summary>
        /// 返回当前乐器的固定八步按键谱；返回值不得由调用方修改。
        /// </summary>
        public ReadOnlySpan<Level83PerformanceInput> GetCurrentPattern()
        {
            return pattern ?? ReadOnlySpan<Level83PerformanceInput>.Empty;
        }

        /// <summary>
        /// 判定当前待输入谱面格；返回被判定格索引，失败、等待评价收口或等待重选时拒绝继续输入。
        /// </summary>
        public Level83InputResult TryAcceptInput(Level83PerformanceInput input, out int beatIndex)
        {
            beatIndex = CurrentBeatIndex;
            if (!IsStarted || IsFailed || IsAwaitingRoundReset || IsAwaitingReselection || pattern == null)
                return Level83InputResult.Ignored;

            if (input != pattern[CurrentBeatIndex])
            {
                IsFailed = true;
                return Level83InputResult.Incorrect;
            }

            CurrentBeatIndex++;
            if (CurrentBeatIndex < BeatCount)
                return Level83InputResult.Correct;

            IsAwaitingRoundReset = true;
            return Level83InputResult.RoundCompleted;
        }

        /// <summary>
        /// 八格全部正确且完成评价收起后，进入等待重选乐器状态；该状态下演奏仍视为已开始
        /// （实体音符砸击门禁保持成立），但谱面输入被拒绝，也不再自动续同一乐器下一轮。
        /// </summary>
        public bool TryEnterReselection()
        {
            if (!IsStarted || IsFailed || !IsAwaitingRoundReset)
                return false;

            IsAwaitingRoundReset = false;
            IsAwaitingReselection = true;
            return true;
        }

        /// <summary>
        /// 等待重选状态下重新选定乐器（可与上一轮相同），切换按键谱并从第一格开始新一轮。
        /// </summary>
        public bool TryReselect(Level83Instrument instrument)
        {
            if (!IsStarted || IsFailed || !IsAwaitingReselection)
                return false;

            Instrument = instrument;
            pattern = GetPattern(instrument);
            CurrentBeatIndex = 0;
            IsAwaitingReselection = false;
            return true;
        }

        #endregion

        #region Helpers

        private static Level83PerformanceInput[] GetPattern(Level83Instrument instrument)
        {
            return instrument switch
            {
                Level83Instrument.Guitar => GuitarPattern,
                Level83Instrument.Harp => HarpPattern,
                Level83Instrument.Saxophone => SaxophonePattern,
                _ => throw new ArgumentOutOfRangeException(nameof(instrument), instrument, null)
            };
        }

        #endregion
    }
}
