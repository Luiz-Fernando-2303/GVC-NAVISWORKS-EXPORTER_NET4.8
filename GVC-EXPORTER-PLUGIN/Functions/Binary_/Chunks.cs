using System;
using System.Collections.Generic;
using Autodesk.Navisworks.Api;

namespace GVC_EXPORTER_PLUGIN.Functions.Binary_
{
    internal class Chunks
    {
        public static List<byte[]> GetChunksBinary()
        {
            var search = new Search();
            search.Selection.SelectAll();
            search.SearchConditions.Add(
                SearchCondition.HasCategoryByName(PropertyCategoryNames.Geometry)
            );
            var models = search.FindAll(Application.ActiveDocument, false);

            var chunks = ChunkBoundingBox3D(models.BoundingBox(), 2, 2, 1);
            var chunkList = new List<byte[]>();

            int count = 0;

            foreach (var box in chunks)
            {
                chunkList.Add(PackBox(box, count));
                count++;
            }
            return chunkList;
        }

        public static byte[] PackBox(BoundingBox3D B, int id)
        {
            byte[] Boxbytes = new byte[49]; // [0][00000000][00000000][00000000][00000000][00000000][00000000] -> [ID][X][Y][Z] -> [49] total
            Boxbytes[0] = (byte)id;
            Buffer.BlockCopy(BitConverter.GetBytes(B.Min.X), 0, Boxbytes, 1, 8);
            Buffer.BlockCopy(BitConverter.GetBytes(B.Min.Y), 0, Boxbytes, 9, 8);
            Buffer.BlockCopy(BitConverter.GetBytes(B.Min.Z), 0, Boxbytes, 17, 8);
            Buffer.BlockCopy(BitConverter.GetBytes(B.Max.X), 0, Boxbytes, 25, 8);
            Buffer.BlockCopy(BitConverter.GetBytes(B.Max.Y), 0, Boxbytes, 33, 8);
            Buffer.BlockCopy(BitConverter.GetBytes(B.Max.Z), 0, Boxbytes, 41, 8);

            return Boxbytes;
        }

        public static (int id, BoundingBox3D box) UnpackBox(byte[] bytes)
        {
            if (bytes.Length != 49)
                throw new ArgumentException("Invalid byte array length. Expected 49 bytes.");

            int id = bytes[0];
            double minX = BitConverter.ToDouble(bytes, 1);
            double minY = BitConverter.ToDouble(bytes, 9);
            double minZ = BitConverter.ToDouble(bytes, 17);
            double maxX = BitConverter.ToDouble(bytes, 25);
            double maxY = BitConverter.ToDouble(bytes, 33);
            double maxZ = BitConverter.ToDouble(bytes, 41);

            var box = new BoundingBox3D(
                new Point3D(minX, minY, minZ),
                new Point3D(maxX, maxY, maxZ)
            );
            return (id, box);
        }

        private static List<BoundingBox3D> ChunkBoundingBox3D(BoundingBox3D originalBox, int xDiv, int yDiv, int zDiv)
        {
            if (originalBox == null)
                throw new ArgumentNullException(nameof(originalBox), "The original bounding box cannot be null.");

            if (xDiv <= 0 || yDiv <= 0 || zDiv <= 0)
                throw new ArgumentOutOfRangeException("Divisions must be greater than zero for all dimensions.");

            double minX = Math.Min(originalBox.Min.X, originalBox.Max.X);
            double minY = Math.Min(originalBox.Min.Y, originalBox.Max.Y);
            double minZ = Math.Min(originalBox.Min.Z, originalBox.Max.Z);

            double maxX = Math.Max(originalBox.Min.X, originalBox.Max.X);
            double maxY = Math.Max(originalBox.Min.Y, originalBox.Max.Y);
            double maxZ = Math.Max(originalBox.Min.Z, originalBox.Max.Z);

            var normalizedBox = new BoundingBox3D(
                new Point3D(minX, minY, minZ),
                new Point3D(maxX, maxY, maxZ)
            );

            var chunks = new List<BoundingBox3D>();

            try
            {
                double sizeX = (normalizedBox.Max.X - normalizedBox.Min.X) / xDiv;
                double sizeY = (normalizedBox.Max.Y - normalizedBox.Min.Y) / yDiv;
                double sizeZ = (normalizedBox.Max.Z - normalizedBox.Min.Z) / zDiv;

                for (int i = 0; i < xDiv; i++)
                {
                    for (int j = 0; j < yDiv; j++)
                    {
                        for (int k = 0; k < zDiv; k++)
                        {
                            double chunkMinX = normalizedBox.Min.X + i * sizeX;
                            double chunkMinY = normalizedBox.Min.Y + j * sizeY;
                            double chunkMinZ = normalizedBox.Min.Z + k * sizeZ;

                            double chunkMaxX = chunkMinX + sizeX;
                            double chunkMaxY = chunkMinY + sizeY;
                            double chunkMaxZ = chunkMinZ + sizeZ;

                            var chunkMin = new Point3D(chunkMinX, chunkMinY, chunkMinZ);
                            var chunkMax = new Point3D(chunkMaxX, chunkMaxY, chunkMaxZ);

                            chunks.Add(new BoundingBox3D(chunkMin, chunkMax));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("An error occurred while generating chunk bounding boxes.", ex);
            }

            return chunks;
        }
    }
}
