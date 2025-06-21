using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace DWHITE.Editor
{
    /// <summary>
    /// Tile Map Editor 的扩展工具类
    /// 提供批量操作、地形生成、优化等高级功能
    /// </summary>
    public static class TileMapEditorExtensions
    {
        #region 地形生成工具
        /// <summary>
        /// 生成基础平台地形
        /// </summary>
        public static void GenerateBasePlatform(Vector3 center, int width, int depth, GameObject terrainTile)
        {
            if (terrainTile == null)
            {
                Debug.LogError("请先选择地形 Tile！");
                return;
            }

            Undo.SetCurrentGroupName("Generate Base Platform");
            int group = Undo.GetCurrentGroup();

            for (int x = 0; x < width; x++)
            {
                for (int z = 0; z < depth; z++)
                {                    Vector3 position = center + new Vector3(
                        (x - width / 2) * TileMapEditorConstants.GRID_SIZE,
                        0,
                        (z - depth / 2) * TileMapEditorConstants.GRID_SIZE
                    );

                    GameObject instance = Object.Instantiate(terrainTile, position, Quaternion.identity);
                    instance.name = terrainTile.name + "_Platform";
                    Undo.RegisterCreatedObjectUndo(instance, "Create Platform Tile");
                }
            }

            Undo.CollapseUndoOperations(group);
            SceneView.RepaintAll();
        }

        /// <summary>
        /// 生成环形地形
        /// </summary>
        public static void GenerateRingTerrain(Vector3 center, float innerRadius, float outerRadius, GameObject terrainTile)
        {
            if (terrainTile == null) return;

            Undo.SetCurrentGroupName("Generate Ring Terrain");
            int group = Undo.GetCurrentGroup();

            int steps = Mathf.RoundToInt(outerRadius * 2 / TileMapEditorConstants.GRID_SIZE);
            Vector3 startPos = center - Vector3.one * outerRadius;

            for (int x = 0; x <= steps; x++)
            {
                for (int z = 0; z <= steps; z++)
                {
                    Vector3 position = startPos + new Vector3(x * TileMapEditorConstants.GRID_SIZE, 0, z * TileMapEditorConstants.GRID_SIZE);
                    float distance = Vector3.Distance(new Vector3(position.x, 0, position.z), new Vector3(center.x, 0, center.z));

                    if (distance >= innerRadius && distance <= outerRadius)
                    {
                        GameObject instance = Object.Instantiate(terrainTile, position, Quaternion.identity);
                        instance.name = terrainTile.name + "_Ring";
                        Undo.RegisterCreatedObjectUndo(instance, "Create Ring Tile");
                    }
                }
            }

            Undo.CollapseUndoOperations(group);
            SceneView.RepaintAll();
        }

        /// <summary>
        /// 自动生成连接路径
        /// </summary>
        public static void GeneratePathBetween(Vector3 start, Vector3 end, GameObject pathTile)
        {
            if (pathTile == null) return;

            Vector3 direction = (end - start).normalized;
            float distance = Vector3.Distance(start, end);
            int steps = Mathf.RoundToInt(distance / TileMapEditorConstants.GRID_SIZE);

            Undo.SetCurrentGroupName("Generate Path");
            int group = Undo.GetCurrentGroup();

            for (int i = 0; i <= steps; i++)
            {
                Vector3 position = start + direction * (i * TileMapEditorConstants.GRID_SIZE);
                position = SnapToGrid(position);

                // 计算旋转使tile朝向路径方向
                Quaternion rotation = Quaternion.LookRotation(direction, Vector3.up);

                GameObject instance = Object.Instantiate(pathTile, position, rotation);
                instance.name = pathTile.name + "_Path";
                Undo.RegisterCreatedObjectUndo(instance, "Create Path Tile");
            }

            Undo.CollapseUndoOperations(group);
        }
        #endregion

        #region 批量操作工具
        /// <summary>
        /// 批量替换选中的对象
        /// </summary>
        public static void BatchReplace(GameObject newPrefab)
        {
            if (newPrefab == null || Selection.gameObjects.Length == 0) return;

            Undo.SetCurrentGroupName("Batch Replace");
            int group = Undo.GetCurrentGroup();

            foreach (GameObject selected in Selection.gameObjects)
            {
                Vector3 position = selected.transform.position;
                Quaternion rotation = selected.transform.rotation;

                Undo.DestroyObjectImmediate(selected);

                GameObject instance = Object.Instantiate(newPrefab, position, rotation);
                instance.name = newPrefab.name;
                Undo.RegisterCreatedObjectUndo(instance, "Replace Object");
            }

            Undo.CollapseUndoOperations(group);
            Selection.objects = new Object[0];
        }

        /// <summary>
        /// 批量随机旋转选中对象
        /// </summary>
        public static void BatchRandomRotate(float[] allowedAngles = null)
        {
            if (Selection.gameObjects.Length == 0) return;

            if (allowedAngles == null)
                allowedAngles = new float[] { 0f, 90f, 180f, 270f };

            Undo.SetCurrentGroupName("Batch Random Rotate");

            foreach (GameObject selected in Selection.gameObjects)
            {
                float randomAngle = allowedAngles[Random.Range(0, allowedAngles.Length)];
                Quaternion newRotation = Quaternion.Euler(0, randomAngle, 0);

                Undo.RecordObject(selected.transform, "Random Rotate");
                selected.transform.rotation = newRotation;
            }
        }

        /// <summary>
        /// 智能排列选中对象
        /// </summary>
        public static void SmartArrange(ArrangeMode mode)
        {
            if (Selection.gameObjects.Length < 2) return;

            Undo.SetCurrentGroupName("Smart Arrange");

            var objects = Selection.gameObjects.OrderBy(obj => obj.transform.position.x)
                                               .ThenBy(obj => obj.transform.position.z)
                                               .ToArray();

            switch (mode)
            {
                case ArrangeMode.Line:
                    ArrangeInLine(objects);
                    break;
                case ArrangeMode.Grid:
                    ArrangeInGrid(objects);
                    break;
                case ArrangeMode.Circle:
                    ArrangeInCircle(objects);
                    break;
            }
        }

        private static void ArrangeInLine(GameObject[] objects)
        {
            if (objects.Length < 2) return;

            Vector3 start = objects[0].transform.position;
            Vector3 end = objects[objects.Length - 1].transform.position;
            Vector3 direction = (end - start) / (objects.Length - 1);

            for (int i = 0; i < objects.Length; i++)
            {
                Vector3 newPosition = start + direction * i;
                Undo.RecordObject(objects[i].transform, "Arrange in Line");
                objects[i].transform.position = SnapToGrid(newPosition);
            }
        }

        private static void ArrangeInGrid(GameObject[] objects)
        {
            int gridSize = Mathf.CeilToInt(Mathf.Sqrt(objects.Length));
            Vector3 startPos = objects[0].transform.position;

            for (int i = 0; i < objects.Length; i++)
            {
                int x = i % gridSize;
                int z = i / gridSize;
                Vector3 newPosition = startPos + new Vector3(x * TileMapEditorConstants.GRID_SIZE, 0, z * TileMapEditorConstants.GRID_SIZE);

                Undo.RecordObject(objects[i].transform, "Arrange in Grid");
                objects[i].transform.position = newPosition;
            }
        }

        private static void ArrangeInCircle(GameObject[] objects)
        {
            Vector3 center = GetCenterPosition(objects);
            float radius = TileMapEditorConstants.GRID_SIZE * 3;

            for (int i = 0; i < objects.Length; i++)
            {
                float angle = (i / (float)objects.Length) * 360f * Mathf.Deg2Rad;
                Vector3 newPosition = center + new Vector3(
                    Mathf.Cos(angle) * radius,
                    0,
                    Mathf.Sin(angle) * radius
                );

                Undo.RecordObject(objects[i].transform, "Arrange in Circle");
                objects[i].transform.position = SnapToGrid(newPosition);
            }
        }
        #endregion

        #region 优化工具
        /// <summary>
        /// 合并静态网格以优化性能
        /// </summary>
        public static void CombineStaticMeshes(string combinedObjectName = "CombinedMesh")
        {
            GameObject[] selectedObjects = Selection.gameObjects;
            if (selectedObjects.Length < 2)
            {
                Debug.LogWarning("请选择至少2个对象进行合并");
                return;
            }

            // 按材质分组
            Dictionary<Material, List<CombineInstance>> materialGroups = new Dictionary<Material, List<CombineInstance>>();

            foreach (GameObject obj in selectedObjects)
            {
                MeshRenderer renderer = obj.GetComponent<MeshRenderer>();
                MeshFilter filter = obj.GetComponent<MeshFilter>();

                if (renderer && filter && filter.sharedMesh)
                {
                    foreach (Material material in renderer.sharedMaterials)
                    {
                        if (!materialGroups.ContainsKey(material))
                            materialGroups[material] = new List<CombineInstance>();

                        CombineInstance combine = new CombineInstance
                        {
                            mesh = filter.sharedMesh,
                            transform = obj.transform.localToWorldMatrix
                        };
                        materialGroups[material].Add(combine);
                    }
                }
            }

            // 创建合并对象
            GameObject combinedObject = new GameObject(combinedObjectName);
            Undo.RegisterCreatedObjectUndo(combinedObject, "Combine Meshes");

            int submeshIndex = 0;
            foreach (var group in materialGroups)
            {
                if (group.Value.Count > 0)
                {
                    Mesh combinedMesh = new Mesh();
                    combinedMesh.CombineMeshes(group.Value.ToArray());

                    GameObject submeshObject = new GameObject($"Submesh_{submeshIndex}");
                    submeshObject.transform.SetParent(combinedObject.transform);

                    MeshFilter meshFilter = submeshObject.AddComponent<MeshFilter>();
                    MeshRenderer meshRenderer = submeshObject.AddComponent<MeshRenderer>();

                    meshFilter.mesh = combinedMesh;
                    meshRenderer.material = group.Key;

                    submeshIndex++;
                }
            }

            // 删除原始对象
            foreach (GameObject obj in selectedObjects)
            {
                Undo.DestroyObjectImmediate(obj);
            }

            Selection.activeGameObject = combinedObject;
        }

        /// <summary>
        /// 自动LOD生成
        /// </summary>
        public static void GenerateAutoLOD(float[] lodDistances = null)
        {
            if (lodDistances == null)
                lodDistances = new float[] { 0.6f, 0.3f, 0.1f };

            foreach (GameObject selected in Selection.gameObjects)
            {
                LODGroup lodGroup = selected.GetComponent<LODGroup>();
                if (lodGroup == null)
                    lodGroup = selected.AddComponent<LODGroup>();

                MeshRenderer renderer = selected.GetComponent<MeshRenderer>();
                if (renderer == null) continue;

                LOD[] lods = new LOD[lodDistances.Length + 1];

                // LOD 0 (原始)
                lods[0] = new LOD(1.0f, new Renderer[] { renderer });

                // 生成简化LOD
                for (int i = 0; i < lodDistances.Length; i++)
                {
                    // 这里可以集成Simplygon或其他LOD生成工具
                    // 暂时使用相同的渲染器
                    lods[i + 1] = new LOD(lodDistances[i], new Renderer[] { renderer });
                }

                Undo.RecordObject(lodGroup, "Generate LOD");
                lodGroup.SetLODs(lods);
                lodGroup.RecalculateBounds();
            }
        }
        #endregion

        #region 工具方法
        private static Vector3 SnapToGrid(Vector3 position)
        {
            float gridSize = TileMapEditorConstants.GRID_SIZE;
            return new Vector3(
                Mathf.Round(position.x / gridSize) * gridSize,
                position.y,
                Mathf.Round(position.z / gridSize) * gridSize
            );
        }

        private static Vector3 GetCenterPosition(GameObject[] objects)
        {
            Vector3 center = Vector3.zero;
            foreach (GameObject obj in objects)
            {
                center += obj.transform.position;
            }
            return center / objects.Length;
        }

        /// <summary>
        /// 检查两个位置是否在网格容差内
        /// </summary>
        public static bool IsPositionOccupied(Vector3 position, List<TileData> tiles, float tolerance = 0.1f)
        {
            return tiles.Any(tile => Vector3.Distance(tile.position, position) < tolerance);
        }

        /// <summary>
        /// 获取最近的网格位置
        /// </summary>
        public static Vector3 GetNearestGridPosition(Vector3 worldPosition)
        {
            return SnapToGrid(worldPosition);
        }

        /// <summary>
        /// 验证地图完整性
        /// </summary>
        public static MapValidationResult ValidateMap(List<TileData> tiles)
        {
            MapValidationResult result = new MapValidationResult();

            // 检查重叠
            for (int i = 0; i < tiles.Count; i++)
            {
                for (int j = i + 1; j < tiles.Count; j++)
                {
                    if (Vector3.Distance(tiles[i].position, tiles[j].position) < 0.1f)
                    {
                        result.overlappingTiles.Add((i, j));
                    }
                }
            }

            // 检查孤立tile
            foreach (var tile in tiles)
            {
                bool hasNeighbor = tiles.Any(other => 
                    other != tile && 
                    Vector3.Distance(tile.position, other.position) <= TileMapEditorConstants.GRID_SIZE * 1.5f);
                
                if (!hasNeighbor)
                {
                    result.isolatedTiles.Add(tile);
                }
            }

            result.isValid = result.overlappingTiles.Count == 0;
            return result;
        }
        #endregion

        #region 数据结构
        public enum ArrangeMode
        {
            Line,
            Grid,
            Circle
        }

        public class MapValidationResult
        {
            public bool isValid = true;
            public List<(int, int)> overlappingTiles = new List<(int, int)>();
            public List<TileData> isolatedTiles = new List<TileData>();
            public List<string> warnings = new List<string>();
        }
        #endregion
    }    /// <summary>
    /// TileMapEditor 的静态常量访问器
    /// </summary>
    public static class TileMapEditorConstants
    {
        public const float GRID_SIZE = 2f;
    }
}
