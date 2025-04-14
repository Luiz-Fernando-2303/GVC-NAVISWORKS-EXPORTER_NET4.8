using Autodesk.Navisworks.Api;
using System.IO.Compression;
using System.Text;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System;

namespace GVC_EXPORTER_PLUGIN.Functions
{
    public class PointCloud_ 
    {
        public ModelItemCollection Items = new ModelItemCollection();
        public List<Point3D> Point = new List<Point3D>();
        public List<(ModelItem item, Point3D point)> ToList()
        {
            var list = new List<(ModelItem item, Point3D point)>();
            for (int i = 0; i < Items.Count; i++)
            {
                list.Add((Items[i], Point[i]));
            }
            return list;
        }
    }

    public class Chunk
    {
        public BoundingBox3D BoundingBox = new BoundingBox3D();
        public ModelItemCollection Items = new ModelItemCollection();
        public PointCloud_ PointCloud = new PointCloud_();
        public string name { get; set; }
    }

    public class CompressedRenderModel
    {
        private string _compressedData;

        public string Name { get; private set; }

        public CompressedRenderModel(List<int[]> triangles, List<double[]> vertices, string name)
        {
            Name = name;
            _compressedData = CompressInternally(triangles, vertices, name);
        }

        public CompressedRenderModel(string compressedBase64, string name)
        {
            _compressedData = compressedBase64;
            Name = name;
        }

        public string GetCompressedData() => _compressedData;

        public Tuple<List<int[]>, List<double[]>, string> Decompress()
        {
            byte[] compressedBytes = Convert.FromBase64String(_compressedData);

            using (var msi = new MemoryStream(compressedBytes))
            using (var mso = new MemoryStream())
            {
                using (var gs = new GZipStream(msi, CompressionMode.Decompress))
                {
                    gs.CopyTo(mso);
                }

                string json = Encoding.UTF8.GetString(mso.ToArray());
                var decompressed = JsonConvert.DeserializeObject<RenderModelData>(json);

                return Tuple.Create(decompressed.Triangles, decompressed.Vertices, decompressed.Name);
            }
        }

        private class RenderModelData
        {
            public List<int[]> Triangles { get; set; }
            public List<double[]> Vertices { get; set; }
            public string Name { get; set; }
        }

        private string CompressInternally(List<int[]> triangles, List<double[]> vertices, string name)
        {
            var data = new RenderModelData
            {
                Triangles = triangles,
                Vertices = vertices,
                Name = name
            };

            string json = JsonConvert.SerializeObject(data);
            byte[] jsonBytes = Encoding.UTF8.GetBytes(json);

            using (var msi = new MemoryStream(jsonBytes))
            using (var mso = new MemoryStream())
            {
                using (var gs = new GZipStream(mso, CompressionMode.Compress))
                {
                    msi.CopyTo(gs);
                }
                return Convert.ToBase64String(mso.ToArray());
            }
        }
    }
}


//public static List<CompressedRenderModel> CompressedSelection(object selection, string selectionName)
//{
//    ConcurrentBag<Tuple<List<int[]>, List<double[]>, string>> bag =
//        new ConcurrentBag<Tuple<List<int[]>, List<double[]>, string>>();

//    ModelItemCollection collection = null;
//    if (selection is ModelItemCollection col)
//        collection = col;
//    else if (selection is ModelItem item)
//        collection = new ModelItemCollection { item };
//    else
//        throw new InvalidCastException($"Tipo de modelo '{selection.GetType().Name}' não suportado. Esperado: ModelItemCollection ou ModelItem.");

//    var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = 100 };
//    foreach (InwOaPath3 path in ComApiBridge.ToInwOpSelection(collection).Paths())
//    {
//        Parallel.ForEach(path.Fragments().Cast<InwOaFragment3>(), parallelOptions, frag =>
//        {
//            var callback = new CallbackGeomListener_BuildMesh();
//            frag.GenerateSimplePrimitives(nwEVertexProperty.eNORMAL, callback);

//            string modelName = selectionName + "_" + path.ObjectName;
//            var tuple = Tuple.Create(callback.triangles, callback.vertices, modelName);
//            bag.Add(tuple);

//            callback = null;
//        });
//    }

//    var models = new List<CompressedRenderModel>();
//    foreach (var model in bag) models.Add(new CompressedRenderModel(model.Item1, model.Item2, model.Item3));

//    return models;
//}