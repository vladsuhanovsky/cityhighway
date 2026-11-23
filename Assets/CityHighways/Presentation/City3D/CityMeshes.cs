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
        readonly List<Vector3> _vertices;
        readonly List<Vector3> _normals;
        readonly List<Vector2> _uvs;
        readonly List<int> _triangles;

        /// <summary>
        /// Reserve space for predictable mesh counts. A reused builder keeps
        /// these buffers across Clear(), avoiding repeated managed allocations.
        /// </summary>
        public MeshBuilder(int expectedQuads = 0)
        {
            int quads = Mathf.Max(0, expectedQuads);
            _vertices = new List<Vector3>(quads * 4);
            _normals = new List<Vector3>(quads * 4);
            _uvs = new List<Vector2>(quads * 4);
            _triangles = new List<int>(quads * 6);
        }

        public int VertexCount => _vertices.Count;

        /// <summary>
        /// Положить построенное на рельеф: к высоте каждой вершины прибавить высоту земли под ней (для плоских
        /// вещей — земли, перекрёстков, декалей). Нормали остаются как были: склоны пологие.
        /// </summary>
        public void Drape(System.Func<float, float, float> height)
        {
            for (int i = 0; i < _vertices.Count; i++)
            {
                var v = _vertices[i];
                v.y += height(v.x, v.z);
                _vertices[i] = v;
            }
        }

        /// <summary>Четырёхугольник a-b-c-d по часовой стрелке, если смотреть на лицевую сторону (так Unity видит грань).</summary>
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Rect uv)
        {
            // Нормаль по диагоналям: не обнуляется, если у грани схлопнуто одно ребро (верх конуса, конёк крыши).
            // Совсем вырожденную грань пропускаем — нулевая нормаль даёт NaN в освещении, а Bloom
            // раздувает такой пиксель во вспышку на весь экран.
            var cross = Vector3.Cross(c - a, d - b);
            if (!(cross.sqrMagnitude > 1e-14f)) return;
            var normal = cross.normalized;
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

        /// <summary>Четырёхугольник с заданной нормалью (карточки деревьев освещаются как кроны, а не как плоскость).</summary>
        public void QuadWithNormal(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Rect uv, Vector3 normal)
        {
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
            var cross = Vector3.Cross(b - a, c - a);
            if (!(cross.sqrMagnitude > 1e-14f)) return;
            var normal = cross.normalized;
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
                _normals.Add(SafeNormal(normalMatrix.MultiplyVector(other._normals[i])));
                _uvs.Add(other._uvs[i]);
            }
            foreach (var t in other._triangles) _triangles.Add(t + offset);
        }

        /// <summary>Нормаль без NaN и нулей (матрица с нулевым масштабом или вырожденная грань) — иначе вверх.</summary>
        static Vector3 SafeNormal(Vector3 n)
        {
            float m = n.sqrMagnitude;
            if (!(m > 1e-14f) || float.IsInfinity(m)) return Vector3.up;
            return n / Mathf.Sqrt(m);
        }

        public void Clear()
        {
            _vertices.Clear();
            _normals.Clear();
            _uvs.Clear();
            _triangles.Clear();
        }

        /// <summary>Переписать готовый меш (карточки деревьев пересобираются при смене угла камеры).</summary>
        public void WriteTo(Mesh mesh)
        {
            mesh.Clear();
            mesh.indexFormat = _vertices.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(_vertices);
            mesh.SetNormals(_normals);
            mesh.SetUVs(0, _uvs);
            mesh.SetTriangles(_triangles, 0);
            mesh.RecalculateBounds();
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

        /// <summary>
        /// Китайская вальмовая крыша с загнутыми вверх углами: длинный конёк, углы карниза приподняты на lift,
        /// середины карнизов — ниже (провис ската). Сверху — тёмный гребень конька с «рогами» на концах.
        /// w × d — с выносом карниза (больше стен), rh — высота до конька.
        /// </summary>
        public void ChineseRoof(float cx, float cz, float w, float d, float y0, float rh, float lift, Rect roof, Rect ridge)
        {
            if (w < d)
            {
                // Строим вдоль X и поворачиваем на 90° вокруг центра.
                var turned = new MeshBuilder();
                turned.ChineseRoof(0f, 0f, d, w, y0, rh, lift, roof, ridge);
                Append(turned, Matrix4x4.TRS(new Vector3(cx, 0f, cz), Quaternion.Euler(0f, 90f, 0f), Vector3.one));
                return;
            }
            float x0 = cx - w / 2f, x1 = cx + w / 2f, z0 = cz - d / 2f, z1 = cz + d / 2f, top = y0 + rh;
            float inset = d / 2f * 0.8f;                     // конёк длиннее, чем у обычной вальмы
            var ra = new Vector3(x0 + inset, top, cz);
            var rb = new Vector3(x1 - inset, top, cz);
            var A = new Vector3(x0, y0 + lift, z0); var B = new Vector3(x1, y0 + lift, z0);
            var C = new Vector3(x1, y0 + lift, z1); var E = new Vector3(x0, y0 + lift, z1);
            var F1 = new Vector3(x0 + w * 0.25f, y0, z0); var F2 = new Vector3(x1 - w * 0.25f, y0, z0);
            var K1 = new Vector3(x1 - w * 0.25f, y0, z1); var K2 = new Vector3(x0 + w * 0.25f, y0, z1);
            var L = new Vector3(x0, y0 + lift * 0.25f, cz); var R = new Vector3(x1, y0 + lift * 0.25f, cz);
            // Передний и задний скаты (трапеции с провисом посередине), боковые — треугольники.
            Tri(A, ra, F1, roof); Quad(F1, ra, rb, F2, roof); Tri(F2, rb, B, roof);
            Tri(C, rb, K1, roof); Quad(K1, rb, ra, K2, roof); Tri(K2, ra, E, roof);
            Tri(E, ra, L, roof); Tri(L, ra, A, roof);
            Tri(B, rb, R, roof); Tri(R, rb, C, roof);
            // Гребень конька и загнутые «рога» на его концах.
            float t = Mathf.Max(0.0012f, rh * 0.08f);
            Beam(ra + Vector3.up * t * 0.4f, rb + Vector3.up * t * 0.4f, t, t, ridge, ridge);
            Beam(ra + Vector3.up * t * 0.4f, ra + new Vector3(-t * 1.2f, t * 2.2f, 0f), t * 0.8f, t * 0.8f, ridge, ridge);
            Beam(rb + Vector3.up * t * 0.4f, rb + new Vector3(t * 1.2f, t * 2.2f, 0f), t * 0.8f, t * 0.8f, ridge, ridge);
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
            Block, Parapet, Gable, Hip, Stepped, Spire, Round, Landmark, LBlock, Sawtooth, Factory, Hangar, Warehouse, Panel,
            /// <summary>Вокзал: центральный зал под двускатной крышей, два крыла, часовая башня со шпилем, колонны у входа.</summary>
            Station,
            /// <summary>Аэровокзал: длинный стеклянный терминал, навес над подъездом, пирс к самолётам.</summary>
            Terminal,
            /// <summary>Диспетчерская вышка аэропорта: ствол и стеклянная кабина наверху.</summary>
            ControlTower,

            // ---- Китайский стиль (Чунцин).
            /// <summary>Дом под китайской крышей с загнутыми углами: белёные или красные стены, красные колонны.</summary>
            ChineseHouse,
            /// <summary>Зал с двойным карнизом: нижний «юбочный» скат вокруг и верхняя крыша.</summary>
            ChineseHall,
            /// <summary>Жилой дом в 5–9 этажей с черепичной китайской крышей и красным поясом.</summary>
            ChineseBlock,
            /// <summary>Высотка Чунцина: стеклянная башня, уступ и павильон под китайской крышей наверху.</summary>
            ChineseTower,
            /// <summary>Пагода: ярусы с выносными крышами и золотое навершие.</summary>
            Pagoda,

            // ---- Европейский стиль (Амстердам).
            /// <summary>Ряд узких кирпичных домов на канале: у каждого свой фронтон — острый, ступенчатый или шейный.</summary>
            CanalRow,
            /// <summary>Кирпичный дом в 4–6 этажей под крутой мансардной крышей из тёмного сланца.</summary>
            EuroBlock,
            /// <summary>Церковь: неф под высокой крышей и башня со ступенями и медным шпилем.</summary>
            Church
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
                    RoofClutter(b, w * 0.8f, d * 0.8f, h + 0.008f, random);
                    break;
                case Kind.Gable:
                    b.Box(0f, 0f, w, d, 0f, h, Facade(CityAtlas.Row.Low), flatRoof);
                    b.GableRoof(0f, 0f, w * 1.06f, d * 1.06f, h, Mathf.Min(w, d) * 0.45f,
                        Roof(random.NextDouble() < 0.6 ? CityAtlas.RoofRed : CityAtlas.RoofBrown), Facade(CityAtlas.Row.Low));
                    Yard(b, w, d, random);
                    break;
                case Kind.Hip:
                    b.Box(0f, 0f, w, d, 0f, h, Facade(CityAtlas.Row.Low), flatRoof);
                    b.HipRoof(0f, 0f, w * 1.06f, d * 1.06f, h, Mathf.Min(w, d) * 0.38f,
                        Roof(random.NextDouble() < 0.5 ? CityAtlas.RoofBrown : CityAtlas.RoofCopper));
                    Yard(b, w, d, random);
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
                case Kind.Station:
                {
                    // Фасад смотрит на −Z (к станции и дороге). Тёплый камень — как у старых вокзалов.
                    var stone = CityAtlas.Uv(CityAtlas.Row.Mid, random.NextDouble() < 0.5 ? 4 : 5);
                    var copper = Roof(CityAtlas.RoofCopper);
                    float hallW = w * 0.46f, wingW = w * 0.27f;
                    b.Box(0f, 0f, hallW, d, 0f, h, stone, flatRoof);
                    b.GableRoof(0f, 0f, hallW * 1.04f, d * 1.04f, h, d * 0.42f, copper, stone);
                    for (int s = -1; s <= 1; s += 2)
                    {
                        b.Box(s * (hallW + wingW) / 2f, d * 0.06f, wingW, d * 0.82f, 0f, h * 0.72f, stone, flatRoof);
                        b.HipRoof(s * (hallW + wingW) / 2f, d * 0.06f, wingW * 1.04f, d * 0.86f, h * 0.72f, d * 0.22f, Roof(CityAtlas.RoofBrown));
                    }
                    // Часовая башня над входом и золотой шпиль.
                    float tower = Mathf.Min(w, d) * 0.3f;
                    b.Box(0f, -d * 0.36f, tower, tower, 0f, h * 2.1f, stone, copper);
                    b.Box(0f, -d * 0.36f, tower * 1.12f, tower * 1.12f, h * 2.1f, h * 0.12f, stone, copper);
                    b.Cone(0f, -d * 0.36f, tower * 0.42f, h * 2.22f, h * 0.9f, 6, CityAtlas.Solid(CityAtlas.Row.Misc, CityAtlas.Spire));
                    // Колоннада у входа.
                    var column = CityAtlas.Solid(CityAtlas.Row.Misc, CityAtlas.Railing);
                    for (int i = -2; i <= 2; i++)
                        if (i != 0) b.Prism(i * hallW * 0.17f, -d * 0.56f, d * 0.035f, 0f, h * 0.8f, 6, column, column);
                    b.Box(0f, -d * 0.56f, hallW * 0.82f, d * 0.12f, h * 0.8f, h * 0.08f, stone, copper);
                    break;
                }
                case Kind.Terminal:
                {
                    var glassFacade = CityAtlas.Uv(CityAtlas.Row.Tall, random.Next(3));
                    var metal = Roof(CityAtlas.RoofMetal);
                    var light = Roof(CityAtlas.RoofLight);
                    // Двухъярусный терминал: стеклянный корпус и широкая светлая кровля с выносом.
                    b.Box(0f, 0f, w, d, 0f, h, glassFacade, metal);
                    b.Box(0f, 0f, w * 1.04f, d * 1.18f, h, h * 0.12f, light, light);
                    b.Box(0f, d * 0.05f, w * 0.5f, d * 0.6f, h * 1.12f, h * 0.35f, glassFacade, metal);
                    // Навес над подъездом (−Z) на тонких опорах.
                    var post = CityAtlas.Solid(CityAtlas.Row.Misc, CityAtlas.Railing);
                    b.Box(0f, -d * 0.72f, w * 0.8f, d * 0.3f, h * 0.55f, h * 0.06f, light, light);
                    for (int i = -3; i <= 3; i++)
                        b.Prism(i * w * 0.12f, -d * 0.82f, d * 0.02f, 0f, h * 0.55f, 4, post, post);
                    // Пирс с телетрапами к самолётам (+Z).
                    b.Box(0f, d * 0.95f, w * 0.12f, d * 0.9f, 0f, h * 0.55f, glassFacade, metal);
                    for (int s = -1; s <= 1; s += 2)
                        b.Box(s * w * 0.12f, d * 1.25f, w * 0.14f, d * 0.12f, h * 0.25f, h * 0.18f, light, light);
                    break;
                }
                case Kind.ControlTower:
                {
                    float r = Mathf.Min(w, d) / 2f;
                    var concrete = CityAtlas.Solid(CityAtlas.Row.Misc, CityAtlas.BridgeConcrete);
                    var glassFacade = CityAtlas.Uv(CityAtlas.Row.Tall, 2);
                    b.Prism(0f, 0f, r * 0.45f, 0f, h * 0.82f, 8, concrete, concrete);
                    b.Prism(0f, 0f, r * 0.75f, h * 0.82f, h * 0.04f, 8, concrete, concrete);
                    b.Prism(0f, 0f, r, h * 0.86f, h * 0.1f, 8, glassFacade, Roof(CityAtlas.RoofDark), r * 1.08f);
                    b.Prism(0f, 0f, r * 1.08f, h * 0.96f, h * 0.03f, 8, Roof(CityAtlas.RoofDark), Roof(CityAtlas.RoofDark), r * 0.5f);
                    b.Prism(0f, 0f, r * 0.06f, h * 0.99f, h * 0.12f, 4, Roof(CityAtlas.RoofMetal), Roof(CityAtlas.RoofMetal));
                    break;
                }
                case Kind.Panel:
                    // Панельный дом: длинная плита, лестничные выходы на крышу.
                    b.Box(0f, 0f, w, d, 0f, h, CityAtlas.Uv(CityAtlas.Row.Mid, col), flatRoof);
                    for (int i = -1; i <= 1; i += 2)
                        b.Box(w * 0.3f * i, 0f, Mathf.Min(w, d) * 0.22f, Mathf.Min(w, d) * 0.22f, h, 0.02f, Roof(CityAtlas.RoofLight), Roof(CityAtlas.RoofLight));
                    RoofClutter(b, w * 0.85f, d * 0.7f, h, random);
                    break;
                case Kind.ChineseHouse:
                case Kind.ChineseHall:
                case Kind.ChineseBlock:
                case Kind.ChineseTower:
                case Kind.Pagoda:
                    BuildChinese(b, kind, w, d, h, random);
                    break;
                case Kind.CanalRow:
                case Kind.EuroBlock:
                case Kind.Church:
                    BuildEuropean(b, kind, w, d, h, random);
                    break;
                default:
                    b.Box(0f, 0f, w, d, 0f, h, Facade(lowRow), flatRoof);
                    break;
            }
        }

        static Rect StyleUv(int column) => CityAtlas.Uv(CityAtlas.Row.Style, column);
        static Rect StyleSolid(int column) => CityAtlas.Solid(CityAtlas.Row.Style, column);

        /// <summary>Китайские здания: стены, красные колонны, крыши с загнутыми углами из глазурованной черепицы.</summary>
        static void BuildChinese(MeshBuilder b, Kind kind, float w, float d, float h, System.Random random)
        {
            var tile = StyleUv(random.NextDouble() < 0.7 ? CityAtlas.ChinaTileGray : CityAtlas.ChinaTileGreen);
            var ridge = CityAtlas.Solid(CityAtlas.Row.Roof, CityAtlas.RoofBlack);
            var lacquer = StyleSolid(CityAtlas.Lacquer);
            var wall = StyleUv(random.NextDouble() < 0.6 ? CityAtlas.ChinaWhite : CityAtlas.ChinaRed);
            var plinth = CityAtlas.Solid(CityAtlas.Row.Misc, CityAtlas.Granite);
            var gold = CityAtlas.Solid(CityAtlas.Row.Misc, CityAtlas.Spire);
            switch (kind)
            {
                case Kind.ChineseHouse:
                {
                    // Каменный цоколь, стены, четыре красные колонны по углам, крыша с выносом и загнутыми углами.
                    b.Box(0f, 0f, w * 1.08f, d * 1.08f, 0f, h * 0.12f, plinth, plinth);
                    b.Box(0f, 0f, w, d, h * 0.12f, h * 0.88f, wall, tile);
                    float r = Mathf.Min(w, d) * 0.035f;
                    for (int k = 0; k < 4; k++)
                        b.Prism(((k & 1) == 0 ? -1f : 1f) * w * 0.5f, ((k & 2) == 0 ? -1f : 1f) * d * 0.5f, r, h * 0.12f, h * 0.88f, 6, lacquer, lacquer);
                    float rh = Mathf.Min(w, d) * 0.34f;
                    b.ChineseRoof(0f, 0f, w * 1.28f, d * 1.34f, h, rh, rh * 0.38f, tile, ridge);
                    break;
                }
                case Kind.ChineseHall:
                {
                    // Двойной карниз: нижний скат вокруг на 60 % высоты, над ним — верхний ярус и большая крыша.
                    b.Box(0f, 0f, w * 1.1f, d * 1.1f, 0f, h * 0.1f, plinth, plinth);
                    b.Box(0f, 0f, w, d, h * 0.1f, h * 0.55f, StyleUv(CityAtlas.ChinaRed), tile);
                    float skirt = Mathf.Min(w, d) * 0.16f;
                    b.ChineseRoof(0f, 0f, w * 1.24f, d * 1.28f, h * 0.65f, skirt, skirt * 0.35f, tile, ridge);
                    b.Box(0f, 0f, w * 0.78f, d * 0.72f, h * 0.65f, h * 0.35f, StyleUv(CityAtlas.ChinaRed), tile);
                    float rh = Mathf.Min(w, d) * 0.3f;
                    b.ChineseRoof(0f, 0f, w * 1.0f, d * 0.98f, h, rh, rh * 0.4f, tile, ridge);
                    break;
                }
                case Kind.ChineseBlock:
                {
                    // Жилой дом: обычный фасад, красный пояс под крышей и низкая китайская крыша.
                    var facade = CityAtlas.Uv(h > 0.16f ? CityAtlas.Row.Mid : CityAtlas.Row.Low, random.Next(CityAtlas.Columns));
                    b.Box(0f, 0f, w, d, 0f, h, facade, tile);
                    b.Box(0f, 0f, w * 1.02f, d * 1.02f, h, h * 0.05f, lacquer, lacquer);
                    float rh = Mathf.Min(w, d) * 0.22f;
                    b.ChineseRoof(0f, 0f, w * 1.16f, d * 1.2f, h * 1.05f, rh, rh * 0.35f, tile, ridge);
                    break;
                }
                case Kind.ChineseTower:
                {
                    // Высотка: стеклянный ствол, уступ, павильон с китайской крышей и шпилем.
                    var glass = CityAtlas.Uv(CityAtlas.Row.Tall, random.Next(3));
                    b.Box(0f, 0f, w, d, 0f, h * 0.82f, glass, CityAtlas.Solid(CityAtlas.Row.Roof, CityAtlas.RoofDark));
                    b.Box(0f, 0f, w * 0.78f, d * 0.78f, h * 0.82f, h * 0.1f, glass, lacquer);
                    float rh = Mathf.Min(w, d) * 0.3f;
                    b.ChineseRoof(0f, 0f, w * 0.98f, d * 0.98f, h * 0.92f, rh, rh * 0.4f, tile, ridge);
                    b.Cone(0f, 0f, Mathf.Min(w, d) * 0.06f, h * 0.92f + rh, h * 0.12f, 6, gold);
                    break;
                }
                case Kind.Pagoda:
                {
                    // Ярусы: каждый уже и ниже предыдущего, у каждого — своя крыша с загнутыми углами.
                    int tiers = h > 0.2f ? 5 : 3;
                    float side = Mathf.Min(w, d), y = 0f;
                    b.Box(0f, 0f, side * 1.15f, side * 1.15f, 0f, h * 0.05f, plinth, plinth);
                    y = h * 0.05f;
                    float tierH = h * 0.95f / (tiers + 0.6f);
                    for (int i = 0; i < tiers; i++)
                    {
                        float s = side * (1f - i * 0.13f);
                        float wallH = tierH * (i == 0 ? 1.2f : 0.75f);
                        b.Box(0f, 0f, s * 0.72f, s * 0.72f, y, wallH, StyleUv(CityAtlas.ChinaRed), tile);
                        y += wallH;
                        float rh = s * 0.16f;
                        b.ChineseRoof(0f, 0f, s * 1.08f, s * 1.08f, y, rh, rh * 0.5f, tile, ridge);
                        y += rh * 0.6f;
                    }
                    b.Prism(0f, 0f, side * 0.03f, y, tierH * 0.3f, 6, gold, gold);
                    b.Cone(0f, 0f, side * 0.05f, y + tierH * 0.3f, tierH * 0.7f, 6, gold);
                    break;
                }
            }
        }

        /// <summary>Европейские здания: кирпич, белые рамы и карнизы, фронтоны, тёмный сланец и медь.</summary>
        static void BuildEuropean(MeshBuilder b, Kind kind, float w, float d, float h, System.Random random)
        {
            var white = CityAtlas.Solid(CityAtlas.Row.Misc, CityAtlas.Railing);
            var slate = CityAtlas.Solid(CityAtlas.Row.Roof, CityAtlas.RoofBlack);
            var tiles = CityAtlas.Solid(CityAtlas.Row.Roof, random.NextDouble() < 0.5 ? CityAtlas.RoofBrown : CityAtlas.RoofRed);
            int[] bricks = { CityAtlas.CanalBrown, CityAtlas.CanalRed, CityAtlas.CanalDark };
            switch (kind)
            {
                case Kind.CanalRow:
                {
                    // Ряд узких домов вплотную, фронтонами к улице (−Z): у каждого своя высота, кирпич и фронтон.
                    int count = Mathf.Clamp(Mathf.RoundToInt(w / 0.034f), 2, 5);
                    float segment = w / count;
                    for (int i = 0; i < count; i++)
                    {
                        float x = -w / 2f + segment * (i + 0.5f);
                        float hi = h * (0.82f + (float)random.NextDouble() * 0.36f);
                        var brick = StyleUv(bricks[random.Next(bricks.Length)]);
                        var roof = random.NextDouble() < 0.6 ? slate : tiles;
                        float sw = segment * 0.98f, gable = sw * 0.75f;
                        b.Box(x, 0f, sw, d, 0f, hi, brick, roof);
                        // Скат вдоль глубины: фронтон смотрит на улицу.
                        b.GableRoof(x, 0f, sw, d, hi, gable, roof, brick);
                        int shape = random.Next(3);
                        float front = -d / 2f + 0.002f;
                        if (shape == 1)
                        {
                            // Ступенчатый фронтон: три уступа поверх треугольника.
                            for (int step = 0; step < 3; step++)
                            {
                                float stepW = sw * (0.84f - step * 0.27f), stepH = gable * 0.34f;
                                b.Box(x, front, stepW, 0.004f, hi + step * stepH, stepH, brick, white);
                            }
                        }
                        else if (shape == 2)
                        {
                            // Шейный фронтон: узкая «шея» с белым карнизом и завитками-плечиками.
                            b.Box(x, front, sw * 0.46f, 0.004f, hi, gable * 0.95f, brick, white);
                            b.Box(x, front, sw * 0.54f, 0.005f, hi + gable * 0.95f, gable * 0.08f, white, white);
                            b.Box(x, front, sw * 0.86f, 0.004f, hi, gable * 0.22f, brick, white);
                        }
                        // Белый пояс-карниз над первым этажом.
                        b.Box(x, front - 0.0005f, sw * 0.9f, 0.002f, hi * 0.22f, 0.0012f, white, white);
                    }
                    break;
                }
                case Kind.EuroBlock:
                {
                    // Кирпичный дом, белый карниз и крутая мансардная крыша (вальма) из тёмного сланца со слуховыми окнами.
                    var brick = StyleUv(bricks[random.Next(bricks.Length)]);
                    b.Box(0f, 0f, w, d, 0f, h, brick, slate);
                    b.Box(0f, 0f, w * 1.03f, d * 1.03f, h, h * 0.04f, white, white);
                    float rh = Mathf.Min(w, d) * 0.4f;
                    b.HipRoof(0f, 0f, w, d, h * 1.04f, rh, slate);
                    int dormers = Mathf.Clamp(Mathf.RoundToInt(w / 0.03f), 1, 4);
                    for (int i = 0; i < dormers; i++)
                    {
                        float x = -w * 0.35f + (dormers == 1 ? w * 0.35f : w * 0.7f * i / (dormers - 1));
                        b.Box(x, -d * 0.3f, w * 0.08f, d * 0.12f, h * 1.04f, rh * 0.45f, white, slate);
                    }
                    break;
                }
                case Kind.Church:
                {
                    // Неф вдоль глубины под высокой крышей, башня у фасада (−Z): кирпич, белый ярус, медный шпиль.
                    var brick = StyleUv(CityAtlas.CanalRed);
                    var copper = CityAtlas.Solid(CityAtlas.Row.Roof, CityAtlas.RoofCopper);
                    float naveW = w * 0.55f;
                    b.Box(0f, d * 0.1f, naveW, d * 0.8f, 0f, h, brick, slate);
                    b.GableRoof(0f, d * 0.1f, naveW * 1.04f, d * 0.82f, h, naveW * 0.7f, slate, brick);
                    float tower = Mathf.Min(w, d) * 0.3f, ty = 0f;
                    b.Box(0f, -d * 0.36f, tower, tower, 0f, h * 1.7f, brick, slate);
                    ty = h * 1.7f;
                    b.Box(0f, -d * 0.36f, tower * 0.8f, tower * 0.8f, ty, h * 0.45f, white, copper);
                    ty += h * 0.45f;
                    b.Prism(0f, -d * 0.36f, tower * 0.34f, ty, h * 0.35f, 8, copper, copper);
                    ty += h * 0.35f;
                    b.Cone(0f, -d * 0.36f, tower * 0.3f, ty, h * 1.1f, 8, copper);
                    b.Cone(0f, -d * 0.36f, tower * 0.05f, ty + h * 1.1f, h * 0.25f, 4, CityAtlas.Solid(CityAtlas.Row.Misc, CityAtlas.Spire));
                    break;
                }
            }
        }

        /// <summary>
        /// Мелочи на плоской крыше: кондиционеры, бак для воды на ножках, антенна. Ставятся в прямоугольнике
        /// w × d вокруг центра крыши на высоте roof.
        /// </summary>
        static void RoofClutter(MeshBuilder b, float w, float d, float roof, System.Random random)
        {
            var metal = CityAtlas.Solid(CityAtlas.Row.Roof, CityAtlas.RoofMetal);
            var light = CityAtlas.Solid(CityAtlas.Row.Roof, CityAtlas.RoofLight);
            int units = 1 + random.Next(3);
            for (int i = 0; i < units; i++)
            {
                float x = ((float)random.NextDouble() - 0.5f) * w, z = ((float)random.NextDouble() - 0.5f) * d;
                b.Box(x, z, 0.006f, 0.005f, roof, 0.0035f, light, metal);
            }
            if (random.NextDouble() < 0.35)
            {
                float x = ((float)random.NextDouble() - 0.5f) * w * 0.6f, z = ((float)random.NextDouble() - 0.5f) * d * 0.6f;
                var steel = CityAtlas.Solid(CityAtlas.Row.Misc, CityAtlas.BridgeSteel);
                for (int k = 0; k < 4; k++)
                    b.Box(x + ((k & 1) == 0 ? -0.003f : 0.003f), z + ((k & 2) == 0 ? -0.003f : 0.003f), 0.0008f, 0.0008f, roof, 0.006f, steel, steel);
                b.Prism(x, z, 0.0045f, roof + 0.006f, 0.007f, 8, CityAtlas.Solid(CityAtlas.Row.Roof, CityAtlas.RoofBrown), CityAtlas.Solid(CityAtlas.Row.Roof, CityAtlas.RoofDark));
            }
            if (random.NextDouble() < 0.3)
            {
                float x = ((float)random.NextDouble() - 0.5f) * w, z = ((float)random.NextDouble() - 0.5f) * d;
                b.Prism(x, z, 0.0005f, roof, 0.022f, 4, metal, metal);
            }
        }

        /// <summary>
        /// Двор у частного дома: низкий штакетник вокруг (калитка со стороны −Z) и иногда сарайчик в углу.
        /// Только у небольших домов — у больших двор в планировке не помещается.
        /// </summary>
        static void Yard(MeshBuilder b, float w, float d, System.Random random)
        {
            if (w * d > 0.0045f || random.NextDouble() > 0.6) return;
            var wood = CityAtlas.Solid(CityAtlas.Row.Roof, CityAtlas.RoofBrown);
            float hx = w / 2f + 0.011f, hz = d / 2f + 0.011f, height = 0.005f, t = 0.0011f;
            b.Box(0f, hz, hx * 2f, t, 0f, height, wood, wood);
            b.Box(-hx, 0f, t, hz * 2f, 0f, height, wood, wood);
            b.Box(hx, 0f, t, hz * 2f, 0f, height, wood, wood);
            // Спереди — две секции с калиткой посередине.
            float gate = Mathf.Min(0.014f, hx * 0.5f), part = hx - gate / 2f;
            b.Box(-hx + part / 2f, -hz, part, t, 0f, height, wood, wood);
            b.Box(hx - part / 2f, -hz, part, t, 0f, height, wood, wood);
            if (random.NextDouble() < 0.5)
            {
                float sx = (random.NextDouble() < 0.5 ? -1f : 1f) * (hx - 0.006f);
                b.Box(sx, hz - 0.006f, 0.008f, 0.007f, 0f, 0.006f, CityAtlas.Uv(CityAtlas.Row.Low, 0), CityAtlas.Solid(CityAtlas.Row.Roof, CityAtlas.RoofDark));
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
