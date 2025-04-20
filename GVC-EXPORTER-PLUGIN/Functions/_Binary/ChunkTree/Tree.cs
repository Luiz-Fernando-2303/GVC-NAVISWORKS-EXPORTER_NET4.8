using System;
using System.Collections.Generic;
using Autodesk.Navisworks.Api;

namespace GVC_EXPORTER_PLUGIN.Functions._Binary.ChunkTree
{
    internal class Tree
    {
        /// <summary>
        /// Representa um nó da árvore de subdivisão espacial contendo um OBB e seus filhos.
        /// </summary>
        public struct ChunkTree
        {
            public Chunks_Oriented.OrientedBoundingBox Box;
            public List<ChunkTree> Children;

            /// <summary>
            /// Indica se o nó é uma folha (sem filhos).
            /// </summary>
            public bool IsLeaf => Children == null || Children.Count == 0;
        }

        /// <summary>
        /// Constrói a árvore de chunks a partir de uma caixa inicial, subdividindo-a recursivamente até o tamanho mínimo.
        /// </summary>
        public static ChunkTree BuildChunkTree(Chunks_Oriented.OrientedBoundingBox rootBox, double minSizeX, double minSizeY, double minSizeZ)
        {
            int idCounter = 0;
            return SubdivideRecursively(rootBox, minSizeX, minSizeY, minSizeZ, ref idCounter);
        }

        /// <summary>
        /// Verifica se uma caixa pode ser subdividida com base nos tamanhos mínimos.
        /// </summary>
        private static bool CanSubdivide(Chunks_Oriented.OrientedBoundingBox obb, double minSizeX, double minSizeY, double minSizeZ)
        {
            return (obb.ExtentX * 2 >= minSizeX * 4) &&
                   (obb.ExtentY * 2 >= minSizeY * 4) &&
                   (obb.ExtentZ * 2 >= minSizeZ * 4);
        }

        /// <summary>
        /// Subdivide uma caixa em até 4 filhos (plano XY), gerando recursivamente a árvore.
        /// </summary>
        private static ChunkTree SubdivideRecursively(Chunks_Oriented.OrientedBoundingBox obb, double minSizeX, double minSizeY, double minSizeZ, ref int idCounter)
        {
            obb.Id = idCounter++;

            var node = new ChunkTree
            {
                Box = obb,
                Children = new List<ChunkTree>()
            };

            if (!CanSubdivide(obb, minSizeX, minSizeY, minSizeZ))
                return node;

            var children = SubdivideIntoFour(obb);

            foreach (var child in children)
            {
                var childNode = SubdivideRecursively(child, minSizeX, minSizeY, minSizeZ, ref idCounter);
                node.Children.Add(childNode);
            }

            return node;
        }

        /// <summary>
        /// Subdivide uma caixa em quatro subcaixas no plano XY, mantendo o mesmo eixo Z.
        /// </summary>
        private static List<Chunks_Oriented.OrientedBoundingBox> SubdivideIntoFour(Chunks_Oriented.OrientedBoundingBox parent)
        {
            var halfX = parent.ExtentX / 2.0;
            var halfY = parent.ExtentY / 2.0;

            var offsetsX = new[] { -halfX, halfX };
            var offsetsY = new[] { -halfY, halfY };

            var children = new List<Chunks_Oriented.OrientedBoundingBox>();

            foreach (var dx in offsetsX)
            {
                foreach (var dy in offsetsY)
                {
                    var center = parent.Center
                                 + parent.AxisX * dx
                                 + parent.AxisY * dy;

                    children.Add(new Chunks_Oriented.OrientedBoundingBox(
                        id: -1,
                        center,
                        parent.AxisX,
                        parent.AxisY,
                        parent.AxisZ,
                        halfX,
                        halfY,
                        parent.ExtentZ
                    ));
                }
            }

            return children;
        }
    }
}
