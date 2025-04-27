using System.Collections.Generic;
using System.Linq;
using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.Clash;

namespace GVC_EXPORTER_PLUGIN.Functions.Clash_
{
    internal class Zone_Detection
    {
        public static void AddZonesToitems()
        {
            Application.ActiveDocument.CurrentSelection.Clear();
            var itemsWithZones = GetZoneGroupedPointsFromClash();
            int count = 0;
            Context.Instance._state["state"] = ("Adicionando propriedades", itemsWithZones.Count, 0);
            foreach (var zone in itemsWithZones)
            {
                var point = zone.Key;
                var properties = zone.Value;
                if (point.ModelItem != null)
                {
                    Properties.Properties_Functions.AddPropertiesToModelItem(point.ModelItem, "Zone", properties);
                    System.Windows.Forms.Application.DoEvents();
                }
                Context.Instance._state["state"] = ("Adicionando propriedades", itemsWithZones.Count, count);
                count++;
            }
            Context.Instance._state["state"] = ("done", 0, 0);
        }

        public static Dictionary<BoxedModelitem, Dictionary<string, string>> GetZoneGroupedPointsFromClash()
        {
            var clashGroup = Clash_Functions.GetClashResultGroupFromTest("Zone Detection");
            if (clashGroup == null)
                return new Dictionary<BoxedModelitem, Dictionary<string, string>>();

            var allPoints = new HashSet<BoxedModelitem>(Context.Instance._points);
            var pointsDictionary = Context.Instance._points.ToDictionary(p => p.ModelItem);
            var zonesPerPoint = ProcessClashes();
            var bestZonePerPoint = BestZonePerPoint();

            Dictionary<BoxedModelitem, (List<BoxedModelitem> zones, List<double> distances, List<double> volumes)> ProcessClashes()
            {
                var zoneItems = new Dictionary<BoxedModelitem, (List<BoxedModelitem>, List<double>, List<double>)>();

                var clashItems = clashGroup.Children.OfType<ClashResult>().ToList();

                int count__ = 0;
                Context.Instance._state["state"] = ("Juntando clashes por ponto", clashItems.Count, 0);

                foreach (var clash in clashItems)
                {
                    var zoneItem = clash.Item1;
                    var pointItem = clash.Item2;

                    var zone = Context.Instance._ZonePoints.FirstOrDefault(z => z.ModelItem.Equals(zoneItem));
                    pointsDictionary.TryGetValue(pointItem, out var point);

                    if (zone == null || point == null)
                        continue;

                    if (!zoneItems.TryGetValue(point, out var entry))
                    {
                        entry = (new List<BoxedModelitem>(), new List<double>(), new List<double>());
                        zoneItems[point] = entry;
                    }

                    entry.Item1.Add(zone);
                    entry.Item2.Add(clash.Distance);
                    entry.Item3.Add(clash.ViewBounds.Volume);

                    allPoints.Remove(point);
                    count__++;
                    Context.Instance._state["state"] = ("Juntando clashes por ModelItem", clashItems.Count, count__);
                }

                Context.Instance._state["state"] = ("done", 0, 0);

                return zoneItems;
            }

            Dictionary<BoxedModelitem, Dictionary<string, string>> BestZonePerPoint()
            {
                var bestZone = new Dictionary<BoxedModelitem, Dictionary<string, string>>();

                int count_ = 0;
                Context.Instance._state["state"] = ("Definindo melhor ocorrencia de clash", zonesPerPoint.Count, 0);
                foreach (var kv in zonesPerPoint)
                {
                    var point = kv.Key;
                    var zoneList = kv.Value.zones;
                    var distanceList = kv.Value.distances;
                    var volumeList = kv.Value.volumes;

                    int bestIndex = -1;
                    double bestVolume = double.MinValue;
                    double bestDistance = double.MaxValue;

                    for (int i = 0; i < zoneList.Count; i++)
                    {
                        double volume = volumeList[i];
                        double distance = distanceList[i];

                        if (volume > bestVolume || (volume == bestVolume && distance < bestDistance))
                        {
                            bestVolume = volume;
                            bestDistance = distance;
                            bestIndex = i;
                        }
                    }

                    if (bestIndex >= 0 && bestIndex < zoneList.Count)
                    {
                        var props = GetCustomProperties(zoneList[bestIndex].ModelItem);
                        bestZone[point] = props;
                    }
                    count_++;
                    Context.Instance._state["state"] = ("Definindo melhor ocorrencia de clash", zonesPerPoint.Count, count_);
                }

                Context.Instance._state["state"] = ("done", 0, 0);
                return bestZone;
            }

            int count = 0;
            Context.Instance._state["state"] = ("Classificando ModelItems restantes", allPoints.Count, 0);
            foreach (var point in allPoints)
            {
                foreach (var zone in Context.Instance._ZonePoints)
                {
                    var obb = zone.OrientedBoundingBox;
                    if (obb != null && point.OrientedBoundingBox != null)
                    {
                        var allInside = point.OrientedBoundingBox.GetCorners().All(corner => obb.Contains(corner));
                        if (allInside)
                        {
                            var props = GetCustomProperties(zone.ModelItem);
                            bestZonePerPoint[point] = props;

                            count++;
                            Context.Instance._state["state"] = ("Classificando ModelItems restantes", allPoints.Count, count);

                            break;
                        }
                    }
                }
            }
            Context.Instance._state["state"] = ("done", 0, 0);

            return bestZonePerPoint;
        }

