using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Autodesk.Navisworks.Api;
using GVC_EXPORTER_PLUGIN.Functions.Box;

namespace GVC_EXPORTER_PLUGIN.Functions.ModelItemBoxCreation
{
    internal class ModelItemBoxCreation
    {
        public static List<BoxedModelitem> GetPackedPoints(ModelItemCollection items = null)
        {
            var modelItems = items == null ? Context.Instance._items : items;

            var packedPoints = new ConcurrentBag<BoxedModelitem>();
            Context.Instance._state["state"] = ("Packing points", modelItems.Count, 0);

            var count = 0;
            Parallel.ForEach(modelItems.OfType<ModelItem>(), modelItem =>
            {
                var packed = PackModelItemPoint(modelItem, count);
                if (packed != null)
                {
                    packedPoints.Add(packed);
                }
                count++;
                Context.Instance._state["state"] = ("Packing points", modelItems.Count, count);
            });

            return packedPoints.ToList();
        }

        public static BoxedModelitem PackModelItemPoint(ModelItem modelItem, int id = 0)
        {
            BoxedModelitem modelItemPoint = new BoxedModelitem(
                modelItem,
                Box_Functions.ObbFromBoundingBox(modelItem.BoundingBox(), id),
                0);

            return modelItemPoint;
        }
    }
}
