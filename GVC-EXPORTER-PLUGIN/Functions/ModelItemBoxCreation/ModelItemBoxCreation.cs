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
        public static void RecursivePackPoints(ModelItemEnumerableCollection models, List<BoxedModelitem> accumulator, List<ModelItem> localItems)
        {
            var stack = new Stack<ModelItem>(models);

            int count = 0;
            Context.Instance._state["state"] = ("Gerando pontos", count, stack.Count);
            while (stack.Count > 0)
            {
                var current = stack.Pop();
                var children = current.Children.ToList(); 

                if (children.Count == 0)
                {
                    var boxed = PackModelItemPoint(current);
                    accumulator.Add(boxed);
                    localItems.Add(current);
                }
                else
                {
                    foreach (var child in children)
                    {
                        stack.Push(child);
                    }
                }
                count++;
                Context.Instance._state["state"] = ("Gerando pontos", count, stack.Count);
            }
        }

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
