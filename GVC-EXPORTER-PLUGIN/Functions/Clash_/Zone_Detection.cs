using System.Collections.Generic;
using System.Linq;
using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.Clash;

namespace GVC_EXPORTER_PLUGIN.Functions.Clash_
{
    internal class Zone_Detection
    {
        public static void AddZonesToItems()
        {
            var itemsWithZones = GetZoneGroupedPointsFromClash();
            int processedCount = 0;

            SetContextState("Adicionando propriedades", itemsWithZones.Count, 0);

            // Fix for the errors related to deconstruction and type inference in the foreach loop
            foreach (var kvp in itemsWithZones)
            {
                var point = kvp.Key;
                var properties = kvp.Value;

                if (point.ModelItem != null)
                    Properties.Properties_Functions.AddPropertiesToModelItem(point.ModelItem, "Zone", properties);

                SetContextState("Adicionando propriedades", itemsWithZones.Count, ++processedCount);
            }

            SetContextState("done", 0, 0);
        }

        public static Dictionary<BoxedModelitem, Dictionary<string, string>> GetZoneGroupedPointsFromClash()
        {
            var clashGroup = Clash_Functions.GetClashResultGroupFromTest("Zone Detection");
            if (clashGroup == null)
                return new Dictionary<BoxedModelitem, Dictionary<string, string>>();

            var allPoints = new HashSet<BoxedModelitem>(Context.Instance._points);
            var zonesPerPoint = GroupZonesByClash(clashGroup, allPoints);
            var bestZones = DetermineBestZonePerPoint(zonesPerPoint);
            AssignRemainingZonesByContainment(allPoints, bestZones);

            return bestZones;
        }

        private static Dictionary<BoxedModelitem, (List<BoxedModelitem> zones, List<double> distances, List<double> volumes)>
        GroupZonesByClash(ClashResultGroup clashGroup, HashSet<BoxedModelitem> allPoints)
        {
            var zoneItems = new Dictionary<BoxedModelitem, (List<BoxedModelitem>, List<double>, List<double>)>();
            int count = 0;

            SetContextState("Juntando clashes por ponto", clashGroup.Children.Count, 0);

            foreach (var clash in clashGroup.Children.OfType<ClashResult>())
            {
                var zone = Context.Instance._ZonePoints.FirstOrDefault(z => z.ModelItem.Equals(clash.Item1));
                var point = Context.Instance._points.FirstOrDefault(p => p.ModelItem.Equals(clash.Item2));

                if (zone == null || point == null) continue;

                if (!zoneItems.TryGetValue(point, out var entry))
                    entry = zoneItems[point] = (new List<BoxedModelitem>(), new List<double>(), new List<double>());

                entry.Item1.Add(zone);
                entry.Item2.Add(clash.Distance);
                entry.Item3.Add(clash.ViewBounds.Volume);

                allPoints.Remove(point);

                SetContextState("Juntando clashes por ModelItem", clashGroup.Children.Count, ++count);
            }

            SetContextState("done", 0, 0);
            return zoneItems;
        }

        private static Dictionary<BoxedModelitem, Dictionary<string, string>>
        DetermineBestZonePerPoint(Dictionary<BoxedModelitem, (List<BoxedModelitem> zones, List<double> distances, List<double> volumes)> zonesPerPoint)
        {
            var bestZone = new Dictionary<BoxedModelitem, Dictionary<string, string>>();
            int count = 0;

            SetContextState("Definindo melhor ocorrência de clash", zonesPerPoint.Count, 0);

            foreach (var kvp in zonesPerPoint)
            {
                var point = kvp.Key;
                var zones = kvp.Value.zones;
                var distances = kvp.Value.distances;
                var volumes = kvp.Value.volumes;

                int bestIndex = -1;
                double bestVolume = double.MinValue;
                double bestDistance = double.MaxValue;

                for (int i = 0; i < zones.Count; i++)
                {
                    if (volumes[i] > bestVolume || (volumes[i] == bestVolume && distances[i] < bestDistance))
                    {
                        bestVolume = volumes[i];
                        bestDistance = distances[i];
                        bestIndex = i;
                    }
                }

                if (bestIndex >= 0)
                    bestZone[point] = GetCustomProperties(zones[bestIndex].ModelItem);

                SetContextState("Definindo melhor ocorrência de clash", zonesPerPoint.Count, ++count);
            }

            SetContextState("done", 0, 0);
            return bestZone;
        }

        private static void AssignRemainingZonesByContainment(
            HashSet<BoxedModelitem> remainingPoints,
            Dictionary<BoxedModelitem, Dictionary<string, string>> bestZonePerPoint)
        {
            int count = 0;
            SetContextState("Classificando ModelItems restantes", remainingPoints.Count, 0);

            foreach (var point in remainingPoints)
            {
                foreach (var zone in Context.Instance._ZonePoints)
                {
                    if (zone.OrientedBoundingBox == null || point.OrientedBoundingBox == null)
                        continue;

                    bool allInside = point.OrientedBoundingBox.GetCorners().All(corner => zone.OrientedBoundingBox.Contains(corner));
                    if (!allInside) continue;

                    bestZonePerPoint[point] = GetCustomProperties(zone.ModelItem);
                    SetContextState("Classificando ModelItems restantes", remainingPoints.Count, ++count);
                    break;
                }
            }

            SetContextState("done", 0, 0);
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

        private static void SetContextState(string status, int total, int progress)
        {
            Context.Instance._state["state"] = (status, total, progress);
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

            for (int i = test.Children.Count - 1; i >= 0; i--)
            {
                var child = test.Children[i];
                if (child is ClashResult result)
                {
                    clashTests.TestsMove(test, i, resultGroup, 0);
                }
            }

            return resultGroup;
        }

        internal static void CreateClashTest(string name, List<BoxedModelitem> points, List<BoxedModelitem> zones)
        {
            var doc = Application.ActiveDocument;
            var clashTest = CreateClashTest(name, CreateModelItemCollection(points), CreateModelItemCollection(zones));
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
