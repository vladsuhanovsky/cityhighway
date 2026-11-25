using System.Collections.Generic;
using UnityEngine;

namespace CityHighways.Presentation
{
    /// <summary>Pure visual layout. Retain full-size crosswalks where their
    /// envelopes fit, omit conflicting markings without changing road geometry.</summary>
    public static class JunctionCrosswalkLayout
    {
        /// <summary>Input is the renderer's angularly ordered, merged directions.
        /// Try every cyclic starting arm; keep the largest greedy non-overlapping
        /// subset, with the earliest start winning ties. Output is in input order.</summary>
        public static void Select(List<Vector2> directions, float halfWidth, float near, float far,
            List<Vector2> result, List<int> scratch, List<int> best)
        {
            result.Clear();
            best.Clear();
            for (int start = 0; start < directions.Count; start++)
            {
                scratch.Clear();
                for (int offset = 0; offset < directions.Count; offset++)
                {
                    int index = (start + offset) % directions.Count;
                    bool fits = true;
                    foreach (int selected in scratch)
                        if (Overlap(directions[index], directions[selected], halfWidth, near, far))
                        { fits = false; break; }
                    if (fits) scratch.Add(index);
                }
                if (scratch.Count <= best.Count) continue;
                best.Clear();
                best.AddRange(scratch);
                if (best.Count == directions.Count) break;
            }
            best.Sort();
            foreach (int index in best) result.Add(directions[index]);
        }

        // Rectangular envelopes include gaps between the painted stripes;
        // intersection is a visual-risk signal, not proof of overlapping paint.
        public static bool Overlap(Vector2 a, Vector2 b, float halfWidth, float near, float far)
        {
            if (a.sqrMagnitude < 1e-8f || b.sqrMagnitude < 1e-8f || near >= far || halfWidth <= 0f) return false;
            a.Normalize(); b.Normalize();
            Vector2 an = new Vector2(-a.y, a.x), bn = new Vector2(-b.y, b.x);
            float center = (near + far) / 2f, halfLength = (far - near) / 2f;
            Vector2 delta = (a - b) * center;
            bool Overlap(Vector2 axis) => Mathf.Abs(Vector2.Dot(delta, axis)) <
                halfLength * (Mathf.Abs(Vector2.Dot(a, axis)) + Mathf.Abs(Vector2.Dot(b, axis))) +
                halfWidth * (Mathf.Abs(Vector2.Dot(an, axis)) + Mathf.Abs(Vector2.Dot(bn, axis))) - 1e-6f;
            return Overlap(a) && Overlap(b) && Overlap(an) && Overlap(bn);
        }
    }
}
