using UnityEngine;
using UnityEngine.UIElements;

namespace CityHighways.Presentation
{
    /// <summary>Original schematic side-view garage thumbnails; not imported vehicle art.</summary>
    public sealed class FleetVehiclePreview : VisualElement
    {
        FleetLivery livery;
        readonly ProfileBadge badge;
        public FleetVehiclePreview()
        {
            pickingMode = PickingMode.Ignore;
            style.height = 98f;
            style.width = 184f;
            style.flexShrink = 0f;
            style.backgroundColor = new Color(.10f,.14f,.19f);
            Ui.Radius(this, 8f);
            badge = new ProfileBadge(16f);
            badge.style.position = Position.Absolute;
            badge.style.left = 47f;
            badge.style.top = 44f;
            Add(badge);
            generateVisualContent += Draw;
        }

        public void Set(FleetLivery value)
        {
            livery = value;
            if (value == null) badge.Set("", Color.white);
            else
            {
                var color = value.Primary;
                float luminance = .2126f * color.r + .7152f * color.g + .0722f * color.b;
                badge.Set(value.Emblem, luminance > .55f ? new Color(.10f,.12f,.15f) : Color.white);
            }
            MarkDirtyRepaint();
        }

        void Draw(MeshGenerationContext ctx)
        {
            if (livery == null || contentRect.width <= 0f || contentRect.height <= 0f) return;
            var p = ctx.painter2D;
            float scale = Mathf.Min(contentRect.width / 184f, contentRect.height / 98f);
            Vector2 origin = contentRect.center - new Vector2(92f,49f) * scale;
            Vector2 P(float x,float y) => origin + new Vector2(x,y) * scale;
            void Shape(Color color, params Vector2[] points)
            {
                p.fillColor = color; p.BeginPath();
                p.MoveTo(P(points[0].x,points[0].y));
                for (int i=1;i<points.Length;i++) p.LineTo(P(points[i].x,points[i].y));
                p.ClosePath(); p.Fill();
            }
            void Rect(Color color,float x,float y,float w,float h) =>
                Shape(color,new Vector2(x,y),new Vector2(x+w,y),new Vector2(x+w,y+h),new Vector2(x,y+h));
            void Circle(Color color,float x,float y,float r)
            {
                p.fillColor = color; p.BeginPath();
                p.Arc(P(x,y),r*scale,Angle.Degrees(0),Angle.Degrees(360)); p.Fill();
            }
            var main = livery.Primary; var accent = livery.Secondary;
            var dark = new Color(.08f,.10f,.12f);
            var steel = new Color(.43f,.50f,.56f);
            var glass = new Color(.58f,.78f,.85f);
            void Wheel(float x,float y,float radius)
            {
                Circle(dark,x,y,radius); Circle(steel,x,y,radius*.48f); Circle(dark,x,y,radius*.19f);
            }
            Rect(steel,16,85,152,1);
            // Common cab at x=47 badge anchor; mechanical silhouettes distinguish all four vehicles.
            switch (livery.Vehicle)
            {
                case "paver":
                    Rect(dark,27,64,100,17);
                    foreach(float x in new[]{39f,58f,77f,96f,115f}) Wheel(x,72,8);
                    Rect(main,31,43,86,20); Rect(main,45,24,30,24);
                    Rect(glass,49,28,21,13); Rect(accent,31,58,86,5);
                    Shape(main,new Vector2(117,43),new Vector2(158,39),new Vector2(146,65),new Vector2(117,65));
                    Rect(steel,12,71,26,8); break;
                case "mixer":
                    Rect(dark,29,65,123,8); Rect(main,31,33,45,32);
                    Rect(glass,35,36,26,13); Rect(accent,32,58,43,5);
                    Shape(main,new Vector2(81,31),new Vector2(114,23),new Vector2(141,40),new Vector2(129,61),new Vector2(95,64),new Vector2(77,49));
                    Shape(accent,new Vector2(94,28),new Vector2(105,25),new Vector2(132,56),new Vector2(124,61));
                    Rect(steel,140,52,16,6);
                    foreach(float x in new[]{47f,119f,143f}) Wheel(x,74,11); break;
                case "grader":
                    Rect(dark,29,60,130,8); Rect(main,31,42,44,21);
                    Rect(main,43,20,27,25); Rect(glass,47,24,19,15);
                    Rect(accent,31,58,116,6); Rect(main,77,47,74,10);
                    Shape(steel,new Vector2(84,62),new Vector2(117,69),new Vector2(112,79),new Vector2(78,72));
                    foreach(float x in new[]{38f,62f,146f}) Wheel(x,73,11); break;
                case "crane":
                    Rect(dark,26,65,133,8); Rect(main,31,33,45,32); Rect(glass,35,36,26,13);
                    Rect(accent,31,58,125,5); Rect(main,83,44,53,19);
                    Shape(main,new Vector2(94,43),new Vector2(125,13),new Vector2(164,13),new Vector2(164,20),new Vector2(132,20),new Vector2(109,44));
                    Rect(accent,128,15,34,4); Rect(steel,160,20,2,30);
                    Rect(dark,157,49,8,7);
                    foreach(float x in new[]{46f,116f,144f}) Wheel(x,74,11); break;
            }
        }
    }
}
