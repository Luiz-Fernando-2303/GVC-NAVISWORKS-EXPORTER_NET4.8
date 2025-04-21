using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.ComApi;
using Autodesk.Navisworks.Api.Interop.ComApi;
using GVC_EXPORTER_PLUGIN.Functions;
using GVC_EXPORTER_PLUGIN.Functions.Chunks;
using GVC_EXPORTER_PLUGIN.Functions.PointCloud;
using GVC_EXPORTER_PLUGIN.Functions.Tree;
using sw = System.Windows;

namespace GVC_EXPORTER_PLUGIN
{
    /// <summary>
    /// Singleton responsável por armazenar o estado atual da aplicação de chunking e visualização.
    /// </summary>
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
            Clear(); // Inicializa com valores padrão
        }

        public Dictionary<string, ModelItem> _EmptyGuids { get; set; }
        public HashSet<string> _UsedGuids { get; set; }
        public ModelItemCollection _items { get; set; }
        public List<byte[]> _points { get; set; }
        public List<byte[]> _pointsLoacated { get; set; }
        public List<byte[]> _chunks { get; set; }
        public BoundingBox3D _modelBox { get; set; }
        public ChunkTree _ChunkTree { get; set; }
        public OrientedBoundingBox _OBB_modelBox { get; set; }
        public bool _RenderFlag { get; set; }
        public Dictionary<string, (string name, int size, int count)> _metadata { get; set; }

        /// <summary>
        /// Limpa todos os dados da instância, reinicializando o contexto.
        /// </summary>
        public void Clear()
        {
            _EmptyGuids = new Dictionary<string, ModelItem>();
            _UsedGuids = new HashSet<string>();
            _items = new ModelItemCollection();
            _points = new List<byte[]>();
            _pointsLoacated = new List<byte[]>();
            _chunks = new List<byte[]>();
            _modelBox = new BoundingBox3D();
            _ChunkTree = new ChunkTree();
            _OBB_modelBox = new OrientedBoundingBox();
            _RenderFlag = false;
            _metadata = new Dictionary<string, (string, int, int)>();
        }
    }

    /// <summary>
    /// Responsável por inicializar o contexto de chunking com base em parâmetros e estado atual.
    /// </summary>
    public static class OBB_ContextInitializer
    {
        public static void InitializeContext(double x = 2, double y = 2, double z = 1, double rotation = 0.0)
        {
            // Backup do estado atual
            var backup = new
            {
                Context.Instance._EmptyGuids,
                Context.Instance._UsedGuids,
                Context.Instance._items,
                Context.Instance._points,
                Context.Instance._pointsLoacated,
                Context.Instance._chunks,
                Context.Instance._modelBox,
                Context.Instance._OBB_modelBox,
                Context.Instance._RenderFlag,
                Context.Instance._metadata
            };

            try
            {
                Context.Instance.Clear();

                // Seleciona todos os itens com geometria
                Context.Instance._metadata["state"] = ("Coletando items", 0, 0);
                var search = new Search();
                search.Selection.SelectAll();
                search.SearchConditions.Add(SearchCondition.HasCategoryByName(PropertyCategoryNames.Geometry));
                Context.Instance._items = search.FindAll(Application.ActiveDocument, false);
                Context.Instance._metadata["state"] = ("done", 0, 0);

                // Processa pontos binários e OBB
                Context.Instance._metadata["state"] = ("Processando pontos", 0, 0);
                Context.Instance._points = PointCloud_Functions.GetPackedPoints();
                Context.Instance._modelBox = Context.Instance._items.BoundingBox();
                Context.Instance._OBB_modelBox = Chunk_Functions.RotateOBB_Z_FromBoundingBox(Context.Instance._modelBox, rotation, 0);
                Context.Instance._metadata["state"] = ("done", 0, 0);

                // Gera os chunks e localiza os pontos
                Context.Instance._metadata["state"] = ("Gerando chunks", 0, 0);
                var obbs = Chunk_Functions.ChunkOBBsFromOBB(Context.Instance._OBB_modelBox, x, y, z);
                Context.Instance._ChunkTree = Tree_Functions.BuildChunkTreeFromBoxes(obbs);

                Context.Instance._chunks = obbs.Select(Chunk_Functions.PackOBB).ToList();
                Context.Instance._pointsLoacated = Tree_Functions.LocatePointsByChunkTree(Context.Instance._ChunkTree);
                Context.Instance._metadata["state"] = ("done", 0, 0);

                // Habilita renderização
                Context.Instance._RenderFlag = true;
            }
            catch (Exception ex)
            {
                // Restaura estado em caso de erro
                Context.Instance._EmptyGuids = backup._EmptyGuids;
                Context.Instance._UsedGuids = backup._UsedGuids;
                Context.Instance._items = backup._items;
                Context.Instance._points = backup._points;
                Context.Instance._pointsLoacated = backup._pointsLoacated;
                Context.Instance._chunks = backup._chunks;
                Context.Instance._modelBox = backup._modelBox;
                Context.Instance._OBB_modelBox = backup._OBB_modelBox;
                Context.Instance._RenderFlag = backup._RenderFlag;
                Context.Instance._metadata = backup._metadata;

                sw.MessageBox.Show(
                    "Error initializing context: " + ex.Message,
                    "Context Initialization Error",
                    sw.MessageBoxButton.OK,
                    sw.MessageBoxImage.Error
                );
            }
        }
    }

    /// <summary>
    /// Responsável por aplicar as informações dos chunks nos itens do modelo no Navisworks.
    /// </summary>
    public static class ContextToModel
    {
        public static void ApplyChunksToItems()
        {
            var chunkToPointsMap = GroupPointsByChunkId();
            Context.Instance._metadata["state"] = ("Applying chunks to items", chunkToPointsMap.Count, 0);

            int count = 0;
            foreach (var entry in chunkToPointsMap)
            {
                int chunkId = entry.Key;
                string chunkCode = IntToCode(chunkId);
                var packedPoints = entry.Value;

                var modelItems = GetModelItemsFromPoints(packedPoints);
                AssignChunkPropertiesToItems(modelItems, chunkCode);

                count++;
                Context.Instance._metadata["state"] = ("Applying chunks to items", chunkToPointsMap.Count, count);
            }
            Context.Instance._metadata["state"] = ("done", 0, 0);
        }

        private static Dictionary<int, List<byte[]>> GroupPointsByChunkId()
        {
            return Context.Instance._points
                .Select(point => new
                {
                    Point = point,
                    ChunkId = PointCloud_Functions.UnpackPoint(point).ChunkId
                })
                .GroupBy(entry => entry.ChunkId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(e => e.Point).ToList()
                );
        }

        private static ModelItemCollection GetModelItemsFromPoints(List<byte[]> packedPoints)
        {
            Context.Instance._metadata["state"] = ("Getting items from points", packedPoints.Count, 0);

            int count = 0;
            var items = new ModelItemCollection();
            foreach (var point in packedPoints)
            {
                items.Add(PointCloud_Functions.GetModelItemsFromPackedPoint(point));
                count++;
                Context.Instance._metadata["state"] = ("Getting items from points", packedPoints.Count, count);
            }
            return items;
        }

        private static void AssignChunkPropertiesToItems(ModelItemCollection items, string chunkCode)
        {
            Context.Instance._metadata["state"] = ("Assigning chunk properties", items.Count, 0);

            var state = ComApiBridge.State;
            int count = 0;
            foreach (var item in items)
            {
                var comPath = ComApiBridge.ToInwOaPath(item);
                var propertyNode = (InwGUIPropertyNode2)state.GetGUIPropertyNode(comPath, true);
                var propertyVec = CreatePropertyVectorWithChunkCode(chunkCode);
                propertyNode.SetUserDefined(0, "Zona", "Zona", propertyVec);

                count++;
                Context.Instance._metadata["state"] = ("Assigning chunk properties", items.Count, count);
            }
        }

        private static InwOaPropertyVec CreatePropertyVectorWithChunkCode(string chunkCode)
        {
            var state = ComApiBridge.State;

            var propertyVec = (InwOaPropertyVec)state.ObjectFactory(
                nwEObjectType.eObjectType_nwOaPropertyVec, null, null);

            var property = (InwOaProperty)state.ObjectFactory(
                nwEObjectType.eObjectType_nwOaProperty, null, null);

            property.name = "ID";
            property.UserName = "ID";
            property.value = chunkCode;

            propertyVec.Properties().Add(property);
            return propertyVec;
        }

        /// <summary>
        /// Converte um inteiro em um código de letras no estilo Excel (A, B, ..., Z, AA, AB...).
        /// </summary>
        private static string IntToCode(int N)
        {
            const string Letters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            if (N == 0) return Letters[0].ToString();

            string result = string.Empty;
            while (N > 0)
            {
                int rest = N % 36;
                result = Letters[rest] + result;
                N /= 36;
            }

            return result;
        }
    }
}
