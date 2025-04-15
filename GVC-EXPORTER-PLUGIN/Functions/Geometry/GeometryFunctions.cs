using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.ComApi;
using Autodesk.Navisworks.Api.Interop.ComApi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GVC_EXPORTER_PLUGIN.Functions.Geometry
{
    public class CallbackGeomListener_BuildMesh : InwSimplePrimitivesCB
    {
        public List<int[]> triangles = new List<int[]>(); // [índices dos vértices]
        public List<double[]> vertices = new List<double[]>(); // [x, y, z]
        private Dictionary<string, int> vertexMap = new Dictionary<string, int>();

        public void Line(InwSimpleVertex v1, InwSimpleVertex v2) { }

        public void Point(InwSimpleVertex v) { }

        public void SnapPoint(InwSimpleVertex v1) { }

        public void Triangle(InwSimpleVertex v1, InwSimpleVertex v2, InwSimpleVertex v3)
        {
            int Va = AddVertex(v1);
            int Vb = AddVertex(v2);
            int Vc = AddVertex(v3);

            triangles.Add(new int[] { Va, Vb, Vc });
        }

        private int AddVertex(InwSimpleVertex v)
        {
            double[] vertex = ConvertToVertex(v);
            string key = $"{vertex[0]:F5}|{vertex[1]:F5}|{vertex[2]:F5}";

            if (vertexMap.TryGetValue(key, out int existingIndex))
            {
                return existingIndex;
            }

            vertices.Add(vertex);
            int newIndex = vertices.Count;
            vertexMap[key] = newIndex;
            return newIndex;
        }

        private double[] ConvertToVertex(InwSimpleVertex v)
        {
            if (v.coord is Array array && array.Length >= 3)
            {
                float[] coords = array.Cast<float>().ToArray();
                return new double[]
                {
                    coords[0],
                    coords[1],
                    coords[2]
                };
            }
            return new double[] { 0, 0, 0 };
        }
    }

    public static class GeometryFunctions
    {
        public static void SaveSelection(object selection, string selectionName)
        {
            ModelItemCollection collection = null;
            if (selection is ModelItemCollection col)
                collection = col;
            else if (selection is ModelItem item)
                collection = new ModelItemCollection { item };
            else
                throw new InvalidCastException($"Tipo de modelo '{selection.GetType().Name}' não suportado. Esperado: ModelItemCollection ou ModelItem.");

            var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = 10 };

            Parallel.ForEach(
                ComApiBridge.ToInwOpSelection(collection).Paths().Cast<InwOaPath3>(),
                parallelOptions,
                path =>
                {
                    foreach (InwOaFragment3 frag in path.Fragments())
                    {
                        var callback = new CallbackGeomListener_BuildMesh();
                        frag.GenerateSimplePrimitives(nwEVertexProperty.eNORMAL, callback);
                        callback = null;
                    }
                }
            );
        }
    }
}
