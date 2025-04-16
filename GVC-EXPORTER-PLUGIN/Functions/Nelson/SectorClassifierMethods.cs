using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.Plugins;

namespace GVC_EXPORTER_PLUGIN.Functions.Nelson
{
    /// <summary>
    /// Provides methods to classify elements by their spatial relationships using bounding boxes.
    /// </summary>
    internal static class SectorClassifierBoundingBoxesMethods
    {
        /// <summary>
        /// Returns a list of elements that intersect with a reference item's bounding box,
        /// filtered by a minimum percentage of intersection.
        /// </summary>
        public static List<ModelItem> GetIntersectingElementsByPercentage(Document doc, ModelItem referenceItem, double percentageThreshold = 0)
        {
            var results = new List<ModelItem>();
            var refBox = referenceItem.BoundingBox();

            foreach (var item in doc.Models.First.RootItem.DescendantsAndSelf)
            {
                if (item == referenceItem) continue;

                var itemBox = item.BoundingBox();
                if (itemBox == null) continue;

                var itemVolume = GetBoxVolume(itemBox);
                if (itemVolume <= 0) continue;

                var intersection = GetIntersectionBox(refBox, itemBox);
                if (intersection == null) continue;

                var intersectionVolume = GetBoxVolume(intersection);
                var intersectingPercentage = (intersectionVolume / itemVolume) * 100;

                if (intersectingPercentage > percentageThreshold)
                    results.Add(item);
            }

            return results;
        }

        /// <summary>
        /// Struct to represent hierarchical groupings of bounding boxes.
        /// </summary>
        public struct GroupedBoundingBoxes
        {
            public string Name;
            public BoundingBox3D BoundingBox;
            public List<GroupedBoundingBoxes> Children;
        }

        /// <summary>
        /// Saves a hierarchical structure of bounding boxes as a TSV string.
        /// (Currently not implemented fully.)
        /// </summary>
        public static string SaveGroupedBoundingBoxesAsTsv(GroupedBoundingBoxes groupedBoundingBoxes, char separator = '\t', char guidsSeparator = ',')
        {
            var sb = new StringBuilder();
            sb.AppendLine("FilePath" + separator + "Sector" + separator + "guids");
            // Future implementation placeholder
            return sb.ToString();
        }

        /// <summary>
        /// Recursively concatenates all file names in the hierarchy with a separator.
        /// </summary>
        static string ConcatFileNames(GroupedBoundingBoxes groupedBoundingBoxes, string result, string filePathSeparator = " / ")
        {
            if (groupedBoundingBoxes.Children == null || groupedBoundingBoxes.Children.Count == 0)
                return result + groupedBoundingBoxes.Name;

            foreach (var child in groupedBoundingBoxes.Children)
                result = ConcatFileNames(child, result + (string.IsNullOrEmpty(result) ? "" : filePathSeparator));

            return result;
        }

        /// <summary>
        /// Recursively populates the results structure with bounding boxes and GUIDs for all elements in the model hierarchy.
        /// </summary>
        public static void AddBoundingBoxes(GroupedBoundingBoxes results, ModelItem item, float projectAzimuth = 0, Point3D projectCenter = null)
        {
            PropertyCategory itemProps = item.PropertyCategories.FirstOrDefault(x => x.DisplayName == "Item");
            if (itemProps == null) return;

            bool isFile = IsFileNode(item);
            string nameOrGuid = isFile
                ? itemProps.Properties.FirstOrDefault(x => x.DisplayName == "Name")?.Value?.ToDisplayString()
                : GetSolidParentGuid(item);

            BoundingBox3D boundingBox = isFile ? null : item.BoundingBox();

            // Aplica rotação se houver centro de projeto e azimute definido
            if (boundingBox != null && projectCenter != null)
            {
                double radians = projectAzimuth * Math.PI / 180;
                Point3D rotatedMin = RotatePoint(boundingBox.Min - projectCenter, radians) + projectCenter;
                Point3D rotatedMax = RotatePoint(boundingBox.Max - projectCenter, radians) + projectCenter;

                boundingBox = new BoundingBox3D(
                    new Point3D(
                        Math.Min(rotatedMin.X, rotatedMax.X),
                        Math.Min(rotatedMin.Y, rotatedMax.Y),
                        Math.Min(rotatedMin.Z, rotatedMax.Z)),
                    new Point3D(
                        Math.Max(rotatedMin.X, rotatedMax.X),
                        Math.Max(rotatedMin.Y, rotatedMax.Y),
                        Math.Max(rotatedMin.Z, rotatedMax.Z))
                );
            }

            GroupedBoundingBoxes groupedBox = new GroupedBoundingBoxes()
            {
                Name = nameOrGuid ?? "",
                BoundingBox = boundingBox,
                Children = new List<GroupedBoundingBoxes>()
            };

            results.Children.Add(groupedBox);

            var childrenFiles = item.Children.Where(x => IsFileNode(x));
            var children = childrenFiles.Any() ? childrenFiles.ToArray() : GetModelElements(item).ToArray();

            foreach (var child in children)
                AddBoundingBoxes(groupedBox, child, projectAzimuth, projectCenter);
        }

        /// <summary>
        /// Gets parent GUID of solid geometry item.
        /// </summary>
        static string GetSolidParentGuid(ModelItem solid)
        {
            return solid.Parent.InstanceGuid.ToString();
        }

