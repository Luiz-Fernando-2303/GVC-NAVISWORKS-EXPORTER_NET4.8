using System;
using System.Collections.Generic;
using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.Plugins;
using GVC_EXPORTER_PLUGIN.Functions._Binary.ChunkTree;
using static GVC_EXPORTER_PLUGIN.Functions._Binary.Chunks_Oriented;

namespace GVC_EXPORTER_PLUGIN.Functions._Binary
{
    /// <summary>
    /// Plugin responsável pela renderização visual dos OBBs do modelo e dos chunks gerados.
    /// </summary>
    public class OBB_Render : RenderPlugin
    {
        /// <summary>
        /// Função principal de renderização chamada pelo Navisworks.
        /// </summary>
        public override void Render(View view, Graphics graphics)
        {
            RenderModelOBB(view, graphics);
            RenderChunks(view, graphics);
            //RenderChunkTree(graphics, Context.Instance._chunkTree);
        }

        /// <summary>
        /// Renderiza recursivamente um ChunkTree com suas caixas OBB.
        /// </summary>
        private void RenderChunkTree(Graphics graphics, Tree.ChunkTree node, int depth = 0)
        {
            if (node.Box == null) return;

            // Exemplo: alternar cor por profundidade
            var color = GetColorByDepth(depth);
            DrawOBB(graphics, node.Box, color, 1);

            if (node.IsLeaf)
                return;

            foreach (var child in node.Children)
            {
                RenderChunkTree(graphics, child, depth + 1);
            }
        }

        /// <summary>
        /// Renderiza o OBB principal do modelo com eixos em vermelho (X), verde (Y) e azul (Z).
        /// </summary>
        private void RenderModelOBB(View view, Graphics graphics)
        {
            var obb = Context.Instance._OBB_modelBox;
            if (!Context.Instance._RenderFlag || obb == null) return;

            DrawOBB(graphics, obb, Color.Blue, 2);

            // Desenha eixos principais
            double length = Math.Min(obb.ExtentX, Math.Min(obb.ExtentY, obb.ExtentZ)) * 2;
            var center = obb.Center;

            graphics.Color(Color.Red, 1.0); graphics.Line(center, center + obb.AxisX * length);
            graphics.Color(Color.Green, 1.0); graphics.Line(center, center + obb.AxisY * length);
            graphics.Color(Color.Blue, 1.0); graphics.Line(center, center + obb.AxisZ * length);
        }

        /// <summary>
        /// Renderiza todos os chunks armazenados no contexto como caixas OBB.
        /// </summary>
        public void RenderChunks(View view, Graphics graphics)
        {
            if (!Context.Instance._RenderFlag) return;

            foreach (var chunk in Context.Instance._chunks)
            {
                var obb = UnpackOBB(chunk);
                DrawOBB(graphics, obb, Color.Red, 1);
            }
        }

        /// <summary>
        /// Desenha uma Oriented Bounding Box com a cor e espessura especificadas.
        /// </summary>
        private void DrawOBB(Graphics graphics, OrientedBoundingBox obb, Color color, int lineWidth)
        {
            var vertices = GetOBBVertices(obb);
            var edges = GetBoxEdges();

            graphics.LineWidth(lineWidth);
            graphics.Color(color, 1.0);

            foreach (var (a, b) in edges)
            {
                graphics.Line(vertices[a], vertices[b]);
            }
        }

        /// <summary>
        /// Retorna os 8 vértices de uma Oriented Bounding Box.
        /// </summary>
        private List<Point3D> GetOBBVertices(OrientedBoundingBox obb)
        {
            var cx = obb.Center;
            var ex = obb.AxisX * obb.ExtentX;
            var ey = obb.AxisY * obb.ExtentY;
            var ez = obb.AxisZ * obb.ExtentZ;

            return new List<Point3D>
            {
                cx - ex - ey - ez, // 0
                cx + ex - ey - ez, // 1
                cx + ex + ey - ez, // 2
                cx - ex + ey - ez, // 3
                cx - ex - ey + ez, // 4
                cx + ex - ey + ez, // 5
                cx + ex + ey + ez, // 6
                cx - ex + ey + ez  // 7
            };
        }

        /// <summary>
        /// Define as 12 arestas da caixa 3D a partir dos vértices numerados.
        /// </summary>
        private List<(int, int)> GetBoxEdges()
        {
            return new List<(int, int)>
            {
                (0, 1), (1, 2), (2, 3), (3, 0), // base inferior
                (4, 5), (5, 6), (6, 7), (7, 4), // topo superior
                (0, 4), (1, 5), (2, 6), (3, 7)  // colunas verticais
            };
        }

        /// <summary>
        /// Retorna uma cor baseada na profundidade do nó usando RGB direto.
        /// </summary>
        private Color GetColorByDepth(int depth)
        {
            switch (depth % 5)
            {
                case 0: return Color.FromByteRGB(255, 0, 0);     // Vermelho
                case 1: return Color.FromByteRGB(0, 255, 0);     // Verde
                case 2: return Color.FromByteRGB(0, 0, 255);     // Azul
                case 3: return Color.FromByteRGB(255, 165, 0);   // Laranja
                case 4: return Color.FromByteRGB(255, 0, 255);   // Magenta
                default: return Color.FromByteRGB(200, 200, 200); // Cinza claro (fallback)
            }
        }
    }
}
