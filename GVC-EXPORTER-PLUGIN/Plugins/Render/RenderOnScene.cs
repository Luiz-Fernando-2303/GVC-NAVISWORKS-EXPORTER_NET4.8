using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.Plugins;
using GVC_EXPORTER_PLUGIN.Functions;
using System.Collections.Generic;

namespace GVC_EXPORTER_PLUGIN.Plugins.Render
{
    public class RenderOnScene : RenderPlugin
    {
        public override void Render(View view, Graphics graphics)
        {
            if (!RenderController.Enabled || 
                Application.ActiveDocument == null || 
                Application.ActiveDocument.IsClear)
                return;

            RenderBoundingBoxes(graphics);
        }

        private void RenderBoundingBoxes(Graphics graphics)
        {
            DrawBoundingBox(graphics, RenderController.chunk.BoundingBox, Color.Red, 2);

            foreach (var item in RenderController.chunk.Items)
            {
                var itemBox = item.BoundingBox();
                DrawBoundingBox(graphics, itemBox, Color.Blue, 1);
            }
        }

        private void DrawBoundingBox(Graphics graphics, BoundingBox3D box, Color color, int lineWidth)
        {
            if (box == null) return;

            var points = GetBoxVertices(box);
            var edges = GetBoxEdges();

            graphics.LineWidth(lineWidth);
            graphics.Color(color, 1.0);

            foreach (var (a, b) in edges)
            {
                graphics.Line(points[a], points[b]);
            }
        }

        private List<Point3D> GetBoxVertices(BoundingBox3D box)
        {
            return new List<Point3D>
            {
                box.Min,
                new Point3D(box.Max.X, box.Min.Y, box.Min.Z),
                new Point3D(box.Max.X, box.Max.Y, box.Min.Z),
                new Point3D(box.Min.X, box.Max.Y, box.Min.Z),
                new Point3D(box.Min.X, box.Min.Y, box.Max.Z),
                new Point3D(box.Max.X, box.Min.Y, box.Max.Z),
                box.Max,
                new Point3D(box.Min.X, box.Max.Y, box.Max.Z)
            };
        }

        private List<(int, int)> GetBoxEdges()
        {
            return new List<(int, int)>
            {
                (0, 1), (1, 2), (2, 3), (3, 0), // base
                (4, 5), (5, 6), (6, 7), (7, 4), // top
                (0, 4), (1, 5), (2, 6), (3, 7)  // verticals
            };
        }
    }

    public static class RenderController
    {
        public static bool Enabled = false;
        public static Chunk chunk = new Chunk();
    }
}
