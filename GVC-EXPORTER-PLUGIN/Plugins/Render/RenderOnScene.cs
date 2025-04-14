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
            if (!RenderController.Enabled || Application.ActiveDocument == null || Application.ActiveDocument.IsClear)
                return;
            
            RenderChunk(graphics);
        }

        private void RenderChunk(Graphics graphics)
        {
            var box = new Box(Color.Red, 1, RenderController.chunk.BoundingBox) { render = true, LineWidth = 2 };

            var boxList = new List<Box>();
            foreach (var b in RenderController.chunk.Items)
                boxList.Add(new Box(Color.Green, 1, b.BoundingBox()) { render = true, LineWidth = 1 });

            foreach (var box_ in boxList)
            {
                graphics.LineWidth(box_.LineWidth);
                graphics.Color(box_.color, box.alpha);
                foreach (var (a, b) in box_.edges)
                    graphics.Line(box_.point3Ds[a], box_.point3Ds[b]);
            }

            graphics.LineWidth(box.LineWidth);
            graphics.Color(box.color, box.alpha);

            foreach (var (a, b) in box.edges)
                graphics.Line(box.point3Ds[a], box.point3Ds[b]);
        }
    }

    public static class RenderController
    {
        public static bool Enabled = false;
        public static Chunk chunk = new Chunk();
    }

    public class Box
    {
        public bool render = false;
        public Color color;
        public double alpha = 1.0;
        public int LineWidth = 1;
        public BoundingBox3D box { get; set; }
        public List<Point3D> point3Ds = new List<Point3D>();
        public List<(int, int)> edges = new List<(int, int)>();

        public Box(Color color_, double alpha_, BoundingBox3D box_)
        {
            this.color = color_;
            this.alpha = alpha_;
            this.box = box_;

            if (box_ != null) VertexBuilding();
        }

        internal void VertexBuilding()
        {
            var pointsList = new[]
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

            var edgesList = new[]
            {
                (0,1), (1,2), (2,3), (3,0),
                (4,5), (5,6), (6,7), (7,4),
                (0,4), (1,5), (2,6), (3,7)
            };

            this.point3Ds = new List<Point3D>(pointsList);
            this.edges = new List<(int, int)>(edgesList);
        }
    }
}
