using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace DWHITE.Editor
{
    /// <summary>
    /// Tile Map Editor 快捷工具窗口
    /// 提供常用的快速操作按钮和预设模板
    /// </summary>
    public class TileMapQuickTools : EditorWindow
    {
        #region 窗口管理
        [MenuItem("Tools/Tile Map Quick Tools")]
        public static void ShowWindow()
        {
            var window = GetWindow<TileMapQuickTools>("Quick Tools");
            window.minSize = new Vector2(300, 500);
        }
        #endregion

        #region 变量
        private GameObject terrainPrefab;
        private GameObject pathPrefab;
        private GameObject structurePrefab;
        
        private Vector3 platformCenter = Vector3.zero;
        private int platformWidth = 10;
        private int platformDepth = 10;
        
        private Vector3 pathStart = Vector3.zero;
        private Vector3 pathEnd = new Vector3(10, 0, 0);
        
        private float ringInnerRadius = 5f;
        private float ringOuterRadius = 10f;
        private Vector3 ringCenter = Vector3.zero;
        
        private Vector2 scrollPosition;
        #endregion

        #region GUI
        private void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            
            GUILayout.Label("🚀 Tile Map Quick Tools", EditorStyles.boldLabel);
            GUILayout.Space(10);
            
            DrawPrefabSelection();
            GUILayout.Space(10);
            
            DrawTerrainGeneration();
            GUILayout.Space(10);
            
            DrawBatchOperations();
            GUILayout.Space(10);
            
            DrawOptimizationTools();
            GUILayout.Space(10);
            
            DrawMapValidation();
            
            EditorGUILayout.EndScrollView();
        }

        private void DrawPrefabSelection()
        {
            EditorGUILayout.BeginVertical("box");
            GUILayout.Label("🎯 Prefab 选择", EditorStyles.boldLabel);
            
            terrainPrefab = EditorGUILayout.ObjectField("地形 Prefab:", terrainPrefab, typeof(GameObject), false) as GameObject;
            pathPrefab = EditorGUILayout.ObjectField("路径 Prefab:", pathPrefab, typeof(GameObject), false) as GameObject;
            structurePrefab = EditorGUILayout.ObjectField("结构 Prefab:", structurePrefab, typeof(GameObject), false) as GameObject;
            
            GUILayout.Space(5);
            
            if (GUILayout.Button("🔍 自动查找常用 Prefabs"))
            {
                AutoFindCommonPrefabs();
            }
            
            EditorGUILayout.EndVertical();
        }

        private void DrawTerrainGeneration()
        {
            EditorGUILayout.BeginVertical("box");
            GUILayout.Label("🌍 地形生成", EditorStyles.boldLabel);
            
            // 平台生成
            GUILayout.Label("基础平台:", EditorStyles.miniBoldLabel);
            platformCenter = EditorGUILayout.Vector3Field("中心点:", platformCenter);
            
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("尺寸:", GUILayout.Width(40));
            platformWidth = EditorGUILayout.IntField(platformWidth, GUILayout.Width(50));
            GUILayout.Label("x", GUILayout.Width(15));
            platformDepth = EditorGUILayout.IntField(platformDepth, GUILayout.Width(50));
            EditorGUILayout.EndHorizontal();
            
            EditorGUI.BeginDisabledGroup(terrainPrefab == null);
            if (GUILayout.Button("🏗️ 生成平台"))
            {
                TileMapEditorExtensions.GenerateBasePlatform(platformCenter, platformWidth, platformDepth, terrainPrefab);
            }
            EditorGUI.EndDisabledGroup();
            
            GUILayout.Space(10);
            
            // 环形地形
            GUILayout.Label("环形地形:", EditorStyles.miniBoldLabel);
            ringCenter = EditorGUILayout.Vector3Field("中心点:", ringCenter);
            ringInnerRadius = EditorGUILayout.FloatField("内半径:", ringInnerRadius);
            ringOuterRadius = EditorGUILayout.FloatField("外半径:", ringOuterRadius);
            
            EditorGUI.BeginDisabledGroup(terrainPrefab == null);
            if (GUILayout.Button("🔺 生成环形地形"))
            {
                TileMapEditorExtensions.GenerateRingTerrain(ringCenter, ringInnerRadius, ringOuterRadius, terrainPrefab);
            }
            EditorGUI.EndDisabledGroup();
            
            GUILayout.Space(10);
            
            // 路径生成
            GUILayout.Label("连接路径:", EditorStyles.miniBoldLabel);
            pathStart = EditorGUILayout.Vector3Field("起点:", pathStart);
            pathEnd = EditorGUILayout.Vector3Field("终点:", pathEnd);
            
            EditorGUI.BeginDisabledGroup(pathPrefab == null);
            if (GUILayout.Button("🛤️ 生成路径"))
            {
                TileMapEditorExtensions.GeneratePathBetween(pathStart, pathEnd, pathPrefab);
            }
            EditorGUI.EndDisabledGroup();
            
            EditorGUILayout.EndVertical();
        }

        private void DrawBatchOperations()
        {
            EditorGUILayout.BeginVertical("box");
            GUILayout.Label("⚡ 批量操作", EditorStyles.boldLabel);
            
            GUILayout.Label($"当前选中: {Selection.gameObjects.Length} 个对象", EditorStyles.helpBox);
            
            EditorGUI.BeginDisabledGroup(Selection.gameObjects.Length == 0);
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("🔄 随机旋转"))
            {
                TileMapEditorExtensions.BatchRandomRotate();
            }
            if (GUILayout.Button("📏 对齐网格"))
            {
                AlignSelectionToGrid();
            }
            EditorGUILayout.EndHorizontal();
            
            GUILayout.Label("智能排列:", EditorStyles.miniBoldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("📏 直线"))
            {
                TileMapEditorExtensions.SmartArrange(TileMapEditorExtensions.ArrangeMode.Line);
            }
            if (GUILayout.Button("🔲 网格"))
            {
                TileMapEditorExtensions.SmartArrange(TileMapEditorExtensions.ArrangeMode.Grid);
            }
            if (GUILayout.Button("⭕ 圆形"))
            {
                TileMapEditorExtensions.SmartArrange(TileMapEditorExtensions.ArrangeMode.Circle);
            }
            EditorGUILayout.EndHorizontal();
            
            GUILayout.Space(5);
            
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginDisabledGroup(structurePrefab == null);
            if (GUILayout.Button("🔄 批量替换"))
            {
                TileMapEditorExtensions.BatchReplace(structurePrefab);
            }
            EditorGUI.EndDisabledGroup();
            
            if (GUILayout.Button("🗑️ 删除选中"))
            {
                DeleteSelected();
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUI.EndDisabledGroup();
            
            EditorGUILayout.EndVertical();
        }

        private void DrawOptimizationTools()
        {
            EditorGUILayout.BeginVertical("box");
            GUILayout.Label("⚙️ 优化工具", EditorStyles.boldLabel);
            
            EditorGUI.BeginDisabledGroup(Selection.gameObjects.Length < 2);
            if (GUILayout.Button("🔗 合并静态网格"))
            {
                TileMapEditorExtensions.CombineStaticMeshes("CombinedTerrain");
            }
            EditorGUI.EndDisabledGroup();
            
            EditorGUI.BeginDisabledGroup(Selection.gameObjects.Length == 0);
            if (GUILayout.Button("📊 生成 LOD"))
            {
                TileMapEditorExtensions.GenerateAutoLOD();
            }
            EditorGUI.EndDisabledGroup();
            
            if (GUILayout.Button("🧹 清理空对象"))
            {
                CleanupEmptyObjects();
            }
            
            if (GUILayout.Button("🔍 查找重复对象"))
            {
                FindDuplicateObjects();
            }
            
            EditorGUILayout.EndVertical();
        }

        private void DrawMapValidation()
        {
            EditorGUILayout.BeginVertical("box");
            GUILayout.Label("✅ 地图验证", EditorStyles.boldLabel);
            
            if (GUILayout.Button("🔍 检查地图完整性"))
            {
                ValidateCurrentMap();
            }
            
            if (GUILayout.Button("📊 生成地图统计"))
            {
                GenerateMapStatistics();
            }
            
            if (GUILayout.Button("📸 生成地图预览"))
            {
                GenerateMapPreview();
            }
            
            EditorGUILayout.EndVertical();
        }
        #endregion

        #region 功能实现
        private void AutoFindCommonPrefabs()
        {
            string[] guids = AssetDatabase.FindAssets("t:GameObject", new[] { "Assets/Art/KayKit/KayKit_SpaceBase" });
            
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                
                if (prefab != null)
                {
                    string name = prefab.name.ToLower();
                    
                    if (terrainPrefab == null && name.Contains("terrain") && !name.Contains("mining"))
                    {
                        terrainPrefab = prefab;
                    }
                    else if (pathPrefab == null && (name.Contains("tunnel_straight") || name.Contains("landingpad")))
                    {
                        pathPrefab = prefab;
                    }
                    else if (structurePrefab == null && name.Contains("basemodule"))
                    {
                        structurePrefab = prefab;
                    }
                }
            }
            
            Debug.Log($"找到 Prefabs: 地形={terrainPrefab?.name}, 路径={pathPrefab?.name}, 结构={structurePrefab?.name}");
        }

        private void AlignSelectionToGrid()
        {
            Undo.SetCurrentGroupName("Align to Grid");
            
            foreach (GameObject obj in Selection.gameObjects)
            {
                Vector3 gridPos = TileMapEditorExtensions.GetNearestGridPosition(obj.transform.position);
                Undo.RecordObject(obj.transform, "Align to Grid");
                obj.transform.position = gridPos;
            }
        }

        private void DeleteSelected()
        {
            if (EditorUtility.DisplayDialog("确认删除", 
                $"确定要删除选中的 {Selection.gameObjects.Length} 个对象吗？", 
                "删除", "取消"))
            {
                Undo.SetCurrentGroupName("Delete Selected");
                
                foreach (GameObject obj in Selection.gameObjects)
                {
                    Undo.DestroyObjectImmediate(obj);
                }
                
                Selection.objects = new Object[0];
            }
        }

        private void CleanupEmptyObjects()
        {
            GameObject[] allObjects = FindObjectsOfType<GameObject>();
            List<GameObject> emptyObjects = new List<GameObject>();
            
            foreach (GameObject obj in allObjects)
            {
                if (obj.transform.childCount == 0 && 
                    obj.GetComponents<Component>().Length <= 1) // 只有Transform组件
                {
                    emptyObjects.Add(obj);
                }
            }
            
            if (emptyObjects.Count > 0)
            {
                if (EditorUtility.DisplayDialog("清理空对象", 
                    $"找到 {emptyObjects.Count} 个空对象，是否删除？", 
                    "删除", "取消"))
                {
                    Undo.SetCurrentGroupName("Cleanup Empty Objects");
                    
                    foreach (GameObject obj in emptyObjects)
                    {
                        Undo.DestroyObjectImmediate(obj);
                    }
                }
            }
            else
            {
                EditorUtility.DisplayDialog("清理完成", "没有找到空对象", "确定");
            }
        }

        private void FindDuplicateObjects()
        {
            GameObject[] allObjects = FindObjectsOfType<GameObject>();
            Dictionary<Vector3, List<GameObject>> positionGroups = new Dictionary<Vector3, List<GameObject>>();
            
            foreach (GameObject obj in allObjects)
            {
                Vector3 gridPos = TileMapEditorExtensions.GetNearestGridPosition(obj.transform.position);
                
                if (!positionGroups.ContainsKey(gridPos))
                    positionGroups[gridPos] = new List<GameObject>();
                
                positionGroups[gridPos].Add(obj);
            }
            
            List<GameObject> duplicates = new List<GameObject>();
            foreach (var group in positionGroups)
            {
                if (group.Value.Count > 1)
                {
                    duplicates.AddRange(group.Value);
                }
            }
            
            if (duplicates.Count > 0)
            {
                Selection.objects = duplicates.ToArray();
                EditorUtility.DisplayDialog("找到重复对象", 
                    $"找到 {duplicates.Count} 个可能重复的对象，已自动选中", "确定");
            }
            else
            {
                EditorUtility.DisplayDialog("检查完成", "没有找到重复对象", "确定");
            }
        }

        private void ValidateCurrentMap()
        {
            // 这里需要与TileMapEditor的数据交互
            // 暂时提供一个简化版本
            
            GameObject[] allTiles = FindObjectsOfType<GameObject>();
            List<TileData> tileData = new List<TileData>();
            
            foreach (GameObject tile in allTiles)
            {
                if (tile.name.Contains("terrain") || tile.name.Contains("basemodule"))
                {
                    tileData.Add(new TileData
                    {
                        prefabName = tile.name,
                        position = tile.transform.position,
                        rotation = tile.transform.rotation
                    });
                }
            }
            
            var result = TileMapEditorExtensions.ValidateMap(tileData);
            
            string message = $"地图验证结果:\n";
            message += $"总 Tiles: {tileData.Count}\n";
            message += $"重叠对象: {result.overlappingTiles.Count}\n";
            message += $"孤立对象: {result.isolatedTiles.Count}\n";
            message += $"状态: {(result.isValid ? "✅ 通过" : "❌ 有问题")}";
            
            EditorUtility.DisplayDialog("地图验证", message, "确定");
        }

        private void GenerateMapStatistics()
        {
            GameObject[] allObjects = FindObjectsOfType<GameObject>();
            Dictionary<string, int> prefabCounts = new Dictionary<string, int>();
            
            foreach (GameObject obj in allObjects)
            {
                string prefabName = obj.name.Split('(')[0].Trim(); // 移除(Clone)后缀
                
                if (!prefabCounts.ContainsKey(prefabName))
                    prefabCounts[prefabName] = 0;
                
                prefabCounts[prefabName]++;
            }
            
            string stats = "地图统计信息:\n\n";
            foreach (var kvp in prefabCounts)
            {
                stats += $"{kvp.Key}: {kvp.Value} 个\n";
            }
            
            Debug.Log(stats);
            EditorUtility.DisplayDialog("地图统计", "统计信息已输出到控制台", "确定");
        }

        private void GenerateMapPreview()
        {
            // 创建地图预览的鸟瞰图
            Camera previewCamera = Camera.main;
            if (previewCamera == null)
            {
                GameObject cameraObj = new GameObject("Preview Camera");
                previewCamera = cameraObj.AddComponent<Camera>();
            }
            
            // 设置相机为鸟瞰视角
            Bounds sceneBounds = GetSceneBounds();
            Vector3 center = sceneBounds.center;
            
            previewCamera.transform.position = center + Vector3.up * (sceneBounds.size.magnitude);
            previewCamera.transform.rotation = Quaternion.LookRotation(Vector3.down);
            previewCamera.orthographic = true;
            previewCamera.orthographicSize = Mathf.Max(sceneBounds.size.x, sceneBounds.size.z) / 2f;
            
            EditorUtility.DisplayDialog("预览相机", "预览相机已设置为鸟瞰视角", "确定");
        }

        private Bounds GetSceneBounds()
        {
            Renderer[] renderers = FindObjectsOfType<Renderer>();
            if (renderers.Length == 0) return new Bounds(Vector3.zero, Vector3.one);
            
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers)
            {
                bounds.Encapsulate(renderer.bounds);
            }
            
            return bounds;
        }
        #endregion
    }
}
