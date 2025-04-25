using Autodesk.Navisworks.Api;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GVC_EXPORTER_PLUGIN.Functions
{
    /// <summary>
    /// Represents an Oriented Bounding Box (OBB), with center, axes, and extents.
    /// </summary>
    public class OrientedBoundingBox
    {
        public int Id;
        public Point3D Center;
        public Vector3D AxisX, AxisY, AxisZ;
        public double ExtentX, ExtentY, ExtentZ;

        public OrientedBoundingBox(int id = 0, Point3D center = null, Vector3D axisX = null, Vector3D axisY = null, Vector3D axisZ = null, double extentX = 0, double extentY = 0, double extentZ = 0)
        {
            Id = id;
            Center = center;
            AxisX = axisX;
            AxisY = axisY;
            AxisZ = axisZ;
            ExtentX = extentX;
            ExtentY = extentY;
            ExtentZ = extentZ;
        }

        /// <summary>
        /// Checks if a point is inside the OBB.
        /// </summary>
        public bool Contains(Point3D p)
        {
            var d = p - Center;
            double dx = d.Dot(AxisX);
            double dy = d.Dot(AxisY);
            double dz = d.Dot(AxisZ);
            return Math.Abs(dx) <= ExtentX && Math.Abs(dy) <= ExtentY && Math.Abs(dz) <= ExtentZ;
        }

        /// <summary>
        /// Returns the corners of the box.
        /// </summary>
        public List<Point3D> GetCorners()
        {
            var corners = new List<Point3D>(8);
            var dx = AxisX * ExtentX;
            var dy = AxisY * ExtentY;
            var dz = AxisZ * ExtentZ;

            int[] signs = { -1, 1 };
            foreach (int sx in signs)
                foreach (int sy in signs)
                    foreach (int sz in signs)
                        corners.Add(Center + dx * sx + dy * sy + dz * sz);

            return corners;
        }

        /// <summary>
        /// Returns the axis-aligned bounding box (AABB) of the OBB.
        /// </summary>
        public BoundingBox3D GetAABB()
        {
            var corners = GetCorners();
            return new BoundingBox3D(
                new Point3D(
                    corners.Min(p => p.X),
                    corners.Min(p => p.Y),
                    corners.Min(p => p.Z)
                ),
                new Point3D(
                    corners.Max(p => p.X),
                    corners.Max(p => p.Y),
                    corners.Max(p => p.Z)
                )
            );
        }
    }

    /// <summary>
    /// Represents a point associated with a model item, including its OBB and chunk ID.
    /// </summary>
    public class BoxedModelitem
    {
        public ModelItem ModelItem;
        public OrientedBoundingBox OrientedBoundingBox;
        public Point3D Center;
        public int id;

        public BoxedModelitem(ModelItem modelItem, OrientedBoundingBox box, int id_)
        {
            ModelItem = modelItem;
            OrientedBoundingBox = box;
            Center = OrientedBoundingBox.Center;
            id = id_;
        }
    }
}
