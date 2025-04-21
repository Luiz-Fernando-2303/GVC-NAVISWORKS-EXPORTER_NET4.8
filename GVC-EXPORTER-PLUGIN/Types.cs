using Autodesk.Navisworks.Api;
using System;
using System.Collections.Generic;

namespace GVC_EXPORTER_PLUGIN.Functions
{
    /// <summary>
    /// Representa uma Oriented Bounding Box (OBB), com centro, eixos e extensões.
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
        /// Verifica se um ponto está contido dentro da OBB.
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
        /// Retorna os cantos da caixa
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
    }

    /// <summary>
    /// Representa um nó da árvore de subdivisão espacial contendo um OBB e seus filhos.
    /// </summary>
    public class ChunkTree
    {
        public OrientedBoundingBox Box;
        public List<ChunkTree> Children;
        public bool IsLeaf => Children == null || Children.Count == 0;
    }
}