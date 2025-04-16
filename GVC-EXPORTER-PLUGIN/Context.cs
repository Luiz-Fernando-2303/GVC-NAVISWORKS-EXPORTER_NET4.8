using System.Collections.Generic;
using Autodesk.Navisworks.Api;
using GVC_EXPORTER_PLUGIN.Functions;
using GVC_EXPORTER_PLUGIN.Functions.Chunks;
using GVC_EXPORTER_PLUGIN.Functions.PointCloud;

namespace GVC_EXPORTER_PLUGIN
{
    internal class Context
    {
        private static Context _instance;
        public static Context Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new Context();
                }
                return _instance;
            }
        }

        public Context()
        {
            ModelBox = new BoundingBox3D();
            ModelPointCloud = new PointCloud_();
            chunks = new List<Chunk>();
            Metadata = new Dictionary<string, object>();
        }

        public BoundingBox3D ModelBox { get; set; }
        public PointCloud_ ModelPointCloud { get; set; }
        public List<Chunk> chunks { get; set; }
        public Dictionary<string, object> Metadata { get; set; }

        public void Clear()
        {
            ModelBox = new BoundingBox3D();
            ModelPointCloud = new PointCloud_();
            chunks.Clear();
            Metadata.Clear();
        }
    }

    public static class ContextInitializer
    {
        public static void InitializeContext(int x = 2, int y = 2, int z = 1, int chunkThreads = 2)
        {
            var originalModelPointCloud = Context.Instance.ModelPointCloud;
            var originalModelBox = Context.Instance.ModelBox;
            var originalChunks = Context.Instance.chunks != null
                ? new List<Chunk>(Context.Instance.chunks)
                : null;

            try
            {
                PointCloudFunctions.ParallelModelPointCloud();

                var search = new Search();
                search.Selection.SelectAll();
                search.SearchConditions.Add(
                    SearchCondition.HasCategoryByName(PropertyCategoryNames.Geometry)
                );
                var collection = search.FindAll(Application.ActiveDocument, false);
                var debug = collection[10].Transform;

                Context.Instance.ModelBox = collection.BoundingBox();

                ChuncksFunctions.BuildChunks(x, y, z, chunkThreads);
            }
            catch
            {
                Context.Instance.ModelPointCloud = originalModelPointCloud;
                Context.Instance.ModelBox = originalModelBox;
                Context.Instance.chunks = originalChunks;

                throw;
            }
        }

        public static void ChunskToSets(Chunk add = null)
        {
            if (add != null)
            {
                var set = new Autodesk.Navisworks.Api.SelectionSet { DisplayName = add.name };
                set.ExplicitModelItems.AddRange(add.Items);
                Autodesk.Navisworks.Api.Application.ActiveDocument.SelectionSets.AddCopy(set);
            } else
            {
                for (int i = 0; i < Context.Instance.chunks.Count; i++)
                {
                    var chunk = Context.Instance.chunks[i];

                    var set = new Autodesk.Navisworks.Api.SelectionSet { DisplayName = chunk.name };
                    set.ExplicitModelItems.AddRange(chunk.Items);
                    Autodesk.Navisworks.Api.Application.ActiveDocument.SelectionSets.AddCopy(set);
                }
            }
        }
    }
}
