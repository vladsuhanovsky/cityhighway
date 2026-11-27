using System;
using UnityEngine;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Evaluates the same bilinear value noise as TerrainTexture.ValueNoise,
    /// reusing its four hash corners for consecutive pixels in the same cell.
    /// Construct once per image row and per (cell size, salt) noise layer.
    /// </summary>
    public struct TerrainNoiseRowSampler
    {
        readonly float _cell;
        readonly int _salt;
        readonly long _rowCell;
        readonly float _rowWeight;

        bool _hasColumn;
        long _columnCell;
        float _a, _b, _c, _d;

        public TerrainNoiseRowSampler(float y, float cell, int salt)
        {
            if (cell <= 0f || float.IsNaN(cell) || float.IsInfinity(cell))
                throw new ArgumentOutOfRangeException(nameof(cell));
            if (float.IsNaN(y) || float.IsInfinity(y))
                throw new ArgumentOutOfRangeException(nameof(y));

            _cell = cell;
            _salt = salt;
            // Explicit double-to-single conversion fixes the grid precision before
            // subtracting its integer part. Mono may otherwise retain extra
            // precision in a float local, depending on JIT/register allocation.
            float gy = (float)((double)y / cell);
            float iy = Mathf.Floor(gy);
            float fy = gy - iy;
            _rowWeight = fy * fy * (3f - 2f * fy);
            _rowCell = (long)iy;

            _hasColumn = false;
            _columnCell = 0;
            _a = _b = _c = _d = 0f;
        }

        public float Sample(float x)
        {
            float gx = (float)((double)x / _cell);
            float ix = Mathf.Floor(gx);
            float fx = gx - ix;
            fx = fx * fx * (3f - 2f * fx);
            long lx = (long)ix;
            if (!_hasColumn || lx != _columnCell)
            {
                _a = Hash(lx, _rowCell, _salt);
                _b = Hash(lx + 1, _rowCell, _salt);
                _c = Hash(lx, _rowCell + 1, _salt);
                _d = Hash(lx + 1, _rowCell + 1, _salt);
                _columnCell = lx;
                _hasColumn = true;
            }
            float top = _a + (_b - _a) * fx;
            float bottom = _c + (_d - _c) * fx;
            return top + (bottom - top) * _rowWeight;
        }

        static float Hash(long x, long y, int salt)
        {
            unchecked
            {
                uint h = (uint)(x * 73856093) ^ (uint)(y * 19349663) ^
                    (uint)(salt * 83492791);
                h ^= h >> 13;
                h *= 0x5bd1e995;
                h ^= h >> 15;
                return (h & 0xFFFFFF) / (float)0x1000000;
            }
        }
    }
}
