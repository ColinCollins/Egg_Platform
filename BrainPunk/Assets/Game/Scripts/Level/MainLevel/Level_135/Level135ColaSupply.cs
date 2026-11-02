using UnityEngine;

/// <summary>135 柜台三瓶可乐的摇晃、归位、逐瓶售卖和原位补货；只由本关主控推进时钟。</summary>
public sealed class Level135ColaSupply : MonoBehaviour
{
    #region 柜台绑定与状态
    /// <summary>按从左到右显式绑定场景原有三瓶；售出的是原物体，补货另建普通瓶。</summary>
    [SerializeField] private SpriteRenderer[] bottles;
    /// <summary>普通瓶及有气的两帧，不修改素材资源。</summary>
    [SerializeField] private Sprite normalFrame, fizzFrameA, fizzFrameB;
    /// <summary>一次有效换向所需的最小位移，过滤指针微小抖动。</summary>
    [SerializeField] private float shakeDistance = 0.18f;
    /// <summary>累计有效换向三次后有气；单次拿起和单向拖动不算摇晃。</summary>
    [SerializeField] private int shakeReversals = 3;
    private sealed class Slot
    {
        public SpriteRenderer Bottle;
        public Transform Parent;
        public Vector3 Position, Scale, ReturnStart;
        public Quaternion Rotation;
        public int Order;
        public bool Charged, Returning;
        public float ReturnTime, Fade = 1f;
    }
    private Slot[] slots;
    private int nextSale, dragging = -1, reversals;
    private Vector3 grabOffset, shakeAnchor, lastDirection;
    private float frameClock;
    /// <summary>仅供本关输入在初始化后查询瓶子，不扫描场景。</summary>
    public bool Ready => slots != null;
    #endregion

    #region 摇晃与归位
    /// <summary>主控开始时记录原位置和大小；只在运行态调用一次。</summary>
    public void Initialize()
    {
        slots = new Slot[bottles.Length];
        for (int i = 0; i < bottles.Length; i++)
        {
            SpriteRenderer bottle = bottles[i];
            slots[i] = new Slot { Bottle = bottle, Parent = bottle.transform.parent,
                Position = bottle.transform.localPosition, Scale = bottle.transform.localScale,
                Rotation = bottle.transform.localRotation, Order = bottle.sortingOrder };
            bottle.sprite = normalFrame;
        }
    }

    /// <summary>世界点命中可操作的瓶子，返回从左到右索引；归位/渐显期间暂不接新拖动。</summary>
    public int Hit(Vector3 world)
    {
        if (!Ready || dragging >= 0) return -1;
        for (int i = 0; i < slots.Length; i++)
        {
            Slot slot = slots[i];
            if (slot.Returning || slot.Fade < 1f) continue;
            Vector3 p = slot.Bottle.transform.InverseTransformPoint(world);
            Bounds b = slot.Bottle.sprite.bounds;
            b.Expand(new Vector3(0.2f, 0.12f, 0f));
            if (p.x >= b.min.x && p.x <= b.max.x && p.y >= b.min.y && p.y <= b.max.y) return i;
        }
        return -1;
    }

    /// <summary>捕获某一瓶；摇晃累计只属于这次拿起的瓶子。</summary>
    public void BeginDrag(int index, Vector3 world)
    {
        dragging = index;
        grabOffset = slots[index].Bottle.transform.position - world;
        slots[index].Bottle.transform.SetParent(transform, true);
        shakeAnchor = world;
        lastDirection = Vector3.zero;
        reversals = 0;
        slots[index].Bottle.sortingOrder = 500;
    }

    /// <summary>跟随当前指针，任意方向来回摇均可，单向长距离移动不会充气。</summary>
    public void Drag(Vector3 world)
    {
        if (dragging < 0) return;
        Slot slot = slots[dragging];
        slot.Bottle.transform.position = world + grabOffset;
        Vector3 delta = world - shakeAnchor;
        if (delta.sqrMagnitude < shakeDistance * shakeDistance) return;
        Vector3 direction = delta.normalized;
        if (Vector3.Dot(direction, lastDirection) < -0.35f) reversals++;
        lastDirection = direction;
        shakeAnchor = world;
        if (reversals >= shakeReversals) slot.Charged = true;
    }

    /// <summary>松手、暂停或失焦时释放；归位动画在本关时钟恢复后继续。</summary>
    public void Release()
    {
        if (dragging < 0) return;
        Slot slot = slots[dragging];
        slot.Bottle.transform.SetParent(slot.Parent, true);
        slot.ReturnStart = slot.Bottle.transform.localPosition;
        slot.ReturnTime = 0f;
        slot.Returning = true;
        dragging = -1;
    }

    /// <summary>由本关有效帧推进归位、补货渐显和有气两帧。</summary>
    public void Tick(float dt)
    {
        if (!Ready) return;
        frameClock += dt;
        foreach (Slot slot in slots)
        {
            if (slot.Returning)
            {
                slot.ReturnTime += dt;
                float t = Mathf.Clamp01(slot.ReturnTime / 0.22f);
                slot.Bottle.transform.localPosition = Vector3.Lerp(slot.ReturnStart, slot.Position, 1f - Mathf.Pow(1f - t, 3f));
                if (t >= 1f) { slot.Returning = false; slot.Bottle.sortingOrder = slot.Order; }
            }
            slot.Fade = Mathf.Min(1f, slot.Fade + dt / 0.35f);
            Color color = slot.Bottle.color; color.a = slot.Fade; slot.Bottle.color = color;
            slot.Bottle.sprite = slot.Charged ? FizzFrame(frameClock) : normalFrame;
        }
    }
    #endregion

    #region 售卖与补货
    /// <summary>按左中右循环寻找可售原瓶；跳过正在拖动、归位或渐显的瓶，全部不可用时才等待。</summary>
    public bool TrySell(out SpriteRenderer sold, out bool charged)
    {
        sold = null; charged = false;
        int saleIndex = -1;
        for (int offset = 0; offset < slots.Length; offset++)
        {
            int index = (nextSale + offset) % slots.Length;
            Slot candidate = slots[index];
            if (index == dragging || candidate.Returning || candidate.Fade < 1f) continue;
            saleIndex = index;
            break;
        }
        if (saleIndex < 0) return false;
        Slot slot = slots[saleIndex];
        sold = slot.Bottle; charged = slot.Charged;
        SpriteRenderer replacement = Instantiate(sold, slot.Parent);
        replacement.name = "Level135_Cola_" + saleIndex;
        replacement.transform.localPosition = slot.Position;
        replacement.transform.localRotation = slot.Rotation;
        replacement.transform.localScale = slot.Scale;
        replacement.sortingOrder = slot.Order;
        replacement.sprite = normalFrame;
        Color c = replacement.color; c.a = 0f; replacement.color = c;
        slot.Bottle = replacement; slot.Charged = false; slot.Fade = 0f;
        nextSale = (saleIndex + 1) % slots.Length;
        return true;
    }

    /// <summary>柜台和手持瓶共用同一组有气帧。</summary>
    public Sprite FizzFrame(float clock) => ((int)(clock / 0.12f) & 1) == 0 ? fizzFrameA : fizzFrameB;
    #endregion
}
