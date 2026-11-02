using System.Collections.Generic;

/// <summary>
/// 出餐前的有限时域接餐预测：把在途餐点和余下订单纳入，选择 1~3 中最大的可接待量。
/// 只预测顾客的接餐/补位，不决定玩家是否能吃，也不修改实际餐盘或角色。
/// </summary>
internal static class Level140MealPlanner
{
    /// <summary>单名顾客膨胀换人前需要实际接收的固定餐点数；只用于本关预测，不代表玩家配置。</summary>
    internal const int MealsPerCustomer = 3;

    /// <summary>预测器使用的顾客状态快照；调用方创建后，预测过程只复制和模拟，不回写运行时顾客。</summary>
    internal sealed class Diner
    {
        /// <summary>顾客已经实际吃到的份数；传入前应已合并飞嘴在途占用，范围由调用方保证。</summary>
        public int Count;
        /// <summary>顾客是否处于吃满后的补位等待阶段；为 true 时到 ReadyAt 前不能接餐。</summary>
        public bool Returning;
        /// <summary>补位顾客最早可重新接餐的预测时间；与餐点到达时间使用同一相对秒基准。</summary>
        public float ReadyAt;
        /// <summary>本名顾客吃到第三份后席位恢复接餐所需的预测时长；不得包含旧顾客出屏等待。</summary>
        public float ReplacementAfterThirdMeal;

        /// <summary>创建供单次候选模拟使用的浅副本；当前成员均为值类型，调用方不得向本类型新增未复制的可变引用。</summary>
        public Diner Copy() => (Diner)MemberwiseClone();
    }

    /// <summary>预测器使用的一盘食物快照；记录剩余份数和到达三个顾客席位的相对时间。</summary>
    internal sealed class Meal
    {
        /// <summary>该盘尚未分配给顾客的份数；模拟过程中递减，原运行时餐盘不会被修改。</summary>
        public int Remaining;
        /// <summary>餐盘到达三个顾客席位的相对时间；必须固定为三项，不可达席位使用正无穷。</summary>
        public float[] Arrivals = new float[3];

        /// <summary>创建独立模拟副本并克隆到达时间数组；用于避免不同候选份数之间共享可变状态。</summary>
        public Meal Copy() => new Meal
        {
            Remaining = Remaining,
            Arrivals = (float[])Arrivals.Clone()
        };
    }

    private struct Visit
    {
        public float Time;
        public int MealIndex;
        public int Seat;
    }

    /// <summary>在一至三份中选择预测可被顾客接住的最大数量；仅计算快照，失败时返回一份并通过 forecastCovered 报告缺口。</summary>
    public static int Choose(Diner[] diners, List<Meal> activeMeals, float[] newMealArrivals,
        int subsequentOrders, float orderInterval, out bool forecastCovered)
    {
        for (int portions = 3; portions >= 1; portions--)
        {
            if (!CanReceive(diners, activeMeals, newMealArrivals, portions, subsequentOrders, orderInterval)) continue;
            forecastCovered = true;
            return portions;
        }

        // 不拒绝玩家有效点击，也不隐藏/吞掉餐点。预测缺口交给调用方报告，保守按一份继续。
        forecastCovered = false;
        return 1;
    }

    private static bool CanReceive(Diner[] sourceDiners, List<Meal> activeMeals, float[] newMealArrivals,
        int portions, int subsequentOrders, float orderInterval)
    {
        var diners = new Diner[sourceDiners.Length];
        for (int i = 0; i < diners.Length; i++) diners[i] = sourceDiners[i].Copy();
        var meals = new List<Meal>();
        foreach (var meal in activeMeals) meals.Add(meal.Copy());
        // 当前候选以外，给每次未来最快点击至少留一份。不是仅保住当前这一盘。
        for (int order = 0; order <= subsequentOrders; order++)
        {
            var meal = new Meal { Remaining = order == 0 ? portions : 1 };
            for (int seat = 0; seat < 3; seat++)
                meal.Arrivals[seat] = newMealArrivals[seat] + order * orderInterval;
            meals.Add(meal);
        }

        var visits = new List<Visit>(meals.Count * 3);
        for (int i = 0; i < meals.Count; i++)
            for (int seat = 0; seat < 3; seat++)
                if (!float.IsInfinity(meals[i].Arrivals[seat]))
                    visits.Add(new Visit { Time = meals[i].Arrivals[seat], MealIndex = i, Seat = seat });
        visits.Sort((a, b) =>
        {
            int time = a.Time.CompareTo(b.Time);
            return time != 0 ? time : b.MealIndex.CompareTo(a.MealIndex);
        });

        foreach (Visit visit in visits)
        {
            Meal meal = meals[visit.MealIndex];
            if (meal.Remaining == 0) continue;
            Diner diner = diners[visit.Seat];
            if (diner.Returning)
            {
                if (visit.Time < diner.ReadyAt) continue;
                diner.Returning = false;
                diner.Count = 0;
            }
            if (diner.Count >= MealsPerCustomer) continue;
            meal.Remaining--;
            diner.Count++;
            if (diner.Count != MealsPerCustomer) continue;
            diner.Returning = true;
            diner.ReadyAt = visit.Time + diner.ReplacementAfterThirdMeal;
        }

        foreach (Meal meal in meals) if (meal.Remaining != 0) return false;
        return true;
    }
}
