using UnityEngine;
using UnityEngine.UIElements;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Signature pixel highway in front of both live city and optional key art.
    /// Draws lightweight quads; no textures, instantiated game objects or GC
    /// allocations per car, and does not intercept mouse/controller input.
    /// </summary>
    public sealed class MenuHighwayOverlay : VisualElement
    {
        readonly ColorMesh _mesh = new ColorMesh();
        readonly float _trafficSpeed;
        float _trafficTime;
        float _departureElapsed = MenuRoadDeparture.DurationSeconds;
        bool _departing;

        public bool Departing => _departing;
        public float DepartureProgress => MenuRoadDeparture.Progress(_departureElapsed);

        public MenuHighwayOverlay()
        {
            var style = MainMenuStyle.Current;
            _trafficSpeed = style != null ? Mathf.Clamp(style.trafficSpeed, 0f, 2f) : 1f;
            pickingMode = PickingMode.Ignore;
            Ui.Fill(this);
            generateVisualContent += Draw;
        }

        public void BeginDeparture()
        {
            _departureElapsed = 0f;
            _departing = true;
            MarkDirtyRepaint();
        }

        public void Tick(float dt, bool reducedMotion)
        {
            if (dt <= 0f) return;
            if (!reducedMotion) _trafficTime += dt * _trafficSpeed;
            if (_departing)
            {
                _departureElapsed += dt;
                if (_departureElapsed >= MenuRoadDeparture.DurationSeconds)
                {
                    _departureElapsed = MenuRoadDeparture.DurationSeconds;
                    _departing = false;
                }
            }
            if (!reducedMotion || _departing) MarkDirtyRepaint();
        }

        static void PixelCar(ColorMesh mesh, float x, float y, Color paint,
            bool faceRight, float scale = 1f)
        {
            float w = 17f * scale, h = 8f * scale;
            mesh.Rect(new Rect(x - w * 0.5f, y - h * 0.25f, w, h * 0.72f), paint);
            mesh.Rect(new Rect(x - w * 0.25f, y - h * 0.70f, w * 0.52f, h * 0.50f),
                new Color(0.67f, 0.81f, 0.87f));
            var black = new Color(0.055f, 0.065f, 0.09f);
            mesh.Rect(new Rect(x - w * 0.33f, y + h * 0.39f, 3f * scale, 2.7f * scale), black);
            mesh.Rect(new Rect(x + w * 0.23f, y + h * 0.39f, 3f * scale, 2.7f * scale), black);
            mesh.Rect(new Rect(x + (faceRight ? w * 0.40f : -w * 0.5f),
                y - h * 0.11f, 2f * scale, 2f * scale),
                faceRight ? new Color(1f, 0.91f, 0.62f) : new Color(1f, 0.25f, 0.23f));
        }

        static float Seed(int i, int lane)
        {
            // Cheap deterministic offsets so cars remain stable between frames.
            return Mathf.Repeat((i * 0.618034f + lane * 0.317f), 1f);
        }

        void Draw(MeshGenerationContext context)
        {
            var r = contentRect;
            if (r.width < 100f || r.height < 100f) return;
            float y = r.height * 0.82f;
            float h = r.height - y;
            var asphaltTop = new Color(0.065f, 0.088f, 0.117f, 0.96f);
            var asphaltBottom = new Color(0.035f, 0.048f, 0.070f, 0.98f);
            _mesh.Clear();
            _mesh.VerticalGradient(new Rect(0f, y, r.width, h), asphaltTop, asphaltBottom);
            _mesh.Rect(new Rect(0f, y, r.width, 3f),
                new Color(0.74f, 0.78f, 0.77f, 0.85f));
            _mesh.Rect(new Rect(0f, r.height - 3f, r.width, 3f),
                new Color(0.45f, 0.48f, 0.47f, 0.82f));

            // Clear central dashed divider, stronger than the old tiny lamps.
            for (float x = -14f; x < r.width; x += 62f)
                _mesh.Rect(new Rect(x, y + h * 0.5f - 1.4f, 30f, 2.8f),
                    new Color(1f, 0.83f, 0.39f, 0.90f));

            // An explicit off-ramp: four-sided dark ribbon and edge markings.
            float exitX = r.width * 0.74f;
            _mesh.Quad(
                new Vector2(exitX - 13f, y + h * 0.74f),
                new Vector2(exitX + 15f, y + h * 0.74f),
                new Vector2(r.width * 0.84f + 13f, y - 53f),
                new Vector2(r.width * 0.84f - 13f, y - 58f),
                asphaltTop, asphaltTop, asphaltTop, asphaltTop);
            _mesh.Quad(
                new Vector2(exitX - 12f, y + h * 0.74f),
                new Vector2(exitX - 9f, y + h * 0.74f),
                new Vector2(r.width * 0.84f - 9f, y - 56f),
                new Vector2(r.width * 0.84f - 12f, y - 58f),
                Color.white, Color.white, Color.white, Color.white);

            // Pixel cars remain identifiable even in neon windows / daytime.
            for (int lane = 0; lane < 2; lane++)
            {
                bool right = lane == 1;
                int count = lane == 0 ? 10 : 12;
                float cy = y + h * (right ? 0.72f : 0.29f);
                float span = r.width + 120f;
                for (int i = 0; i < count; i++)
                {
                    float progress = Mathf.Repeat(Seed(i, lane) * span +
                        _trafficTime * (right ? 65f : -56f) * (0.8f + Seed(i, 3) * 0.4f), span);
                    float x = progress - 60f;
                    if (x < -18f || x > r.width + 18f) continue;
                    var paint = i % 4 == 0 ? new Color(0.94f, 0.38f, 0.31f) :
                        i % 4 == 1 ? new Color(0.22f, 0.69f, 0.76f) :
                        i % 4 == 2 ? new Color(0.93f, 0.70f, 0.28f) :
                        new Color(0.86f, 0.89f, 0.88f);
                    PixelCar(_mesh, x, cy, paint, right);
                }
            }

            // The exit signal turns green only after New Game is pressed.
            float signalX = r.width * 0.84f + 16f;
            float signalY = y - 56f;
            _mesh.Rect(new Rect(signalX, signalY, 3f, 39f),
                new Color(0.53f, 0.61f, 0.65f));
            _mesh.Rect(new Rect(signalX - 5f, signalY - 17f, 13f, 21f),
                new Color(0.025f, 0.055f, 0.075f));
            _mesh.Rect(new Rect(signalX - 2f, signalY - 14f, 7f, 7f),
                _departing ? new Color(0.16f, 0.98f, 0.42f) :
                    new Color(0.94f, 0.28f, 0.18f));
            if (_departing)
            {
                var p = MenuRoadDeparture.Position(DepartureProgress, r.width, r.height);
                PixelCar(_mesh, p.x, p.y, new Color(1f, 0.79f, 0.31f), true, 1.25f);
            }
            _mesh.Flush(context);
        }
    }
}
