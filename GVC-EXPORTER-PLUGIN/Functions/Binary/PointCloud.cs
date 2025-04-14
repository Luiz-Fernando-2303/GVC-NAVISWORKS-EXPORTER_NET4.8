using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Autodesk.Navisworks.Api;

namespace GVC_EXPORTER_PLUGIN.Functions.Binary
{
    internal class PointCloud
    {
        public static List<byte[]> Points()
        {
            var search = new Search();
            search.Selection.SelectAll();
            search.SearchConditions.Add(
                SearchCondition.HasCategoryByName(PropertyCategoryNames.Geometry)
            );
            var models = search.FindAll(Application.ActiveDocument, false);

            var pointsBag = new ConcurrentBag<byte[]>();

            Parallel.ForEach(models.OfType<ModelItem>(), model =>
            {
                var point = PackPoint(model);
                if (point != null)
                    pointsBag.Add(point);
            });

            return pointsBag.ToList();
        }

        public static byte[] PackPoint(ModelItem model)
        {
            if (model.InstanceGuid == Guid.Empty)
                return null;

            var point = new byte[40]; // [16] -> Guid, [8] -> X, [8] -> Y, [8] -> Z || [40] total

            byte[] guidBytes = model.InstanceGuid.ToByteArray();
            Buffer.BlockCopy(guidBytes, 0, point, 0, 16);

            double x = model.Geometry.BoundingBox.Center.X;
            double y = model.Geometry.BoundingBox.Center.Y;
            double z = model.Geometry.BoundingBox.Center.Z;

            Buffer.BlockCopy(BitConverter.GetBytes(x), 0, point, 16, 8);
            Buffer.BlockCopy(BitConverter.GetBytes(y), 0, point, 24, 8);
            Buffer.BlockCopy(BitConverter.GetBytes(z), 0, point, 32, 8);

            return point;
        }

        public static ModelItem GetModelItem(byte[] point)
        {
            byte[] guidBytes = new byte[16];
            Buffer.BlockCopy(point, 0, guidBytes, 0, 16);
            Guid PlaceholderGuid = new Guid(guidBytes);

            Search search = new Search();
            ModelItemEnumerableCollection moic = search.FindAll(Autodesk.Navisworks.Api.Application.ActiveDocument, true).DescendantsAndSelf;
            ModelItemCollection micdd = new ModelItemCollection();

            foreach (ModelItem mi in moic)
            {
                if (mi.InstanceGuid != PlaceholderGuid)
                {
                    return mi;
                }
            }
            return null;
        }
    }
}
