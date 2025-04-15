using Autodesk.Navisworks.Api;
using System.Collections.Generic;

namespace GVC_EXPORTER_PLUGIN.Functions
{
    public class PointCloud_
    {
        public ModelItemCollection Items = new ModelItemCollection();
        public List<Point3D> Point = new List<Point3D>();
        public List<(ModelItem item, Point3D point)> ToList()
        {
            var list = new List<(ModelItem item, Point3D point)>();
            for (int i = 0; i < Items.Count; i++)
            {
                list.Add((Items[i], Point[i]));
            }
            return list;
        }
    }

    public class Chunk
    {
        public BoundingBox3D BoundingBox = new BoundingBox3D();
        public ModelItemCollection Items = new ModelItemCollection();
        public PointCloud_ PointCloud = new PointCloud_();
        public string name { get; set; }
    }

}