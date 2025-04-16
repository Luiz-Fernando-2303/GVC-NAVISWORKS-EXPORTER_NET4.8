using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Autodesk.Navisworks.Api;

namespace GVC_EXPORTER_PLUGIN.Functions.Binary_
{
    /// <summary>
    /// Provides utilities for extracting and packing point cloud data from Navisworks model items.
    /// </summary>
    internal class PointCloudExporter
    {
        /// <summary>
        /// Retrieves and packs the center points of all geometry model items into byte arrays.
        /// Each byte array represents a unique model item as [GUID(16)][X(8)][Y(8)][Z(8)][ChunkID(1)].
        /// </summary>
        /// <returns>A list of packed byte arrays representing model item center points.</returns>
        public static List<byte[]> GetPackedPoints()
        {
            var modelItems = Context.Instance._items;
            var packedPoints = new ConcurrentBag<byte[]>();

            Parallel.ForEach(modelItems.OfType<ModelItem>(), modelItem =>
            {
                var packed = PackModelItemCenter(modelItem);
                if (packed != null)
                {
                    packedPoints.Add(packed);
                }
            });

            return packedPoints.ToList();
        }

        /// <summary>
        /// Packs a model item's InstanceGuid and center point coordinates into a 40-byte array.
        /// Format: [GUID(16)][X(8)][Y(8)][Z(8)][ChunkID(1)]
        /// </summary>
        /// <param name="modelItem">The model item to pack.</param>
        /// <returns>A 41-byte array representing the model item, or null if GUID is empty.</returns>
        public static byte[] PackModelItemCenter(ModelItem modelItem)
        {
            if (modelItem.InstanceGuid == Guid.Empty)
                return null;

            var data = new byte[40];
            Buffer.BlockCopy(modelItem.InstanceGuid.ToByteArray(), 0, data, 0, 16);

            var center = modelItem.Geometry.BoundingBox.Center;
            Buffer.BlockCopy(BitConverter.GetBytes(center.X), 0, data, 16, 8);
            Buffer.BlockCopy(BitConverter.GetBytes(center.Y), 0, data, 24, 8);
            Buffer.BlockCopy(BitConverter.GetBytes(center.Z), 0, data, 32, 8);
            data[data.Length - 1] = (byte)0;

            return data;
        }

        /// <summary>
        /// Searches for model items matching the GUID encoded in the packed point array.
        /// </summary>
        /// <param name="packedPoint">The 40-byte array containing the GUID and center point.</param>
        /// <returns>A collection of model items that match the encoded GUID.</returns>
        public static ModelItemCollection GetModelItemsFromPackedPoint(byte[] packedPoint)
        {
            byte[] guidBytes = new byte[16];
            Buffer.BlockCopy(packedPoint, 0, guidBytes, 0, 16);
            Guid targetGuid = new Guid(guidBytes);

            var foundItems = new ModelItemCollection();
            var allItems = Context.Instance._items;

            foreach (var item in allItems)
            {
                if (item.InstanceGuid == targetGuid)
                {
                    foundItems.Add(item);
                }
            }

            return foundItems;
        }
    }
}