        /// <summary>
        /// Rotates a vector in the XY plane.
        /// </summary>
        static Vector3D RotatePoint(Vector3D point, double angle)
        {
            var x = point.X * Math.Cos(angle) - point.Y * Math.Sin(angle);
            var y = point.X * Math.Sin(angle) + point.Y * Math.Cos(angle);
            return new Vector3D(x, y, point.Z);
        }

        /// <summary>
        /// Gets all solid model elements (excluding abstract containers and non-physical elements).
        /// </summary>
        public static IEnumerable<ModelItem> GetModelElements(ModelItem model)
        {
            return model.Descendants.Where(item => item.ClassDisplayName == "Solid");
        }

        /// <summary>
        /// Determines whether a model item is an abstract grouping container.
        /// </summary>
        private static bool IsAbstractContainer(ModelItem item)
        {
            return item.PropertyCategories.All(cat => cat.Properties.Count == 0)
                || item.DisplayName.StartsWith("Type")
                || item.DisplayName.StartsWith("Family");
        }

        /// <summary>
        /// Returns intersecting elements and intersection volume with a reference item.
        /// </summary>
        public static List<(ModelItem Item, double Volume)> GetIntersectingElementsAndVolume(Document doc, ModelItem referenceItem)
        {
            var results = new List<(ModelItem, double)>();
            var refBox = referenceItem.BoundingBox();

            foreach (var item in doc.Models.First.RootItem.Descendants)
            {
                if (item == referenceItem) continue;

                var itemBox = item.BoundingBox();
                if (itemBox == null) continue;

                var intersection = GetIntersectionBox(refBox, itemBox);
                if (intersection != null)
                {
                    var volume = GetBoxVolume(intersection);
                    results.Add((item, volume));
                }
            }

            return results;
        }

        /// <summary>
        /// Gets all model items intersecting the reference item's bounding box.
        /// </summary>
        public static List<ModelItem> GetIntersectingElements(Document doc, ModelItem referenceItem)
        {
            var results = new List<ModelItem>();
            BoundingBox3D refBox = referenceItem.BoundingBox();

            foreach (var item in doc.Models.First.RootItem.DescendantsAndSelf)
            {
                if (item == referenceItem) continue;

                var itemBox = item.BoundingBox();
                if (itemBox == null) continue;

                if (DoBoxesIntersect(refBox, itemBox))
                    results.Add(item);
            }

            return results;
        }

        /// <summary>
        /// Calculates the volume of a bounding box.
        /// </summary>
        private static double GetBoxVolume(BoundingBox3D box)
        {
            var size = box.Max - box.Min;
            return size.X * size.Y * size.Z;
        }

        /// <summary>
        /// Returns the intersection box between two bounding boxes, or null if none.
        /// </summary>
        private static BoundingBox3D GetIntersectionBox(BoundingBox3D box1, BoundingBox3D box2)
        {
            var min = new Point3D(
                Math.Max(box1.Min.X, box2.Min.X),
                Math.Max(box1.Min.Y, box2.Min.Y),
                Math.Max(box1.Min.Z, box2.Min.Z));

            var max = new Point3D(
                Math.Min(box1.Max.X, box2.Max.X),
                Math.Min(box1.Max.Y, box2.Max.Y),
                Math.Min(box1.Max.Z, box2.Max.Z));

            if (min.X > max.X || min.Y > max.Y || min.Z > max.Z)
                return null;

            return new BoundingBox3D(min, max);
        }

        /// <summary>
        /// Checks if two bounding boxes intersect.
        /// </summary>
        private static bool DoBoxesIntersect(BoundingBox3D box1, BoundingBox3D box2)
        {
            return !(box1.Min.X > box2.Max.X || box2.Min.X > box1.Max.X ||
                     box1.Min.Y > box2.Max.Y || box2.Min.Y > box1.Max.Y ||
                     box1.Min.Z > box2.Max.Z || box2.Min.Z > box1.Max.Z);
        }

        /// <summary>
        /// Verifies if the model item is a file node based on its type property.
        /// </summary>
        private static bool IsFileNode(ModelItem item)
        {
            var itemProps = item.PropertyCategories.FirstOrDefault(x => x.DisplayName == "Item");
            if (itemProps is null) return false;

            var type = itemProps.Properties.FirstOrDefault(x => x.DisplayName == "Type");
            if (type is null) return false;

            var value = type.Value.ToDisplayString();
            return value != null && value == "File";
        }
    }

    /// <summary>
    /// Entry point plugin for executing sector classification with bounding boxes.
    /// </summary>
    public class SectorClassifierPlugin : AddInPlugin
    {
        public override int Execute(params string[] parameters)
        {
            try
            {
                var doc = Application.MainDocument;
                var results = new SectorClassifierBoundingBoxesMethods.GroupedBoundingBoxes
                {
                    Children = new List<SectorClassifierBoundingBoxesMethods.GroupedBoundingBoxes>()
                };

                var projectCenter = doc.Models.First.RootItem.BoundingBox().Center;
                float projectAzimuth = -41.84f;

                SectorClassifierBoundingBoxesMethods.AddBoundingBoxes(
                    results,
                    doc.Models.First.RootItem,
                    projectAzimuth,
                    projectCenter
                );
            }
            catch (Exception)
            {
                return 0; // Failure
            }

            return 1; // Success
        }
    }
}
