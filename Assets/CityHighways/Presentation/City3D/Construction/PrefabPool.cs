using UnityEngine;
using UnityEngine.Pool;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Пул копий одного префаба на встроенном UnityEngine.Pool.ObjectPool: объекты не создаются и не удаляются
    /// во время стройки — берутся из пула (Get) и возвращаются (Release) выключенными под корнем пула.
    /// </summary>
    public sealed class PrefabPool
    {
        readonly GameObject _prefab;
        readonly Transform _root;
        readonly ObjectPool<GameObject> _pool;

        public PrefabPool(GameObject prefab, Transform root, int prewarm, int maxSize = 64)
        {
            _prefab = prefab;
            _root = root;
            _pool = new ObjectPool<GameObject>(Create, OnGet, OnRelease, OnDestroyItem, true, Mathf.Max(1, prewarm), maxSize);
            // Заранее создаём prewarm копий, чтобы первая стройка не дёргала Instantiate.
            var warm = new GameObject[prewarm];
            for (int i = 0; i < prewarm; i++) warm[i] = _pool.Get();
            for (int i = 0; i < prewarm; i++) _pool.Release(warm[i]);
        }

        public int ActiveCount => _pool.CountActive;

        /// <summary>Взять объект и поставить в точку (масштаб — как у префаба).</summary>
        public GameObject Get(Vector3 position, Quaternion rotation, Transform parent)
        {
            var go = _pool.Get();
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, rotation);
            go.transform.localScale = _prefab.transform.localScale;
            return go;
        }

        public void Release(GameObject go)
        {
            if (go != null) _pool.Release(go);
        }

        public void Clear() => _pool.Clear();

        GameObject Create()
        {
            var go = Object.Instantiate(_prefab, _root);
            go.name = _prefab.name;
            go.SetActive(false);
            return go;
        }

        static void OnGet(GameObject go) => go.SetActive(true);

        void OnRelease(GameObject go)
        {
            RoadConstructionPlayer.KillTweens(go);
            go.SetActive(false);
            go.transform.SetParent(_root, false);
        }

        static void OnDestroyItem(GameObject go)
        {
            if (go == null) return;
            if (Application.isPlaying) Object.Destroy(go);
            else Object.DestroyImmediate(go);
        }
    }
}
