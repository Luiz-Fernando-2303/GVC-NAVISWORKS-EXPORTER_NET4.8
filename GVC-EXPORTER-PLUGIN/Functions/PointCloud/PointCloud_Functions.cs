using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Autodesk.Navisworks.Api;

namespace GVC_EXPORTER_PLUGIN.Functions.PointCloud
{
    /// <summary>
    /// Fornece utilitários para extração e empacotamento de dados de nuvem de pontos a partir de ModelItems do Navisworks.
    /// </summary>
    internal class PointCloud_Functions
    {
        /// <summary>
        /// Recupera e empacota os pontos centrais de todos os ModelItems com geometria em arrays de bytes.
        /// Cada array representa um item único como: [GUID(16)][X(8)][Y(8)][Z(8)][ChunkID(4)].
        /// </summary>
        /// <returns>Uma lista de arrays de bytes representando os pontos centrais dos itens do modelo.</returns>
        public static List<byte[]> GetPackedPoints()
        {
            var modelItems = Context.Instance._items;
            var packedPoints = new ConcurrentBag<byte[]>();
            Context.Instance._metadata["state"] = ("Packing points", modelItems.Count, 0);

            var count = 0;
            Parallel.ForEach(modelItems.OfType<ModelItem>(), modelItem =>
            {
                var packed = PackModelItemCenter(modelItem);
                if (packed != null)
                {
                    packedPoints.Add(packed);
                }
                count++;
                Context.Instance._metadata["state"] = ("Packing points", modelItems.Count, count);
            });

            return packedPoints.ToList();
        }

        /// <summary>
        /// Empacota o InstanceGuid e as coordenadas do centro do ModelItem em um array de 44 bytes.
        /// Formato: [GUID(16)][X(8)][Y(8)][Z(8)][ChunkID(4)]
        /// </summary>
        /// <param name="modelItem">O ModelItem a ser empacotado.</param>
        /// <returns>Um array de 44 bytes representando o item, ou null se o GUID estiver vazio.</returns>
        public static byte[] PackModelItemCenter(ModelItem modelItem)
        {
            Guid guid = Guid.NewGuid();

            try
            {
                if (Context.Instance._EmptyGuids == null)
                    Context.Instance._EmptyGuids = new Dictionary<string, ModelItem>();

                if (Context.Instance._UsedGuids == null)
                    Context.Instance._UsedGuids = new HashSet<string>();

                do
                {
                    guid = Guid.NewGuid();
                }
                while (Context.Instance._UsedGuids.Contains(guid.ToString()));

                Context.Instance._UsedGuids.Add(guid.ToString());
                Context.Instance._EmptyGuids[guid.ToString()] = modelItem;
            }
            catch (Exception)
            {
                return null;
            }

            try
            {
                var data = new byte[44]; // 16 GUID + 24 Center + 4 ChunkId

                // GUID
                Buffer.BlockCopy(guid.ToByteArray(), 0, data, 0, 16);

                // Centro (XYZ)
                var center = modelItem.BoundingBox().Center;
                Buffer.BlockCopy(BitConverter.GetBytes(center.X), 0, data, 16, 8);
                Buffer.BlockCopy(BitConverter.GetBytes(center.Y), 0, data, 24, 8);
                Buffer.BlockCopy(BitConverter.GetBytes(center.Z), 0, data, 32, 8);

                // Chunk ID será preenchido posteriormente (posição 40 a 43)
                Buffer.BlockCopy(BitConverter.GetBytes(0), 0, data, 40, 4); // valor padrão: 0

                return data;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Procura ModelItems que correspondam ao GUID codificado no array de ponto empacotado.
        /// </summary>
        /// <param name="packedPoint">O array de 44 bytes contendo o GUID e o ponto central.</param>
        /// <returns>Uma coleção de ModelItems que correspondem ao GUID codificado.</returns>
        public static ModelItemCollection GetModelItemsFromPackedPoint(byte[] packedPoint)
        {
            byte[] guidBytes = new byte[16];
            Buffer.BlockCopy(packedPoint, 0, guidBytes, 0, 16);
            Guid targetGuid = new Guid(guidBytes);

            var foundItems = new ModelItemCollection();
            var allItems = Context.Instance._items;

            var EmptySearch = Context.Instance._EmptyGuids.FirstOrDefault(x => x.Key == targetGuid.ToString()).Value;
            if (EmptySearch != default)
            {
                foundItems.Add(EmptySearch);
            }

            return foundItems;
        }

        /// <summary>
        /// Desempacota um array de ponto em seus componentes: GUID, coordenadas e ChunkId.
        /// </summary>
        /// <param name="packedPoint">O array de bytes empacotado representando um ponto.</param>
        /// <returns>Uma tupla contendo o GUID, coordenadas X/Y/Z e ChunkId.</returns>
        public static (Guid guid, double X, double Y, double Z, int ChunkId) UnpackPoint(byte[] packedPoint)
        {
            byte[] guidBytes = new byte[16];
            Buffer.BlockCopy(packedPoint, 0, guidBytes, 0, 16);
            Guid guid = new Guid(guidBytes);

            byte[] idBytes = new byte[4];
            Buffer.BlockCopy(packedPoint, 40, idBytes, 0, 4);
            int chunkId = BitConverter.ToInt32(idBytes, 0);

            double x = BitConverter.ToDouble(packedPoint, 16);
            double y = BitConverter.ToDouble(packedPoint, 24);
            double z = BitConverter.ToDouble(packedPoint, 32);

            return (guid, x, y, z, chunkId);
        }
    }
}
