using UnityEngine;
using UnityEditor;

namespace DWHITE.Editor
{
    /// <summary>
    /// Tile Map Editor 安装向导和测试工具
    /// 帮助用户快速设置和验证编辑器功能
    /// </summary>
    public class TileMapSetupWizard : EditorWindow
    {
        private Vector2 scrollPosition;
        private bool hasKayKitAssets = false;
        private bool hasMapDataFolder = false;
        private int foundPrefabCount = 0;
        
        [MenuItem("Tools/Tile Map Setup Wizard")]
        public static void ShowWindow()
        {
            var window = GetWindow<TileMapSetupWizard>("Setup Wizard");
            window.minSize = new Vector2(400, 300);
            window.CheckSetup();
        }
        
        private void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            
            GUILayout.Label("🛠️ Tile Map Editor 安装向导", EditorStyles.boldLabel);
            GUILayout.Space(10);
            
            DrawSetupStatus();
            GUILayout.Space(10);
            
            DrawQuickActions();
            GUILayout.Space(10);
            
            DrawTestTools();
            
            EditorGUILayout.EndScrollView();
        }
        
        private void DrawSetupStatus()
        {
            EditorGUILayout.BeginVertical("box");
            GUILayout.Label("📋 设置状态检查", EditorStyles.boldLabel);
            
            // KayKit 资源检查
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(hasKayKitAssets ? "✅" : "❌", GUILayout.Width(20));
            GUILayout.Label($"KayKit 模型资源 ({foundPrefabCount} 个模型)");
            EditorGUILayout.EndHorizontal();
            
            if (!hasKayKitAssets)
            {
                EditorGUILayout.HelpBox("请将KayKit太空基地模型包导入到 Assets/Art/KayKit/KayKit_SpaceBase/ 文件夹", MessageType.Warning);
            }
            
            // 地图数据文件夹检查
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(hasMapDataFolder ? "✅" : "❌", GUILayout.Width(20));
            GUILayout.Label("MapData 文件夹");
            EditorGUILayout.EndHorizontal();
            
            if (!hasMapDataFolder)
            {
                EditorGUILayout.HelpBox("MapData 文件夹将在首次保存地图时自动创建", MessageType.Info);
            }
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawQuickActions()
        {
            EditorGUILayout.BeginVertical("box");
            GUILayout.Label("🚀 快速操作", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("🔍 重新检查设置"))
            {
                CheckSetup();
            }
            
            if (GUILayout.Button("📁 创建 MapData 文件夹"))
            {
                CreateMapDataFolder();
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("🎯 打开主编辑器"))
            {
                TileMapEditor.ShowWindow();
            }
            
            if (GUILayout.Button("⚡ 打开快捷工具"))
            {
                TileMapQuickTools.ShowWindow();
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawTestTools()
        {
            EditorGUILayout.BeginVertical("box");
            GUILayout.Label("🧪 测试工具", EditorStyles.boldLabel);
            
            if (GUILayout.Button("创建测试场景"))
            {
                CreateTestScene();
            }
            
            if (GUILayout.Button("生成示例地图"))
            {
                GenerateExampleMap();
            }
            
            if (GUILayout.Button("清理测试数据"))
            {
                CleanupTestData();
            }
            
            EditorGUILayout.EndVertical();
        }
        
        private void CheckSetup()
        {
            // 检查 KayKit 资源
            string[] guids = AssetDatabase.FindAssets("t:GameObject", new[] { "Assets/Art/KayKit/KayKit_SpaceBase" });
            hasKayKitAssets = guids.Length > 0;
            foundPrefabCount = guids.Length;
            
            // 检查 MapData 文件夹
            hasMapDataFolder = AssetDatabase.IsValidFolder("Assets/MapData");
            
            Repaint();
        }
        
        private void CreateMapDataFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/MapData"))
            {
                AssetDatabase.CreateFolder("Assets", "MapData");
                AssetDatabase.Refresh();
                CheckSetup();
                EditorUtility.DisplayDialog("成功", "MapData 文件夹已创建！", "确定");
            }
            else
            {
                EditorUtility.DisplayDialog("提示", "MapData 文件夹已存在", "确定");
            }
        }
        
        private void CreateTestScene()
        {
            // 创建新场景
            var newScene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(
                UnityEditor.SceneManagement.NewSceneSetup.DefaultGameObjects,
                UnityEditor.SceneManagement.NewSceneMode.Single);
            
            // 设置场景名称
            newScene.name = "TileMapTest";
            
            // 添加基础光照
            GameObject sun = GameObject.Find("Directional Light");
            if (sun != null)
            {
                sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            }
            
            // 设置相机位置以便观察
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                mainCamera.transform.position = new Vector3(0, 10, -10);
                mainCamera.transform.rotation = Quaternion.Euler(30, 0, 0);
            }
            
            EditorUtility.DisplayDialog("成功", "测试场景已创建！\n现在可以打开Tile Map Editor开始构建地图。", "确定");
        }
        
        private void GenerateExampleMap()
        {
            if (!hasKayKitAssets)
            {
                EditorUtility.DisplayDialog("错误", "请先导入KayKit模型包", "确定");
                return;
            }
            
            // 查找地形模型
            string[] guids = AssetDatabase.FindAssets("terrain", new[] { "Assets/Art/KayKit/KayKit_SpaceBase" });
            if (guids.Length == 0)
            {
                EditorUtility.DisplayDialog("错误", "未找到地形模型", "确定");
                return;
            }
            
            string terrainPath = AssetDatabase.GUIDToAssetPath(guids[0]);
            GameObject terrainPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(terrainPath);
            
            if (terrainPrefab != null)
            {
                // 创建简单的5x5平台
                for (int x = -2; x <= 2; x++)
                {
                    for (int z = -2; z <= 2; z++)
                    {
                        Vector3 position = new Vector3(x * 2f, 0, z * 2f);
                        GameObject instance = Instantiate(terrainPrefab, position, Quaternion.identity);
                        instance.name = "ExampleTerrain";
                    }
                }
                
                EditorUtility.DisplayDialog("成功", "示例地图已生成！", "确定");
            }
        }
        
        private void CleanupTestData()
        {
            if (EditorUtility.DisplayDialog("确认清理", "这将删除所有名称包含 'Example' 的对象，确定继续吗？", "确定", "取消"))
            {
                GameObject[] allObjects = FindObjectsOfType<GameObject>();
                int cleanedCount = 0;
                
                foreach (GameObject obj in allObjects)
                {
                    if (obj.name.Contains("Example"))
                    {
                        DestroyImmediate(obj);
                        cleanedCount++;
                    }
                }
                
                EditorUtility.DisplayDialog("清理完成", $"已清理 {cleanedCount} 个测试对象", "确定");
            }
        }
    }
}
