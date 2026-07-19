using System.Collections.Generic;
using UnityEngine;

namespace Networking
{
    // 网络预制体注册表（ScriptableObject）。
    // 在编辑器中配置所有需要网络生成的预制体，SpawnSystem 通过 prefabId 查找。
    [CreateAssetMenu(menuName = "Networking/Prefab Registry")]
    public class NetworkPrefabRegistry : ScriptableObject
    {
        [System.Serializable]
        public struct Entry
        {
            public string prefabId;
            public GameObject prefab;
        }

        public Entry[] entries;

        private Dictionary<string, GameObject> _cache;

        private void BuildCache()
        {
            _cache = new Dictionary<string, GameObject>();
            if (entries != null)
            {
                foreach (var entry in entries)
                {
                    if (!string.IsNullOrEmpty(entry.prefabId) && entry.prefab != null)
                        _cache[entry.prefabId] = entry.prefab;
                }
            }
        }

        public GameObject GetPrefab(string prefabId)
        {
            if (_cache == null)
                BuildCache();

            if (_cache.TryGetValue(prefabId, out GameObject prefab))
                return prefab;

            Debug.LogError($"[PrefabRegistry] 未找到预制体: {prefabId}");
            return null;
        }

#if UNITY_EDITOR
        // 编辑器下自动刷新缓存
        private void OnValidate()
        {
            _cache = null;
        }
#endif
    }
}
