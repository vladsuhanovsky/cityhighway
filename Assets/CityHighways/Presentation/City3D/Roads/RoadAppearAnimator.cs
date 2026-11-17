using System.Collections;
using UnityEngine;
#if DOTWEEN
using DG.Tweening;
#endif

namespace CityHighways.Presentation
{
    /// <summary>
    /// Плавное появление построенной дороги: куски «вытягиваются» вдоль дороги от узла А к узлу Б
    /// (масштаб по Z от нуля), перила — вместе с полотном, опоры моста поднимаются из воды с лёгким
    /// перелётом. С DOTween (символ DOTWEEN) — последовательность DOTween, без него — та же анимация корутиной.
    /// </summary>
    public static class RoadAppearAnimator
    {
        const float Tiny = 0.0001f;

        public static void Play(BuiltRoad road, RoadPrefabSet set, MonoBehaviour host)
        {
            if (road?.Root == null || road.Growing.Count == 0) return;
            float total = Mathf.Max(0.001f, road.Length);
            float duration = Mathf.Max(0.05f, set.buildDuration);
            // Каждый кусок стартует, когда «волна» стройки доходит до его начала, и растёт треть общего времени.
            float Delay(float at) => at / total * duration * 0.7f;
            float grow = duration * 0.35f;

            foreach (var item in road.Growing)
                item.transform.localScale = item.rise
                    ? new Vector3(item.scale.x, Tiny, item.scale.z)
                    : new Vector3(item.scale.x, item.scale.y, Tiny);

#if DOTWEEN
            var sequence = DOTween.Sequence().SetLink(road.Root);
            foreach (var item in road.Growing)
            {
                if (item.rise)
                    sequence.Insert(Delay(item.at) + grow * 0.5f,
                        item.transform.DOScaleY(item.scale.y, set.pierRiseDuration).SetEase(Ease.OutBack));
                else
                    sequence.Insert(Delay(item.at), item.transform.DOScaleZ(item.scale.z, grow).SetEase(set.growCurve));
            }
            sequence.SetId(road.Root);
#else
            if (host != null) host.StartCoroutine(Animate(road, set, Delay, grow));
            else Finish(road);
#endif
        }

        /// <summary>
        /// Постепенное появление «по фронту»: всё, что до distance (метры от начала дороги), уже лежит, кусок под
        /// фронтом вытянут ровно до него (масштаб по Z), дальше — пусто; опора поднимается, когда фронт над ней.
        /// Так дорогу проявляет стройка (RoadConstructionPlayer) кадр за кадром.
        /// </summary>
        public static void Reveal(BuiltRoad road, float distance)
        {
            if (road?.Root == null) return;
            foreach (var item in road.Growing)
            {
                if (item.transform == null) continue;
                if (item.rise)
                {
                    float k = Mathf.Clamp01((distance - item.at) / 0.06f);
                    item.transform.localScale = new Vector3(item.scale.x, Mathf.Max(Tiny, item.scale.y * k), item.scale.z);
                }
                else
                {
                    float k = Mathf.Clamp01((distance - item.at) / Mathf.Max(Tiny, item.scale.z));
                    item.transform.localScale = new Vector3(item.scale.x, item.scale.y, Mathf.Max(Tiny, item.scale.z * k));
                }
            }
        }

        /// <summary>Остановить анимацию и поставить куски в конечный размер.</summary>
        public static void Kill(BuiltRoad road)
        {
            if (road?.Root == null) return;
#if DOTWEEN
            DOTween.Kill(road.Root, true);
#endif
        }

        static void Finish(BuiltRoad road)
        {
            foreach (var item in road.Growing)
                if (item.transform != null) item.transform.localScale = item.scale;
        }

#if !DOTWEEN
        static IEnumerator Animate(BuiltRoad road, RoadPrefabSet set, System.Func<float, float> delay, float grow)
        {
            float time = 0f, rise = Mathf.Max(0.05f, set.pierRiseDuration);
            float end = 0f;
            foreach (var item in road.Growing)
                end = Mathf.Max(end, delay(item.at) + (item.rise ? grow * 0.5f + rise : grow));
            while (time < end && road.Root != null)
            {
                time += Time.deltaTime;
                foreach (var item in road.Growing)
                {
                    if (item.transform == null) continue;
                    if (item.rise)
                    {
                        float k = Mathf.Clamp01((time - delay(item.at) - grow * 0.5f) / rise);
                        item.transform.localScale = new Vector3(item.scale.x, Mathf.Max(Tiny, item.scale.y * OutBack(k)), item.scale.z);
                    }
                    else
                    {
                        float k = Mathf.Clamp01((time - delay(item.at)) / grow);
                        item.transform.localScale = new Vector3(item.scale.x, item.scale.y, Mathf.Max(Tiny, item.scale.z * set.growCurve.Evaluate(k)));
                    }
                }
                yield return null;
            }
            if (road.Root != null) Finish(road);
        }

        /// <summary>Как Ease.OutBack у DOTween: небольшой перелёт и возврат.</summary>
        static float OutBack(float t)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float x = t - 1f;
            return 1f + c3 * x * x * x + c1 * x * x;
        }
#endif
    }
}
