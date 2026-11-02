using UnityEngine;

namespace Game.Common
{
    /// <summary>
    /// 监听触发事件，在指定偏移位置生成目标物体的副本（只生成一次）
    /// </summary>
    public class SpawnCopyOnTrigger : MonoBehaviour
    {
        [Header("Spawn Settings")]
        [Tooltip("要生成的目标物体（如果为空则生成当前物体）")]
        [SerializeField] private GameObject target;
        
        [Tooltip("生成的副本的父物体（如果为空则不设置父物体）")]
        [SerializeField] private Transform root;
        
        [Tooltip("生成副本的偏移位置（相对于当前物体）")]
        [SerializeField] private Vector3 spawnOffset = Vector3.zero;
        
        [Tooltip("是否使用世界空间偏移（false 则使用本地空间）")]
        [SerializeField] private bool useWorldSpace = true;
        
        [Tooltip("是否已生成（只生成一次）")]
        [SerializeField] private bool hasSpawned = false;
        
        [Header("Copy Settings")]
        [Tooltip("生成的副本名称后缀")]
        [SerializeField] private string copyNameSuffix = "_Copy";
        
        private GameObject spawnedCopy;
        
        /// <summary>
        /// 触发生成副本
        /// </summary>
        public void TriggerSpawn()
        {
            if (hasSpawned)
            {
                Debug.LogWarning($"[SpawnCopyOnTrigger] {gameObject.name} 已经生成过副本，不再生成。");
                return;
            }
            
            SpawnCopy();
        }
        
        /// <summary>
        /// 生成副本
        /// </summary>
        private void SpawnCopy()
        {
            // 确定要生成的目标
            GameObject targetToSpawn = target != null ? target : gameObject;
            
            // 计算生成位置
            Vector3 spawnPosition;
            if (useWorldSpace)
            {
                spawnPosition = transform.position + spawnOffset;
            }
            else
            {
                spawnPosition = transform.position + transform.TransformDirection(spawnOffset);
            }
            
            // 生成副本
            spawnedCopy = Instantiate(targetToSpawn, spawnPosition, transform.rotation);
            
            // 设置父物体
            if (root != null)
            {
                spawnedCopy.transform.SetParent(root);
            }
            
            // 设置副本名称
            spawnedCopy.name = targetToSpawn.name + copyNameSuffix;
            
            // 移除副本上的 SpawnCopyOnTrigger 组件（避免副本也生成副本）
/*             SpawnCopyOnTrigger copyComponent = spawnedCopy.GetComponent<SpawnCopyOnTrigger>();
            if (copyComponent != null)
            {
                Destroy(copyComponent);
            } */
            
            // 标记已生成
            hasSpawned = true;
            
            Debug.Log($"[SpawnCopyOnTrigger] {gameObject.name} 在位置 {spawnPosition} 生成了 {targetToSpawn.name} 的副本 {spawnedCopy.name}");
        }
        
        /// <summary>
        /// 重置生成状态（允许再次生成）
        /// </summary>
        public void ResetSpawnState()
        {
            hasSpawned = false;
            if (spawnedCopy != null)
            {
                Destroy(spawnedCopy);
                spawnedCopy = null;
            }
        }
        
        /// <summary>
        /// 获取生成的副本
        /// </summary>
        public GameObject GetSpawnedCopy()
        {
            return spawnedCopy;
        }
        
        /// <summary>
        /// 设置生成偏移
        /// </summary>
        public void SetSpawnOffset(Vector3 offset)
        {
            spawnOffset = offset;
        }
    }
}
