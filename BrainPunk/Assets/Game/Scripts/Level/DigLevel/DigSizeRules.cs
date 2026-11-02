using System;
using System.Collections.Generic;

namespace Game.Dig
{
    /// <summary>无 Unity 状态的数值规则，供物理批次结算与边界测试共用。</summary>
    public static class DigSizeRules
    {
        /// <summary>以批次开始大小统一比较；任一病菌更大即拒绝整个吞噬批次。</summary>
        public static bool CanConsumeBatch(int actorSize, IReadOnlyList<int> germSizes)
        {
            if (actorSize <= 0) throw new ArgumentOutOfRangeException(nameof(actorSize));
            for (int i = 0; i < germSizes.Count; i++)
            {
                if (germSizes[i] <= 0) throw new ArgumentOutOfRangeException(nameof(germSizes));
                if (germSizes[i] > actorSize) return false;
            }
            return true;
        }

        /// <summary>线性尺寸映射；数值 10 对应单位体型，禁止用 Transform 反推大小。</summary>
        public static float ScaleFor(int currentSize) => (float)Math.Sqrt(currentSize / 10.0);
    }
}
