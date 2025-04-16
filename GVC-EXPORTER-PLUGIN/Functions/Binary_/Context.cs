using System.Collections.Generic;
using Autodesk.Navisworks.Api;

namespace GVC_EXPORTER_PLUGIN.Functions.Binary_
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
            _items = new ModelItemCollection();
            _points = new List<byte[]>();
            _pointsLoacated = new List<byte[]>();
            _chunks = new List<byte[]>();
        }

        public ModelItemCollection _items { get; set; }
        public List<byte[]> _points { get; set; }
        public List<byte[]> _pointsLoacated { get; set; } = new List<byte[]>();
        public List<byte[]> _chunks { get; set; }

        public void Clear()
        {
            _items = new ModelItemCollection();
            _points = new List<byte[]>();
            _pointsLoacated = new List<byte[]>();
            _chunks = new List<byte[]>();
        }
    }

    public static class ContextInitializer
    {
        public static void InitializeContext(int x = 2, int y = 2, int z = 1)
        {
            var o_items = Context.Instance._items;
            var o_points = Context.Instance._points;
            var o_pointsLoacated = Context.Instance._pointsLoacated;
            var o_chunks = Context.Instance._chunks;

            try
            {
                var search = new Search();
                search.Selection.SelectAll();
                search.SearchConditions.Add(SearchCondition.HasCategoryByName(PropertyCategoryNames.Geometry));
                Context.Instance._items = search.FindAll(Application.ActiveDocument, false);

                Context.Instance._points = PointCloudExporter.GetPackedPoints();

                Context.Instance._chunks = Chunks.GetChunksBinary(x, y, z);

                Context.Instance._pointsLoacated = Chunks.LocatePoints();

            }
            catch
            {
                Context.Instance._items = o_items;
                Context.Instance._points = o_points;
                Context.Instance._pointsLoacated = o_pointsLoacated;
                Context.Instance._chunks = o_chunks;

                throw;
            }
        }
    }
}
