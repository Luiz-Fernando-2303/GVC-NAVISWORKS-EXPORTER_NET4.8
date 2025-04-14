using Autodesk.Navisworks.Api;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace GVC_EXPORTER_PLUGIN.Functions.Chunks
{
    public static class ChuncksFunctions
    {

        public static void BuildChunks(int x = 2, int y = 2, int z = 1, int chunkThreads = 2)
        {
            var pointCloud = Context.Instance.ModelPointCloud;
            var modelBbox = Context.Instance.ModelBox;
            var pointCloudList = pointCloud.ToList();
            Context.Instance.chunks = new List<Chunk>();

            var chunksBbox = ChunkBoundingBox3D(modelBbox, x, y, z);

            var lockObj = new object();
            var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = chunkThreads };

            Parallel.ForEach(chunksBbox, parallelOptions, Chunk =>
            {
                var chunk = new Chunk { BoundingBox = Chunk, name = "" };

                foreach (var point in pointCloudList)
                {
                    if (InBox(Chunk, point.point))
                    {
                        chunk.PointCloud.Items.Add(point.item);
                        chunk.PointCloud.Point.Add(point.point);
                    }
                }
                chunk.Items = chunk.PointCloud.Items;

                lock (lockObj)
                {
                    Context.Instance.chunks.Add(chunk);
                }
            });

            for (int i = 0; i < Context.Instance.chunks.Count; i++)
            {
                var chunk = Context.Instance.chunks[i];
                chunk.name = $"Chunk {i + 1}";
            }
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

        private static bool InBox(BoundingBox3D box, Point3D point)
        {
            if (box == null || point == null) return false;

            try
            {
                return point.X >= box.Min.X && point.X <= box.Max.X &&
                       point.Y >= box.Min.Y && point.Y <= box.Max.Y &&
                       point.Z >= box.Min.Z && point.Z <= box.Max.Z;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: ", ex.Message);
                return false;
            }
        }
    }
}
