using System;
using Autodesk.Navisworks.Api;

namespace GVC_EXPORTER_PLUGIN.Functions.Box
{
    internal class Box_Functions
    {
        public static OrientedBoundingBox ObbFromBoundingBox(BoundingBox3D box, int id)
        {
            double rad = Context.Instance.RotationCorrection * Math.PI / 180.0;
            double cos = Math.Cos(rad);
            double sin = Math.Sin(rad);

            var center = new Point3D(
                (box.Min.X + box.Max.X) / 2.0,
                (box.Min.Y + box.Max.Y) / 2.0,
                (box.Min.Z + box.Max.Z) / 2.0
            );

            double extentX = (box.Max.X - box.Min.X) / 2.0;
            double extentY = (box.Max.Y - box.Min.Y) / 2.0;
            double extentZ = (box.Max.Z - box.Min.Z) / 2.0;

            var axisX = new Vector3D(cos, sin, 0);
            var axisY = new Vector3D(-sin, cos, 0);
            var axisZ = new Vector3D(0, 0, 1);

            return new OrientedBoundingBox(id, center, axisX, axisY, axisZ, extentX, extentY, extentZ);
        }
    }
}
