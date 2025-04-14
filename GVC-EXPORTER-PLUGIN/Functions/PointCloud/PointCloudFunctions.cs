using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Autodesk.Navisworks.Api;

namespace GVC_EXPORTER_PLUGIN.Functions.PointCloud
{
    public static class PointCloudFunctions
    {
        public static void ParallelModelPointCloud(int batchSize = 500)
        {
            var batches = ModelItemsBatchs(batchSize);

            var bag = new ConcurrentBag<(ModelItem item, Point3D point)>();
            Parallel.ForEach(batches, batch =>
            {
                foreach (var item in batch)
                {
                    var bbox = item.Geometry.BoundingBox;
                    if (bbox != null && !bbox.IsEmpty)
                    {
                        bag.Add((item, bbox.Center));
                    }
                }
            });

            var pointCloud = new PointCloud_();
            foreach (var entry in bag)
            {
                pointCloud.Items.Add(entry.item);
                pointCloud.Point.Add(entry.point);
            }

            Context.Instance.ModelPointCloud = pointCloud;
        }

        private static List<List<ModelItem>> ModelItemsBatchs(int batchSize = 500)
        {
            List<List<ModelItem>> batches = new List<List<ModelItem>>();

            var search = new Search();
            search.Selection.SelectAll();
            search.SearchConditions.Add(
                SearchCondition.HasCategoryByName(PropertyCategoryNames.Geometry)
            );
            var models = search.FindAll(Application.ActiveDocument, false);

            List<ModelItem> allItems = new List<ModelItem>();

            allItems = models.ToList();

            for (int i = 0; i < allItems.Count; i += batchSize)
                batches.Add(allItems.Skip(i).Take(batchSize).ToList());

            return batches;
        }
    }
}