        private static Dictionary<string, string> GetCustomProperties(ModelItem item)
        {
            var dict = new Dictionary<string, string>();
            var customCategory = item.PropertyCategories.FirstOrDefault(c => c.DisplayName == "Custom");
            if (customCategory == null) return dict;

            foreach (var prop in customCategory.Properties)
            {
                var key = prop.DisplayName;
                var value = prop.Value.IsDisplayString ? prop.Value.ToDisplayString() : prop.Value.ToString();
                dict[key] = value;
            }
            return dict;
        }
    }

    internal class Clash_Functions
    {
        public static void RunZonesClash(string name, List<BoxedModelitem> points, List<BoxedModelitem> zones){
            CreateClashTest(name, points, zones);

            var doc = Application.ActiveDocument;
            var documentClash = doc.GetClash();

            var toRun = documentClash.TestsData.Tests
                .Where(t => t.DisplayName == name && t is ClashTest)
                .Cast<ClashTest>().First();

            documentClash.TestsData.TestsRunTest(toRun);
        }

        public static ClashResultGroup GetClashResultGroupFromTest(string clashTestName, string groupName = "Grouped:NEW")
        {
            DocumentClash documentClash = Application.ActiveDocument.GetClash();
            DocumentClashTests clashTests = documentClash.TestsData;

            ClashTest test = clashTests.Tests
                .OfType<ClashTest>()
                .FirstOrDefault(t => t.DisplayName == clashTestName);

            if (test == null)
                return null;

            int existingGroupIndex = test.Children.IndexOfDisplayName(groupName);
            ClashResultGroup resultGroup;

            if (existingGroupIndex == -1)
            {
                var newGroup = new ClashResultGroup
                {
                    DisplayName = groupName
                };

                clashTests.TestsInsertCopy(test, 0, newGroup);
                resultGroup = (ClashResultGroup)test.Children[0];
            }
            else
            {
                resultGroup = (ClashResultGroup)test.Children[existingGroupIndex];
            }

            int count = 0;
            Context.Instance._state["state"] = ("Gerando ClashGroup", test.Children.Count, count);
            for (int i = test.Children.Count - 1; i >= 0; i--)
            {
                var child = test.Children[i];
                if (child is ClashResult result)
                {
                    clashTests.TestsMove(test, i, resultGroup, 0);
                }
                Context.Instance._state["state"] = ("Gerando ClashGroup", test.Children.Count, count);
                count++;
            }

            return resultGroup;
        }

        internal static void CreateClashTest(string name, List<BoxedModelitem> points, List<BoxedModelitem> zones)
        {
            var doc = Application.ActiveDocument;
            var clashTest = CreateClashTest(name, Context.Instance._items, CreateModelItemCollection(zones));
            AddClashTest(clashTest);
        }

        internal static void AddClashTest(ClashTest test)
        {
            var doc = Application.ActiveDocument;
            var documentClash = doc.GetClash();
            documentClash.TestsData.TestsAddCopy(test);
        }

        internal static ClashTest CreateClashTest(string name, ModelItemCollection selA, ModelItemCollection selB)
        {
            ClashTest clashTest = new ClashTest();
            clashTest.CustomTestName = name;
            clashTest.DisplayName = name;
            clashTest.TestType = ClashTestType.Hard;
            clashTest.Tolerance = 0.0;
            clashTest.SelectionA.SelfIntersect = false;
            clashTest.SelectionA.PrimitiveTypes= PrimitiveTypes.Triangles;
            clashTest.SelectionA.Selection.CopyFrom(selA);
            clashTest.SelectionB.SelfIntersect = false;
            clashTest.SelectionB.PrimitiveTypes = PrimitiveTypes.Triangles;
            clashTest.SelectionB.Selection.CopyFrom(selB);

            return clashTest;
        }

        internal static ModelItemCollection CreateModelItemCollection(List<BoxedModelitem> itens)
        {
            ModelItemCollection collection = new ModelItemCollection();
            List<ModelItem> modelItems = itens.Select(p => p.ModelItem).ToList();
            collection.AddRange(modelItems);

            return collection;
        }
    }
}
