using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Navisworks.Api;

namespace GVC_EXPORTER_PLUGIN.Functions._Binary
{
    internal class Chunks_Oriented
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
        }

        /// <summary>
        /// Divide uma OBB maior em múltiplas OBBs menores (chunks) baseadas em dimensões fornecidas.
        /// </summary>
        public static List<OrientedBoundingBox> ChunkOBBsFromOBB(OrientedBoundingBox obb, double sizeX, double sizeY, double sizeZ)
        {
            if (sizeX <= 0 || sizeY <= 0 || sizeZ <= 0)
                throw new ArgumentOutOfRangeException("Os tamanhos dos chunks devem ser maiores que zero em todos os eixos.");

            Context.Instance._metadata["state"] = ("Creating chunks", 0, 0);

            double totalX = obb.ExtentX * 2;
            double totalY = obb.ExtentY * 2;
            double totalZ = obb.ExtentZ * 2;

            int xDiv = (int)Math.Ceiling(totalX / sizeX);
            int yDiv = (int)Math.Ceiling(totalY / sizeY);
            int zDiv = (int)Math.Ceiling(totalZ / sizeZ);

            double halfSizeX = sizeX / 2.0;
            double halfSizeY = sizeY / 2.0;
            double halfSizeZ = sizeZ / 2.0;

            var chunks = new List<OrientedBoundingBox>();
            int id = 0;

            for (int i = 0; i < xDiv; i++)
            {
                for (int j = 0; j < yDiv; j++)
                {
                    for (int k = 0; k < zDiv; k++)
                    {
                        double offsetX = -obb.ExtentX + halfSizeX + i * sizeX;
                        double offsetY = -obb.ExtentY + halfSizeY + j * sizeY;
                        double offsetZ = -obb.ExtentZ + halfSizeZ + k * sizeZ;

                        var center = obb.Center
                            + obb.AxisX * offsetX
                            + obb.AxisY * offsetY
                            + obb.AxisZ * offsetZ;

                        double limitX = obb.ExtentX - Math.Abs(offsetX);
                        double limitY = obb.ExtentY - Math.Abs(offsetY);
                        double limitZ = obb.ExtentZ - Math.Abs(offsetZ);

                        double extentX = Math.Min(halfSizeX, limitX);
                        double extentY = Math.Min(halfSizeY, limitY);
                        double extentZ = Math.Min(halfSizeZ, limitZ);

                        if (extentX > 0 && extentY > 0 && extentZ > 0)
                        {
                            chunks.Add(new OrientedBoundingBox(
                                id++,
                                center,
                                obb.AxisX,
                                obb.AxisY,
                                obb.AxisZ,
                                extentX,
                                extentY,
                                extentZ
                            ));
                        }
                    }
                }
            }

            return chunks;
        }

        /// <summary>
        /// Atribui cada ponto empacotado a um chunk (OBB) com base em sua posição.
        /// </summary>
        public static List<byte[]> LocatePointsOBB(List<OrientedBoundingBox> obbs)
        {
            var updatedPoints = new List<byte[]>(Context.Instance._points.Count);
            var usedIds = new HashSet<int>();

            Context.Instance._metadata["state"] = ("Locating points", Context.Instance._points.Count, 0);

            int count = 0;
            foreach (var point in Context.Instance._points)
            {
                double x = BitConverter.ToDouble(point, 16);
                double y = BitConverter.ToDouble(point, 24);
                double z = BitConverter.ToDouble(point, 32);
                var p = new Point3D(x, y, z);

                foreach (var obb in obbs)
                {
                    if (obb.Contains(p))
                    {
                        Buffer.BlockCopy(BitConverter.GetBytes(obb.Id), 0, point, 40, 4);
                        updatedPoints.Add(point);
                        usedIds.Add(obb.Id);
                        break;
                    }
                }
                count++;
                Context.Instance._metadata["state"] = ("Locating points", Context.Instance._points.Count, count);
            }

            Context.Instance._metadata["state"] = ("Clearing empty chunks", 0, 0);
            Context.Instance._chunks = obbs
                .Where(obb => usedIds.Contains(obb.Id))
                .Select(PackOBB)
                .ToList();

            Context.Instance._metadata["state"] = ("done", 0, 0);
            return updatedPoints;
        }

        /// <summary>
        /// Empacota uma OBB em um array de bytes (124 bytes) contendo ID, centro, eixos e extensões.
        /// </summary>
        public static byte[] PackOBB(OrientedBoundingBox obb)
        {
            byte[] buffer = new byte[124];
            int offset = 0;

            Buffer.BlockCopy(BitConverter.GetBytes(obb.Id), 0, buffer, offset, 4); offset += 4;

            void CopyVector(Vector3D v)
            {
                Buffer.BlockCopy(BitConverter.GetBytes(v.X), 0, buffer, offset, 8); offset += 8;
                Buffer.BlockCopy(BitConverter.GetBytes(v.Y), 0, buffer, offset, 8); offset += 8;
                Buffer.BlockCopy(BitConverter.GetBytes(v.Z), 0, buffer, offset, 8); offset += 8;
            }

            Buffer.BlockCopy(BitConverter.GetBytes(obb.Center.X), 0, buffer, offset, 8); offset += 8;
            Buffer.BlockCopy(BitConverter.GetBytes(obb.Center.Y), 0, buffer, offset, 8); offset += 8;
            Buffer.BlockCopy(BitConverter.GetBytes(obb.Center.Z), 0, buffer, offset, 8); offset += 8;

            CopyVector(obb.AxisX);
            CopyVector(obb.AxisY);
            CopyVector(obb.AxisZ);

            Buffer.BlockCopy(BitConverter.GetBytes(obb.ExtentX), 0, buffer, offset, 8); offset += 8;
            Buffer.BlockCopy(BitConverter.GetBytes(obb.ExtentY), 0, buffer, offset, 8); offset += 8;
            Buffer.BlockCopy(BitConverter.GetBytes(obb.ExtentZ), 0, buffer, offset, 8);

            return buffer;
        }

        /// <summary>
        /// Desempacota um array de bytes (124 bytes) em uma OBB com ID, centro, eixos e extensões.
        /// </summary>
        public static OrientedBoundingBox UnpackOBB(byte[] bytes)
        {
            if (bytes.Length != 124)
                throw new ArgumentException("Tamanho inválido de OBB. Esperado: 124 bytes.");

            int offset = 0;

            int id = BitConverter.ToInt32(bytes, offset); offset += 4;

            double ReadDouble() { double val = BitConverter.ToDouble(bytes, offset); offset += 8; return val; }

            Point3D center = new Point3D(ReadDouble(), ReadDouble(), ReadDouble());

            Vector3D axisX = new Vector3D(ReadDouble(), ReadDouble(), ReadDouble());
            Vector3D axisY = new Vector3D(ReadDouble(), ReadDouble(), ReadDouble());
            Vector3D axisZ = new Vector3D(ReadDouble(), ReadDouble(), ReadDouble());

            double extentX = ReadDouble();
            double extentY = ReadDouble();
            double extentZ = ReadDouble();

            return new OrientedBoundingBox(id, center, axisX, axisY, axisZ, extentX, extentY, extentZ);
        }

        /// <summary>
        /// Cria uma OBB rotacionada no plano XY a partir de uma BoundingBox3D.
        /// </summary>
        public static OrientedBoundingBox RotateOBB_Z_FromBoundingBox(BoundingBox3D box, double angleDegrees, byte id)
        {
            double rad = angleDegrees * Math.PI / 180.0;
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
