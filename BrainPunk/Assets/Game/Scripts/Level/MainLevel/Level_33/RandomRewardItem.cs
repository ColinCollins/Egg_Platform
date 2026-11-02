using System;
using UnityEngine;

public class RandomRewardItem : MonoBehaviour
{
    [SerializeField] private Sprite[] randomSprites;

    private SpriteRenderer render;
    private DirectMove move;
    private LayerTriggerDetector layerDetector;

    void Start()
    {
        // Debug.Log("------------------ init");
        render = GetComponent<SpriteRenderer>();
        move = GetComponent<DirectMove>();
        layerDetector = GetComponent<LayerTriggerDetector>();

        RefreshSprite();

        // 订阅地面检测事件
        if (layerDetector != null)
        {
            layerDetector.OnEnter.AddListener(OnGroundEnter);
        }
        //  Debug.Log("------------------ init: " + render);
    }

    void OnEnable()
    {
        Debug.Log("------------------ enable");
        RefreshSprite();
    }

    void OnDisable()
    {
        // 取消订阅事件
        if (layerDetector != null)
        {
            layerDetector.OnEnter.RemoveListener(OnGroundEnter);
        }
    }

    private void RefreshSprite()
    {
        if (render == null)
            return;
        render.sprite = randomSprites[UnityEngine.Random.Range(0, randomSprites.Length)];
    }

    /// <summary>
    /// 当检测到地面时调用
    /// </summary>
    private void OnGroundEnter(Collider2D other)
    {
        if (move != null)
        {
            move.enabled = false;
        }
    }
}
