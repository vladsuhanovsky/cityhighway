using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Сборщик низкополигональных мешей: грани без сглаживания (у каждой свои вершины — «low-poly» с чёткими
    /// гранями), UV — плитки CityAtlas. Всё, что строится в одном районе, складывается в один меш — город
    /// рисуется считанными вызовами.
    /// </summary>
    public sealed class MeshBuilder
    {
        readonly List<Vector3> _vertices = new List<Vector3>();
        readonly List<Vector3> _normals = new List<Vector3>();
        readonly List<Vector2> _uvs = new List<Vector2>();
        readonly List<int> _triangles = new List<int>();

        public int VertexCount => _vertices.Count;

        /// <summary>Четырёхугольник a-b-c-d по часовой стрелке, если смотреть на лицевую сторону (так Unity видит грань).</summary>
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Rect uv)
        {
            var normal = Vector3.Cross(b - a, c - a).normalized;
            int i = _vertices.Count;
            _vertices.Add(a); _vertices.Add(b); _vertices.Add(c); _vertices.Add(d);
            for (int k = 0; k < 4; k++) _normals.Add(normal);
            _uvs.Add(new Vector2(uv.xMin, uv.yMin));
            _uvs.Add(new Vector2(uv.xMin, uv.yMax));
            _uvs.Add(new Vector2(uv.xMax, uv.yMax));
            _uvs.Add(new Vector2(uv.xMax, uv.yMin));
            _triangles.Add(i); _triangles.Add(i + 1); _triangles.Add(i + 2);
            _triangles.Add(i); _triangles.Add(i + 2); _triangles.Add(i + 3);
        }

        /// <summary>Треугольник a-b-c по часовой стрелке со стороны лица.</summary>
        public void Tri(Vector3 a, Vector3 b, Vector3 c, Rect uv)
        {
            var normal = Vector3.Cross(b - a, c - a).normalized;
            int i = _vertices.Count;
            _vertices.Add(a); _vertices.Add(b); _vertices.Add(c);
            for (int k = 0; k < 3; k++) _normals.Add(normal);
            _uvs.Add(new Vector2(uv.xMin, uv.yMin));
            _uvs.Add(new Vector2(uv.center.x, uv.yMax));
            _uvs.Add(new Vector2(uv.xMax, uv.yMin));
            _triangles.Add(i); _triangles.Add(i + 1); _triangles.Add(i + 2);
        }

        /// <summary>Добавить вершины другого сборщика, переставив их матрицей (поворот, сдвиг, масштаб).</summary>
        public void Append(MeshBuilder other, Matrix4x4 transform)
        {
            int offset = _vertices.Count;
            var normalMatrix = transform.inverse.transpose;
            for (int i = 0; i < other._vertices.Count; i++)
            {
                _vertices.Add(transform.MultiplyPoint3x4(other._vertices[i]));
                _normals.Add(normalMatrix.MultiplyVector(other._normals[i]).normalized);
                _uvs.Add(other._uvs[i]);
            }
            foreach (var t in other._triangles) _triangles.Add(t + offset);
        }

        public void Clear()
        {
            _vertices.Clear();
            _normals.Clear();
            _uvs.Clear();
            _triangles.Clear();
        }

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name, hideFlags = HideFlags.DontSave };
            if (_vertices.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(_vertices);
            mesh.SetNormals(_normals);
            mesh.SetUVs(0, _uvs);
            mesh.SetTriangles(_triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        // ================================================================== Примитивы

        /// <summary>Коробка: стены с фасадом facade и крыша roof. Центр основания (cx, y0, cz), размеры w × h × d.</summary>
        public void Box(float cx, float cz, float w, float d, float y0, float h, Rect facade, Rect roof, bool bottom = false)
        {
            float x0 = cx - w / 2f, x1 = cx + w / 2f, z0 = cz - d / 2f, z1 = cz + d / 2f, y1 = y0 + h;
            // Стены: юг (−Z, к зрителю), восток, север, запад.
            Quad(new Vector3(x0, y0, z0), new Vector3(x0, y1, z0), new Vector3(x1, y1, z0), new Vector3(x1, y0, z0), facade);
            Quad(new Vector3(x1, y0, z0), new Vector3(x1, y1, z0), new Vector3(x1, y1, z1), new Vector3(x1, y0, z1), facade);
            Quad(new Vector3(x1, y0, z1), new Vector3(x1, y1, z1), new Vector3(x0, y1, z1), new Vector3(x0, y0, z1), facade);
            Quad(new Vector3(x0, y0, z1), new Vector3(x0, y1, z1), new Vector3(x0, y1, z0), new Vector3(x0, y0, z0), facade);
            Quad(new Vector3(x0, y1, z0), new Vector3(x0, y1, z1), new Vector3(x1, y1, z1), new Vector3(x1, y1, z0), roof);
            if (bottom) Quad(new Vector3(x0, y0, z1), new Vector3(x0, y0, z0), new Vector3(x1, y0, z0), new Vector3(x1, y0, z1), roof);
        }

        /// <summary>Двускатная крыша над прямоугольником: конёк вдоль длинной стороны, фронтоны — фасадом.</summary>
        public void GableRoof(float cx, float cz, float w, float d, float y0, float rh, Rect roof, Rect gable)
        {
            float x0 = cx - w / 2f, x1 = cx + w / 2f, z0 = cz - d / 2f, z1 = cz + d / 2f, top = y0 + rh;
            if (w >= d)
            {
                Quad(new Vector3(x0, y0, z0), new Vector3(x0, top, cz), new Vector3(x1, top, cz), new Vector3(x1, y0, z0), roof);
                Quad(new Vector3(x1, y0, z1), new Vector3(x1, top, cz), new Vector3(x0, top, cz), new Vector3(x0, y0, z1), roof);
                Tri(new Vector3(x0, y0, z1), new Vector3(x0, top, cz), new Vector3(x0, y0, z0), gable);
                Tri(new Vector3(x1, y0, z0), new Vector3(x1, top, cz), new Vector3(x1, y0, z1), gable);
            }
            else
            {
                Quad(new Vector3(x0, y0, z1), new Vector3(cx, top, z1), new Vector3(cx, top, z0), new Vector3(x0, y0, z0), roof);
                Quad(new Vector3(x1, y0, z0), new Vector3(cx, top, z0), new Vector3(cx, top, z1), new Vector3(x1, y0, z1), roof);
                Tri(new Vector3(x0, y0, z0), new Vector3(cx, top, z0), new Vector3(x1, y0, z0), gable);
                Tri(new Vector3(x1, y0, z1), new Vector3(cx, top, z1), new Vector3(x0, y0, z1), gable);
            }
        }

        /// <summary>Вальмовая крыша: четыре ската сходятся к короткому коньку.</summary>
        public void HipRoof(float cx, float cz, float w, float d, float y0, float rh, Rect roof)
        {
            float x0 = cx - w / 2f, x1 = cx + w / 2f, z0 = cz - d / 2f, z1 = cz + d / 2f, top = y0 + rh;
            float inset = Mathf.Min(w, d) / 2f;
            Vector3 ra, rb;
            if (w >= d) { ra = new Vector3(x0 + inset, top, cz); rb = new Vector3(x1 - inset, top, cz); }
            else { ra = new Vector3(cx, top, z0 + inset); rb = new Vector3(cx, top, z1 - inset); }
            var a = new Vector3(x0, y0, z0); var b = new Vector3(x1, y0, z0);
            var c = new Vector3(x1, y0, z1); var e = new Vector3(x0, y0, z1);
            if (w >= d)
            {
                Quad(a, ra, rb, b, roof);
                Quad(c, rb, ra, e, roof);
                Tri(e, ra, a, roof);
                Tri(b, rb, c, roof);
            }
            else
            {
                Quad(e, rb, ra, a, roof);
                Quad(b, ra, rb, c, roof);
                Tri(a, ra, b, roof);
                Tri(c, rb, e, roof);
            }
        }

        /// <summary>Многогранная призма (круглая башня, труба, опора моста): sides граней, крыша — веером.</summary>
        public void Prism(float cx, float cz, float r, float y0, float h, int sides, Rect wall, Rect top, float topRadius = -1f)
        {
            if (topRadius < 0f) topRadius = r;
            float y1 = y0 + h;
            var center = new Vector3(cx, y1, cz);
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * Mathf.PI * 2f / sides, a1 = (i + 1) * Mathf.PI * 2f / sides;
                var p0 = new Vector3(cx + Mathf.Cos(a0) * r, y0, cz + Mathf.Sin(a0) * r);
                var p1 = new Vector3(cx + Mathf.Cos(a1) * r, y0, cz + Mathf.Sin(a1) * r);
                var q0 = new Vector3(cx + Mathf.Cos(a0) * topRadius, y1, cz + Mathf.Sin(a0) * topRadius);
                var q1 = new Vector3(cx + Mathf.Cos(a1) * topRadius, y1, cz + Mathf.Sin(a1) * topRadius);
                float u0 = wall.xMin + wall.width * i / sides, u1 = wall.xMin + wall.width * (i + 1) / sides;
                Quad(p0, q0, q1, p1, new Rect(u0, wall.yMin, u1 - u0, wall.height));
                if (topRadius > 0.0001f) Tri(q0, center, q1, top);
            }
        }

        /// <summary>Конус (шпиль, ель): основание r на высоте y0, вершина на y0 + h.</summary>
        public void Cone(float cx, float cz, float r, float y0, float h, int sides, Rect uv)
        {
            var apex = new Vector3(cx, y0 + h, cz);
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * Mathf.PI * 2f / sides, a1 = (i + 1) * Mathf.PI * 2f / sides;
                var p0 = new Vector3(cx + Mathf.Cos(a0) * r, y0, cz + Mathf.Sin(a0) * r);
                var p1 = new Vector3(cx + Mathf.Cos(a1) * r, y0, cz + Mathf.Sin(a1) * r);
                Tri(p0, apex, p1, uv);
            }
        }

        static readonly Vector3[] IcoVertices = BuildIco(out IcoFaces);
        static readonly int[] IcoFaces;

        static Vector3[] BuildIco(out int[] faces)
        {
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            var v = new[]
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1)
            };
            for (int i = 0; i < v.Length; i++) v[i] = v[i].normalized;
            faces = new[]
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1
            };
            return v;
        }

        /// <summary>Крона-икосаэдр (20 граней): центр, радиусы по горизонтали и вертикали.</summary>
        public void Blob(Vector3 center, float r, float ry, Rect uv)
        {
            for (int f = 0; f < IcoFaces.Length; f += 3)
            {
                Vector3 P(int i) => center + new Vector3(IcoVertices[i].x * r, IcoVertices[i].y * ry, IcoVertices[i].z * r);
                Tri(P(IcoFaces[f]), P(IcoFaces[f + 1]), P(IcoFaces[f + 2]), uv);
            }
        }

        /// <summary>Брус между двумя точками (перила, фермы, полотно моста): ширина width, высота height.</summary>
        public void Beam(Vector3 a, Vector3 b, float width, float height, Rect side, Rect top)
        {
            var along = b - a;
            float length = along.magnitude;
            if (length < 0.0001f) return;
            var center = (a + b) / 2f;
            var rotation = Quaternion.LookRotation(along / length, Vector3.up);
            var local = new MeshBuilder();
            local.Box(0f, 0f, width, length, -height / 2f, height, side, top, true);
            Append(local, Matrix4x4.TRS(center, rotation, Vector3.one));
        }
    }

    /// <summary>Типы зданий и их сборка из примитивов. Размеры — в метрах сцены (1 = 100 пикселей поля).</summary>
    public static class CityShapes
    {
        public enum Kind
        {
            Block, Parapet, Gable, Hip, Stepped, Spire, Round, Landmark, LBlock, Sawtooth, Factory, Hangar, Warehouse, Panel
        }

        /// <summary>
        /// Построить здание в локальных координатах (центр основания в нуле): w × d — основание, h — высота стен.
        /// random — для деталей (оттенок, надстройки), чтобы одинаковые типы не были копиями.
        /// </summary>
        public static void Build(MeshBuilder b, Kind kind, float w, float d, float h, System.Random random)
        {
            int col = random.Next(CityAtlas.Columns);
            Rect Facade(CityAtlas.Row row) => CityAtlas.Uv(row, col);
            Rect Roof(int c) => CityAtlas.Solid(CityAtlas.Row.Roof, c);
            var flatRoof = Roof(random.NextDouble() < 0.5 ? CityAtlas.RoofDark : CityAtlas.RoofGravel);
            var lowRow = h > 0.16f ? CityAtlas.Row.Mid : CityAtlas.Row.Low;

            switch (kind)
            {
                case Kind.Parapet:
                    b.Box(0f, 0f, w, d, 0f, h, Facade(lowRow), flatRoof);
                    // Парапет и надстройка на крыше.
                    b.Box(0f, 0f, w * 0.92f, d * 0.92f, h, 0.008f, Facade(lowRow), Roof(CityAtlas.RoofLight));
                    b.Box(w * 0.18f, -d * 0.12f, w * 0.3f, d * 0.28f, h, 0.025f, Roof(CityAtlas.RoofMetal), Roof(CityAtlas.RoofMetal));
                    break;
                case Kind.Gable:
                    b.Box(0f, 0f, w, d, 0f, h, Facade(CityAtlas.Row.Low), flatRoof);
                    b.GableRoof(0f, 0f, w * 1.06f, d * 1.06f, h, Mathf.Min(w, d) * 0.45f,
                        Roof(random.NextDouble() < 0.6 ? CityAtlas.RoofRed : CityAtlas.RoofBrown), Facade(CityAtlas.Row.Low));
                    break;
                case Kind.Hip:
                    b.Box(0f, 0f, w, d, 0f, h, Facade(CityAtlas.Row.Low), flatRoof);
                    b.HipRoof(0f, 0f, w * 1.06f, d * 1.06f, h, Mathf.Min(w, d) * 0.38f,
                        Roof(random.NextDouble() < 0.5 ? CityAtlas.RoofBrown : CityAtlas.RoofCopper));
                    break;
                case Kind.Stepped:
                {
                    float h1 = h * 0.5f, h2 = h * 0.32f, h3 = h * 0.18f;
                    b.Box(0f, 0f, w, d, 0f, h1, Facade(CityAtlas.Row.Tall), flatRoof);
                    b.Box(0f, 0f, w * 0.74f, d * 0.74f, h1, h2, Facade(CityAtlas.Row.Tall), flatRoof);
                    b.Box(0f, 0f, w * 0.48f, d * 0.48f, h1 + h2, h3, Facade(CityAtlas.Row.Tall), Roof(CityAtlas.RoofMetal));
                    break;
                }
                case Kind.Spire:
                    b.Box(0f, 0f, w, d, 0f, h, Facade(CityAtlas.Row.Tall), Roof(CityAtlas.RoofBlack));
                    b.Box(0f, 0f, w * 0.6f, d * 0.6f, h, h * 0.06f, Facade(CityAtlas.Row.Tall), Roof(CityAtlas.RoofMetal));
                    b.Cone(0f, 0f, Mathf.Min(w, d) * 0.12f, h * 1.06f, h * 0.28f, 6, CityAtlas.Solid(CityAtlas.Row.Misc, CityAtlas.Spire));
                    break;
                case Kind.Round:
                {
                    float r = Mathf.Min(w, d) / 2f;
                    b.Prism(0f, 0f, r, 0f, h, 10, Facade(CityAtlas.Row.Tall), Roof(CityAtlas.RoofDark));
                    b.Prism(0f, 0f, r * 0.75f, h, h * 0.05f, 10, Roof(CityAtlas.RoofMetal), Roof(CityAtlas.RoofMetal), r * 0.6f);
                    break;
                }
                case Kind.Landmark:
                {
                    // Высотка «свадебным тортом»: широкое основание, три уступа и золотой шпиль.
                    var stone = CityAtlas.Uv(CityAtlas.Row.Mid, 0);
                    float h1 = h * 0.42f, h2 = h * 0.26f, h3 = h * 0.16f;
                    b.Box(0f, 0f, w, d, 0f, h1 * 0.55f, stone, flatRoof);
                    b.Box(0f, 0f, w * 0.62f, d * 0.62f, 0f, h1, stone, flatRoof);
                    b.Box(0f, 0f, w * 0.44f, d * 0.44f, h1, h2, stone, flatRoof);
                    b.Box(0f, 0f, w * 0.28f, d * 0.28f, h1 + h2, h3, stone, Roof(CityAtlas.RoofCopper));
                    b.Cone(0f, 0f, w * 0.1f, h1 + h2 + h3, h * 0.32f, 8, CityAtlas.Solid(CityAtlas.Row.Misc, CityAtlas.Spire));
                    break;
                }
                case Kind.LBlock:
                    b.Box(-w * 0.2f, 0f, w * 0.6f, d, 0f, h, Facade(lowRow), flatRoof);
                    b.Box(w * 0.3f, -d * 0.25f, w * 0.4f, d * 0.5f, 0f, h * 0.8f, Facade(lowRow), flatRoof);
                    break;
                case Kind.Sawtooth:
                {
                    b.Box(0f, 0f, w, d, 0f, h, Facade(CityAtlas.Row.Industrial), flatRoof);
                    int teeth = Mathf.Max(2, Mathf.RoundToInt(w / 0.05f));
                    float step = w / teeth, rise = Mathf.Min(0.035f, h * 0.5f);
                    var glassRoof = CityAtlas.Solid(CityAtlas.Row.Roof, CityAtlas.RoofMetal);
                    for (int i = 0; i < teeth; i++)
                    {
                        float x0 = -w / 2f + i * step, x1 = x0 + step, z0 = -d / 2f, z1 = d / 2f;
                        // Скат и вертикальное «окно» зуба.
                        b.Quad(new Vector3(x0, h, z0), new Vector3(x0, h, z1), new Vector3(x1, h + rise, z1), new Vector3(x1, h + rise, z0), flatRoof);
                        b.Quad(new Vector3(x1, h, z0), new Vector3(x1, h + rise, z0), new Vector3(x1, h + rise, z1), new Vector3(x1, h, z1), glassRoof);
                        b.Tri(new Vector3(x0, h, z0), new Vector3(x1, h + rise, z0), new Vector3(x1, h, z0), Facade(CityAtlas.Row.Industrial));
                        b.Tri(new Vector3(x1, h, z1), new Vector3(x1, h + rise, z1), new Vector3(x0, h, z1), Facade(CityAtlas.Row.Industrial));
                    }
                    break;
                }
                case Kind.Factory:
                {
                    b.Box(0f, 0f, w, d, 0f, h, Facade(CityAtlas.Row.Industrial), flatRoof);
                    float r = Mathf.Min(w, d) * 0.12f;
                    b.Prism(w * 0.32f, d * 0.25f, r, 0f, h * 3.2f, 8,
                        CityAtlas.Uv(CityAtlas.Row.Misc, CityAtlas.ChimneyStripe), Roof(CityAtlas.RoofBlack), r * 0.8f);
                    break;
                }
                case Kind.Hangar:
                {
                    // Полуцилиндр вдоль длинной стороны.
                    bool alongX = w >= d;
                    float length = alongX ? w : d, span = alongX ? d : w;
                    const int arcs = 7;
                    var metal = CityAtlas.Solid(CityAtlas.Row.Roof, CityAtlas.RoofMetal);
                    var end = CityAtlas.Uv(CityAtlas.Row.Industrial, col);
                    var local = new MeshBuilder();
                    for (int i = 0; i < arcs; i++)
                    {
                        float a0 = Mathf.PI * i / arcs, a1 = Mathf.PI * (i + 1) / arcs;
                        float x0 = Mathf.Cos(a0) * span / 2f, y0 = Mathf.Sin(a0) * h, x1 = Mathf.Cos(a1) * span / 2f, y1 = Mathf.Sin(a1) * h;
                        local.Quad(new Vector3(x0, y0, -length / 2f), new Vector3(x1, y1, -length / 2f), new Vector3(x1, y1, length / 2f), new Vector3(x0, y0, length / 2f), metal);
                        local.Tri(new Vector3(x0, y0, -length / 2f), new Vector3(0f, 0f, -length / 2f), new Vector3(x1, y1, -length / 2f), end);
                        local.Tri(new Vector3(x1, y1, length / 2f), new Vector3(0f, 0f, length / 2f), new Vector3(x0, y0, length / 2f), end);
                    }
                    b.Append(local, alongX ? Matrix4x4.Rotate(Quaternion.Euler(0f, 90f, 0f)) : Matrix4x4.identity);
                    break;
                }
                case Kind.Warehouse:
                    b.Box(0f, 0f, w, d, 0f, h, Facade(CityAtlas.Row.Industrial), Roof(CityAtlas.RoofLight));
                    break;
                case Kind.Panel:
                    // Панельный дом: длинная плита, лестничные выходы на крышу.
                    b.Box(0f, 0f, w, d, 0f, h, CityAtlas.Uv(CityAtlas.Row.Mid, col), flatRoof);
                    for (int i = -1; i <= 1; i += 2)
                        b.Box(w * 0.3f * i, 0f, Mathf.Min(w, d) * 0.22f, Mathf.Min(w, d) * 0.22f, h, 0.02f, Roof(CityAtlas.RoofLight), Roof(CityAtlas.RoofLight));
                    break;
                default:
                    b.Box(0f, 0f, w, d, 0f, h, Facade(lowRow), flatRoof);
                    break;
            }
        }

        public enum Tree { Round, Spruce, Poplar }

        /// <summary>Дерево: ствол и крона (икосаэдр, ярусы конусов или вытянутая крона). size — высота.</summary>
        public static void BuildTree(MeshBuilder b, Tree kind, Vector3 at, float size, System.Random random, bool autumn)
        {
            var trunk = CityAtlas.Solid(CityAtlas.Row.Nature, CityAtlas.Trunk);
            int leafIndex = autumn && random.NextDouble() < 0.35 ? CityAtlas.LeafAutumn : random.Next(4);
            var leaf = CityAtlas.Solid(CityAtlas.Row.Nature, leafIndex);
            switch (kind)
            {
                case Tree.Spruce:
                {
                    var needles = CityAtlas.Solid(CityAtlas.Row.Nature, random.NextDouble() < 0.5 ? CityAtlas.Spruce : CityAtlas.LeafDark);
                    b.Prism(at.x, at.z, size * 0.05f, at.y, size * 0.2f, 5, trunk, trunk);
                    b.Cone(at.x, at.z, size * 0.32f, at.y + size * 0.15f, size * 0.5f, 7, needles);
                    b.Cone(at.x, at.z, size * 0.24f, at.y + size * 0.42f, size * 0.42f, 7, needles);
                    b.Cone(at.x, at.z, size * 0.15f, at.y + size * 0.68f, size * 0.32f, 6, needles);
                    break;
                }
                case Tree.Poplar:
                    b.Prism(at.x, at.z, size * 0.04f, at.y, size * 0.25f, 5, trunk, trunk);
                    b.Blob(at + Vector3.up * size * 0.6f, size * 0.16f, size * 0.42f, leaf);
                    break;
                default:
                    b.Prism(at.x, at.z, size * 0.05f, at.y, size * 0.35f, 5, trunk, trunk);
                    b.Blob(at + Vector3.up * size * 0.62f, size * 0.32f, size * 0.3f, leaf);
                    break;
            }
        }
    }
}
