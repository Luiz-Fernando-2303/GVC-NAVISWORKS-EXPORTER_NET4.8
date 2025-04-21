using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Navisworks.Api;

namespace GVC_EXPORTER_PLUGIN.Functions.Tree
{
    internal class Tree_Functions
    {
        public static List<byte[]> LocatePointsByChunkTree(ChunkTree root)
        {
            var updatedPoints = new List<byte[]>(Context.Instance._points.Count);
            var usedIds = new HashSet<int>();

            Context.Instance._metadata["state"] = ("Locating points", Context.Instance._points.Count, 0);

            int count = 0;
            foreach (var point in Context.Instance._points)
            {
                double x = BitConverter.ToDouble(point, 16);
                double y = BitConverter.ToDouble(point, 24);
                double z = BitConverter.ToDouble(point, 32);
                var p = new Point3D(x, y, z);

                var leaf = FindLeafContainingPoint(root, p);
                if (leaf != null)
                {
                    Buffer.BlockCopy(BitConverter.GetBytes(leaf.Box.Id), 0, point, 40, 4);
                    updatedPoints.Add(point);
                    usedIds.Add(leaf.Box.Id);
                }

                count++;
                Context.Instance._metadata["state"] = ("Locating points", Context.Instance._points.Count, count);
            }

            Context.Instance._metadata["state"] = ("Clearing empty chunks", 0, 0);
            Context.Instance._chunks = CollectUsedChunks(root, usedIds).Select(Chunks.Chunk_Functions.PackOBB).ToList();

            Context.Instance._metadata["state"] = ("done", 0, 0);
            return updatedPoints;
        }

        private static ChunkTree FindLeafContainingPoint(ChunkTree node, Point3D point)
        {
            if (!node.Box.Contains(point))
                return null;

            if (node.IsLeaf)
                return node;

            foreach (var child in node.Children)
            {
                var found = FindLeafContainingPoint(child, point);
                if (found != null)
                    return found;
            }

            return null;
        }

        private static List<OrientedBoundingBox> CollectUsedChunks(ChunkTree node, HashSet<int> usedIds)
        {
            var result = new List<OrientedBoundingBox>();

            if (usedIds.Contains(node.Box.Id))
                result.Add(node.Box);

            if (!node.IsLeaf)
            {
                foreach (var child in node.Children)
                    result.AddRange(CollectUsedChunks(child, usedIds));
            }

            return result;
        }

        public static ChunkTree BuildChunkTreeFromBoxes(List<OrientedBoundingBox> obbs)
        {
            var leaves = obbs.Select(box => new ChunkTree { Box = box, Children = null }).ToList();
            var d = BuildChunkTreeFromLeaves(leaves);
            return d;
        }

        public static ChunkTree BuildChunkTreeFromLeaves(List<ChunkTree> leaves)
        {
            int nextId = leaves.Max(c => c.Box.Id) + 1;
            var currentLevel = leaves;

            while (currentLevel.Count > 1)
            {
                var minCenter = new Point3D(
                    currentLevel.Min(c => c.Box.Center.X),
                    currentLevel.Min(c => c.Box.Center.Y),
                    currentLevel.Min(c => c.Box.Center.Z)
                );

                double sizeX = currentLevel[0].Box.ExtentX * 2;
                double sizeY = currentLevel[0].Box.ExtentY * 2;
                double sizeZ = currentLevel[0].Box.ExtentZ * 2;

                // Grade com múltiplas caixas por célula
                var grid = new Dictionary<(int, int, int), List<ChunkTree>>();
                var keyMap = new Dictionary<ChunkTree, (int, int, int)>();

                foreach (var chunk in currentLevel)
                {
                    int i = (int)Math.Floor((chunk.Box.Center.X - minCenter.X) / sizeX);
                    int j = (int)Math.Floor((chunk.Box.Center.Y - minCenter.Y) / sizeY);
                    int k = (int)Math.Floor((chunk.Box.Center.Z - minCenter.Z) / sizeZ);
                    var key = (i, j, k);

                    if (!grid.ContainsKey(key))
                        grid[key] = new List<ChunkTree>();

                    grid[key].Add(chunk);
                    keyMap[chunk] = key;
                }

                var visitedChunks = new HashSet<ChunkTree>();
                var nextLevel = new List<ChunkTree>();

                foreach (var kvp in grid)
                {
                    foreach (var chunk in kvp.Value)
                    {
                        if (visitedChunks.Contains(chunk)) continue;

                        List<ChunkTree> bestGroup = null;
                        (int dx, int dy, int dz) bestSize = (1, 1, 1);

                        for (int dx = 2; dx >= 1; dx--)
                        {
                            for (int dy = 2; dy >= 1; dy--)
                            {
                                for (int dz = 2; dz >= 1; dz--)
                                {
                                    var group = new List<ChunkTree>();
                                    bool valid = true;

                                    for (int x = 0; x < dx && valid; x++)
                                        for (int y = 0; y < dy && valid; y++)
                                            for (int z = 0; z < dz && valid; z++)
                                            {
                                                var neighborKey = (kvp.Key.Item1 + x, kvp.Key.Item2 + y, kvp.Key.Item3 + z);
                                                if (grid.TryGetValue(neighborKey, out var list))
                                                {
                                                    foreach (var c in list)
                                                    {
                                                        if (!visitedChunks.Contains(c))
                                                            group.Add(c);
                                                    }
                                                }
                                                else
                                                {
                                                    valid = false;
                                                }
                                            }

                                    if (valid && group.Count > 1 && (bestGroup == null || group.Count > bestGroup.Count))
                                    {
                                        bestGroup = group;
                                        bestSize = (dx, dy, dz);
                                    }
                                }
                            }
                        }

                        if (bestGroup == null)
                        {
                            // Não conseguiu agrupar com mais ninguém
                            visitedChunks.Add(chunk);
                            nextLevel.Add(chunk);
                        }
                        else
                        {
                            foreach (var c in bestGroup)
                                visitedChunks.Add(c);

                            var parent = new ChunkTree
                            {
                                Box = ComputeParentOBBFromCorners(bestGroup, nextId++),
                                Children = bestGroup
                            };
                            nextLevel.Add(parent);
                        }
                    }
                }

                currentLevel = nextLevel;
            }

            return currentLevel[0];
        }

        private static OrientedBoundingBox ComputeParentOBBFromCorners(List<ChunkTree> children, int id)
        {
            var allCorners = new List<Point3D>();
            foreach (var child in children)
                allCorners.AddRange(child.Box.GetCorners());

            var min = new Point3D(
                allCorners.Min(p => p.X),
                allCorners.Min(p => p.Y),
                allCorners.Min(p => p.Z)
            );

            var max = new Point3D(
                allCorners.Max(p => p.X),
                allCorners.Max(p => p.Y),
                allCorners.Max(p => p.Z)
            );

            var center = new Point3D(
                (min.X + max.X) / 2,
                (min.Y + max.Y) / 2,
                (min.Z + max.Z) / 2
            );

            var extentX = (max.X - min.X) / 2;
            var extentY = (max.Y - min.Y) / 2;
            var extentZ = (max.Z - min.Z) / 2;

            var axisX = children[0].Box.AxisX;
            var axisY = children[0].Box.AxisY;
            var axisZ = children[0].Box.AxisZ;

            return new OrientedBoundingBox(id, center, axisX, axisY, axisZ, extentX, extentY, extentZ);
        }
    }
}
