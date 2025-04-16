using System;
using System.Collections.Generic;
using Autodesk.Navisworks.Api;

namespace GVC_EXPORTER_PLUGIN.Functions.Binary_
{
    /// <summary>
    /// Handles chunking of the model bounding box and conversion of chunk data into binary format.
    /// </summary>
    internal class Chunks
    {
        /// <summary>
        /// Iterates over all points and checks which bounding box (chunk) each point falls into.
        /// If a point is inside a chunk, assigns the chunk's ID to the last byte of the point array.
        /// Returns the list of points with their associated chunk ID.
        /// </summary>
        /// <returns>List of byte arrays representing points with assigned chunk IDs.</returns>
        public static List<byte[]> LocatePoints()
        {
            List<byte[]> points = new List<byte[]>();

            foreach (var point in Context.Instance._points)
            {
                foreach (var chunk in Context.Instance._chunks)
                {
                    if (PointInBox(point, chunk))
                    {
                        point[point.Length - 1] = chunk[0]; // Assign the chunk ID to the point
                        points.Add(point);
                    }
                }
            }

            return points;
        }

        /// <summary>
        /// Checks whether a 3D point is inside the bounding box defined by a chunk.
        /// Coordinates are extracted from the byte arrays and compared accordingly.
        /// </summary>
        /// <param name="p">The point as a byte array [GUID(16)][X(8)][Y(8)][Z(8)][ChunkID(1)].</param>
        /// <param name="b">The chunk as a byte array [ID(1)][MinX(8)][MinY(8)][MinZ(8)][MaxX(8)][MaxY(8)][MaxZ(8)].</param>
        /// <returns>True if the point is within the chunk's bounding box; otherwise, false.</returns>
        public static bool PointInBox(byte[] p, byte[] b)
        {
            var miX = BitConverter.ToDouble(b, 1);
            var miY = BitConverter.ToDouble(b, 9);
            var miZ = BitConverter.ToDouble(b, 17);

            var maX = BitConverter.ToDouble(b, 25);
            var maY = BitConverter.ToDouble(b, 33);
            var maZ = BitConverter.ToDouble(b, 41);

            var x = BitConverter.ToDouble(p, 16);
            var y = BitConverter.ToDouble(p, 24);
            var z = BitConverter.ToDouble(p, 32);

            if (x >= miX && x <= maX && y >= miY && y <= maY && z >= miZ && z <= maZ)
            {
                return true;
            }

            return false;
        }


        /// <summary>
        /// Retrieves the bounding box of all geometry model items, splits it into chunks,
        /// and returns each chunk packed as a binary byte array.
        /// </summary>
        /// <returns>List of byte arrays representing each chunk's bounding box and identifier.</returns>
        public static List<byte[]> GetChunksBinary(int x = 2, int y = 2, int z = 1)
        {
            var search = new Search();
            search.Selection.SelectAll();
            search.SearchConditions.Add(
                SearchCondition.HasCategoryByName(PropertyCategoryNames.Geometry)
            );
            var models = search.FindAll(Application.ActiveDocument, false);

            var chunks = ChunkBoundingBox3D(models.BoundingBox(), x, y, z);
            var chunkList = new List<byte[]>();

            int count = 0;
            foreach (var box in chunks)
            {
                chunkList.Add(PackBox(box, count));
                count++;
            }
            return chunkList;
        }

        /// <summary>
        /// Packs the bounding box and an identifier into a 49-byte array.
        /// Format: [ID(1)][MinX(8)][MinY(8)][MinZ(8)][MaxX(8)][MaxY(8)][MaxZ(8)]
        /// </summary>
        /// <param name="box">The bounding box to pack.</param>
        /// <param name="id">The ID for the chunk.</param>
        /// <returns>Byte array representation of the bounding box and ID.</returns>
        public static byte[] PackBox(BoundingBox3D box, int id)
        {
            byte[] buffer = new byte[49];
            buffer[0] = (byte)id;
            Buffer.BlockCopy(BitConverter.GetBytes(box.Min.X), 0, buffer, 1, 8);
            Buffer.BlockCopy(BitConverter.GetBytes(box.Min.Y), 0, buffer, 9, 8);
            Buffer.BlockCopy(BitConverter.GetBytes(box.Min.Z), 0, buffer, 17, 8);
            Buffer.BlockCopy(BitConverter.GetBytes(box.Max.X), 0, buffer, 25, 8);
            Buffer.BlockCopy(BitConverter.GetBytes(box.Max.Y), 0, buffer, 33, 8);
            Buffer.BlockCopy(BitConverter.GetBytes(box.Max.Z), 0, buffer, 41, 8);
            return buffer;
        }

        /// <summary>
        /// Unpacks a byte array representing a bounding box and ID.
        /// </summary>
        /// <param name="bytes">49-byte array to unpack.</param>
        /// <returns>Tuple containing the ID and the BoundingBox3D.</returns>
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

        /// <summary>
        /// Splits a bounding box into smaller chunks based on the given divisions in X, Y, and Z axes.
        /// </summary>
        /// <param name="originalBox">The original bounding box to split.</param>
        /// <param name="xDiv">Number of divisions along the X axis.</param>
        /// <param name="yDiv">Number of divisions along the Y axis.</param>
        /// <param name="zDiv">Number of divisions along the Z axis.</param>
        /// <returns>List of smaller bounding boxes resulting from the division.</returns>
        private static List<BoundingBox3D> ChunkBoundingBox3D(BoundingBox3D originalBox, int xDiv, int yDiv, int zDiv)
        {
            BoundingBox3D NormalizeBox(BoundingBox3D box)
            {
                double minX = Math.Min(box.Min.X, box.Max.X);
                double minY = Math.Min(box.Min.Y, box.Max.Y);
                double minZ = Math.Min(box.Min.Z, box.Max.Z);
                double maxX = Math.Max(box.Min.X, box.Max.X);
                double maxY = Math.Max(box.Min.Y, box.Max.Y);
                double maxZ = Math.Max(box.Min.Z, box.Max.Z);

                return new BoundingBox3D(
                    new Point3D(minX, minY, minZ),
                    new Point3D(maxX, maxY, maxZ)
                );
            }

            BoundingBox3D CreateChunkBox(BoundingBox3D box, int i, int j, int k, double sizeX, double sizeY, double sizeZ)
            {
                double minX = box.Min.X + i * sizeX;
                double minY = box.Min.Y + j * sizeY;
                double minZ = box.Min.Z + k * sizeZ;
                double maxX = minX + sizeX;
                double maxY = minY + sizeY;
                double maxZ = minZ + sizeZ;

                return new BoundingBox3D(
                    new Point3D(minX, minY, minZ),
                    new Point3D(maxX, maxY, maxZ)
                );
            }

            if (originalBox == null)
                throw new ArgumentNullException(nameof(originalBox), "The original bounding box cannot be null.");

            if (xDiv <= 0 || yDiv <= 0 || zDiv <= 0)
                throw new ArgumentOutOfRangeException("Divisions must be greater than zero for all dimensions.");

            var normalizedBox = NormalizeBox(originalBox);
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
                            var chunkBox = CreateChunkBox(normalizedBox, i, j, k, sizeX, sizeY, sizeZ);
                            chunks.Add(chunkBox);
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
