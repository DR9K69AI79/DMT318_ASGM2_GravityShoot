using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DWHITE.Editor
{
    /// <summary>
    /// 简单高效的3D Tile地图编辑器
    /// 支持智能全向堆叠、实时交互模式、网格对齐放置、分类管理、快速预览等功能
    /// </summary>
    public class TileMapEditor : EditorWindow
    {
        #region 配置常量
        private const string TILE_FOLDER_PATH = "Assets/Art/Prefab";
        private const string MAP_DATA_FOLDER = "Assets/MapData";
        private const int TOOLBAR_HEIGHT = 60;
        private const int SIDEBAR_WIDTH = 250;
        #endregion

        #region 配置变量
        private float gridSize = 2f; // 网格大小，根据你的模型调整
        private float currentHeight = 0f; // 当前放置高度
        private float heightStep = 2f; // 高度调整步长
        #endregion

        #region 编辑器状态
        // 简化后的编辑状态 - 通过按键实时切换
        private GameObject selectedPrefab;
        private GameObject previewObject;
        private Vector3 currentGridPosition;
        private Vector3 targetSurfaceNormal = Vector3.up; // 当前表面法线
        private float rotationY = 0f;
        private bool snapToGrid = true;
        private bool showGrid = true;
        private LayerMask placementLayerMask = -1;
        
        // 实时交互状态
        private bool isShiftPressed = false; // Shift = 绘制模式
        private bool isCtrlPressed = false;  // Ctrl = 删除模式
        private bool isAltPressed = false;   // Alt = 场景拾取模式
        private bool isPainting = false;     // 是否正在连续绘制
        
        // 分类管理
        private Dictionary<string, List<GameObject>> categorizedPrefabs = new Dictionary<string, List<GameObject>>();
        private string[] categories;
        private int selectedCategory = 0;
        private Vector2 scrollPosition;
        private bool showPrefabNames = true;
        
        // 地图数据
        private List<TileData> currentMapTiles = new List<TileData>();
        private string currentMapName = "NewMap";
        #endregion

        #region 窗口管理
        [MenuItem("Tools/Tile Map Editor")]
        public static void ShowWindow()
        {
            var window = GetWindow<TileMapEditor>("Tile Map Editor");
            window.minSize = new Vector2(800, 600);
        }

        private void OnEnable()
        {
            if (!ValidateSetup()) return;
            
            LoadPrefabs();
            SceneView.duringSceneGui += OnSceneGUI;
            Undo.undoRedoPerformed += OnUndoRedo;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            Undo.undoRedoPerformed -= OnUndoRedo;
            DestroyPreviewObject();
        }

        private void OnUndoRedo()
        {
            RefreshMapData();
        }
        #endregion

        #region 资源加载
        private void LoadPrefabs()
        {
            categorizedPrefabs.Clear();
            
            // 加载所有FBX文件并按类型分类
            string[] guids = AssetDatabase.FindAssets("t:GameObject", new[] { TILE_FOLDER_PATH });
            
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                
                if (prefab != null)
                {
                    string category = DetermineCategory(prefab.name);
                    
                    if (!categorizedPrefabs.ContainsKey(category))
                        categorizedPrefabs[category] = new List<GameObject>();
                    
                    categorizedPrefabs[category].Add(prefab);
                }
            }
            
            // 对每个分类的prefab按名称排序
            foreach (var category in categorizedPrefabs.Keys.ToList())
            {
                categorizedPrefabs[category] = categorizedPrefabs[category]
                    .OrderBy(p => p.name)
                    .ToList();
            }
            
            categories = categorizedPrefabs.Keys.OrderBy(c => GetCategoryPriority(c)).ToArray();
        }

        private string DetermineCategory(string prefabName)
        {
            prefabName = prefabName.ToLower();
            
            if (prefabName.Contains("terrain"))
                return "🌍 地形";
            else if (prefabName.Contains("basemodule") || prefabName.Contains("roofmodule"))
                return "🏢 基础建筑";
            else if (prefabName.Contains("tunnel"))
                return "🚇 隧道";
            else if (prefabName.Contains("cargo") || prefabName.Contains("container"))
                return "📦 货物";
            else if (prefabName.Contains("lander") || prefabName.Contains("landingpad"))
                return "🚀 着陆设施";
            else if (prefabName.Contains("structure"))
                return "🏗️ 结构";
            else if (prefabName.Contains("rock"))
                return "🪨 岩石";
            else if (prefabName.Contains("light") || prefabName.Contains("solar") || prefabName.Contains("wind"))
                return "⚡ 设备";
            else if (prefabName.Contains("truck") || prefabName.Contains("drill"))
                return "🚛 载具";
            else
                return "📋 其他";
        }

        private int GetCategoryPriority(string category)
        {
            switch (category)
            {
                case "🌍 地形": return 0;
                case "🏢 基础建筑": return 1;
                case "🚇 隧道": return 2;
                case "🏗️ 结构": return 3;
                case "🚀 着陆设施": return 4;
                case "📦 货物": return 5;
                case "⚡ 设备": return 6;
                case "🪨 岩石": return 7;
                case "🚛 载具": return 8;
                default: return 9;
            }
        }
        #endregion

        #region GUI 绘制
        private void OnGUI()
        {
            EditorGUILayout.BeginHorizontal();
            
            // 左侧工具栏
            DrawSidebar();
            
            // 右侧主面板
            EditorGUILayout.BeginVertical();
            DrawToolbar();
            DrawMainPanel();
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.EndHorizontal();
            
            // 处理事件
            ProcessEvents();
        }

        private void DrawSidebar()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(SIDEBAR_WIDTH));
            
            GUILayout.Label("🎯 Tile Map Editor", EditorStyles.boldLabel);
            
            // 实时交互模式说明
            GUILayout.Space(10);
            GUILayout.Label("交互模式 (实时)", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginVertical("box");
            GUILayout.Label("• 普通模式: 点击放置");
            GUILayout.Label("• Shift + 拖拽: 连续绘制");
            GUILayout.Label("• Ctrl + 点击: 删除");
            GUILayout.Label("• Alt + 点击: 场景拾取");
            EditorGUILayout.EndVertical();
            
            // 当前状态显示
            EditorGUILayout.BeginVertical("box");
            string currentModeText = "🔹 普通放置";
            if (isShiftPressed) currentModeText = "🎨 绘制模式";
            else if (isCtrlPressed) currentModeText = "🗑️ 删除模式";
            else if (isAltPressed) currentModeText = "🎯 拾取模式";
            
            GUILayout.Label($"当前: {currentModeText}", EditorStyles.helpBox);
            if (isPainting) GUILayout.Label("✏️ 正在绘制...", EditorStyles.helpBox);
            EditorGUILayout.EndVertical();
            
            GUILayout.Space(10);
            
            // 设置选项
            GUILayout.Label("设置", EditorStyles.boldLabel);
            snapToGrid = EditorGUILayout.Toggle("网格对齐", snapToGrid);
            showGrid = EditorGUILayout.Toggle("显示网格", showGrid);
            showPrefabNames = EditorGUILayout.Toggle("显示名称", showPrefabNames);
            
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("网格大小");
            gridSize = EditorGUILayout.FloatField(gridSize);
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("当前高度");
            currentHeight = EditorGUILayout.FloatField(currentHeight);
            if (GUILayout.Button("复位", GUILayout.Width(50)))
                currentHeight = 0f;
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("高度步长");
            heightStep = EditorGUILayout.FloatField(heightStep);
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("旋转角度");
            rotationY = EditorGUILayout.FloatField(rotationY);
            if (GUILayout.Button("复位", GUILayout.Width(50)))
                rotationY = 0f;
            EditorGUILayout.EndHorizontal();
            
            GUILayout.Space(10);
            
            // 分类选择
            if (categories != null && categories.Length > 0)
            {
                GUILayout.Label("Prefab 分类", EditorStyles.boldLabel);
                selectedCategory = GUILayout.SelectionGrid(selectedCategory, categories, 1);
                
                GUILayout.Space(5);
                
                // Prefab 列表
                if (selectedCategory < categories.Length)
                {
                    string category = categories[selectedCategory];
                    if (categorizedPrefabs.ContainsKey(category))
                    {
                        DrawPrefabList(categorizedPrefabs[category]);
                    }
                }
            }
            
            EditorGUILayout.EndVertical();
        }

        private void DrawPrefabList(List<GameObject> prefabs)
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            
            foreach (GameObject prefab in prefabs)
            {
                EditorGUILayout.BeginHorizontal();
                
                // 预览图标
                Texture2D preview = AssetPreview.GetAssetPreview(prefab);
                if (preview != null)
                {
                    if (GUILayout.Button(preview, GUILayout.Width(40), GUILayout.Height(40)))
                    {
                        selectedPrefab = prefab;
                    }
                }
                else
                {
                    if (GUILayout.Button("📦", GUILayout.Width(40), GUILayout.Height(40)))
                    {
                        selectedPrefab = prefab;
                    }
                }
                
                // 名称
                EditorGUILayout.BeginVertical();
                if (showPrefabNames)
                {
                    string displayName = prefab.name.Replace("_", " ");
                    GUILayout.Label(displayName, EditorStyles.wordWrappedLabel);
                }
                
                // 高亮选中项
                if (selectedPrefab == prefab)
                {
                    EditorGUILayout.HelpBox("✓ 已选中", MessageType.Info);
                }
                EditorGUILayout.EndVertical();
                
                EditorGUILayout.EndHorizontal();
                GUILayout.Space(2);
            }
            
            EditorGUILayout.EndScrollView();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginVertical("box", GUILayout.Height(TOOLBAR_HEIGHT));
            
            EditorGUILayout.BeginHorizontal();
            
            // 地图操作
            GUILayout.Label("地图操作:", GUILayout.Width(60));
            
            currentMapName = EditorGUILayout.TextField(currentMapName, GUILayout.Width(120));
            
            if (GUILayout.Button("新建", GUILayout.Width(50)))
                NewMap();
            
            if (GUILayout.Button("保存", GUILayout.Width(50)))
                SaveMap();
            
            if (GUILayout.Button("加载", GUILayout.Width(50)))
                LoadMap();
            
            GUILayout.FlexibleSpace();
            
            // 操作按钮
            if (GUILayout.Button("清空地图", GUILayout.Width(80)))
                ClearMap();
            
            if (GUILayout.Button("刷新", GUILayout.Width(50)))
                RefreshMapData();
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label($"当前选中: {(selectedPrefab ? selectedPrefab.name : "无")}", EditorStyles.helpBox);
            GUILayout.Label($"地图 Tiles: {currentMapTiles.Count}", EditorStyles.helpBox);
            GUILayout.Label($"工作高度: {currentHeight:F1}", EditorStyles.helpBox);
            
            // 显示当前交互状态
            string statusText = "🔹 普通";
            if (isShiftPressed && isPainting) statusText = "🎨 绘制中";
            else if (isShiftPressed) statusText = "🎨 绘制模式";
            else if (isCtrlPressed) statusText = "🗑️ 删除模式";
            else if (isAltPressed) statusText = "🎯 拾取模式";
            
            GUILayout.Label(statusText, EditorStyles.helpBox);
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndVertical();
        }

        private void DrawMainPanel()
        {
            EditorGUILayout.BeginVertical("box");
            
            GUILayout.Label("使用说明:", EditorStyles.boldLabel);
            GUILayout.Label("🎯 实时交互模式 - 无需切换，按键即用:");
            GUILayout.Label("• 普通点击: 放置 Tiles");
            GUILayout.Label("• Shift + 拖拽: 连续绘制");
            GUILayout.Label("• Ctrl + 点击: 删除");
            GUILayout.Label("• Alt + 点击: 从场景拾取");
            
            GUILayout.Space(5);
            GUILayout.Label("🔧 快捷键:");
            GUILayout.Label("• R键: 旋转选中的Prefab");
            GUILayout.Label("• Q/E键: 调整放置高度");
            GUILayout.Label("• G键: 切换网格显示");
            GUILayout.Label("• F1键: 显示详细帮助");
            
            GUILayout.Space(5);
            GUILayout.Label("🌟 智能全向堆叠:");
            GUILayout.Label("• 自动检测碰撞表面");
            GUILayout.Label("• 支持在任意方向表面堆叠");
            GUILayout.Label("• 法线指示器显示堆叠方向");
            
            GUILayout.Space(10);
            
            // 统计信息
            if (currentMapTiles.Count > 0)
            {
                GUILayout.Label("地图统计:", EditorStyles.boldLabel);
                var stats = currentMapTiles.GroupBy(t => t.prefabName)
                    .OrderByDescending(g => g.Count())
                    .Take(5);
                
                foreach (var group in stats)
                {
                    GUILayout.Label($"• {group.Key}: {group.Count()} 个");
                }
            }
            
            EditorGUILayout.EndVertical();
        }
        #endregion

        #region Scene视图交互
        private void OnSceneGUI(SceneView sceneView)
        {
            HandleSceneInput();
            DrawSceneGizmos();
            
            if (showGrid)
                DrawGrid();
            
            UpdatePreview();
        }

        private void HandleSceneInput()
        {
            Event e = Event.current;

            // 检测修饰键状态
            isShiftPressed = e.shift;
            isCtrlPressed = e.control;
            isAltPressed = e.alt;

            // 获取鼠标在世界空间的位置 - 智能全向堆叠
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            Vector3 targetPosition;
            targetSurfaceNormal = Vector3.up; // 默认向上            // 智能碰撞检测 - 支持全方向堆叠
            RaycastHit[] hits = Physics.RaycastAll(ray, Mathf.Infinity);
            RaycastHit validHit = new RaycastHit();
            bool hasValidHit = false;
            
            // 过滤出有效的碰撞点（排除预览对象）
            foreach (var hit in hits)
            {
                if (hit.collider.gameObject.name != "[PREVIEW]")
                {
                    if (!hasValidHit || hit.distance < validHit.distance)
                    {
                        validHit = hit;
                        hasValidHit = true;
                    }
                }
            }
            
            if (hasValidHit)
            {
                targetPosition = validHit.point;
                targetSurfaceNormal = validHit.normal;

                // 智能堆叠：根据表面法线和对象bounds调整位置
                Bounds hitObjectBounds = GetObjectBounds(validHit.collider.gameObject);
                
                // 计算合适的偏移距离
                float offsetDistance = gridSize * 0.5f;
                if (selectedPrefab != null)
                {
                    // 尝试获取选中prefab的大小信息
                    Renderer[] renderers = selectedPrefab.GetComponentsInChildren<Renderer>();
                    if (renderers.Length > 0)
                    {
                        Bounds prefabBounds = renderers[0].bounds;
                        foreach (var renderer in renderers)
                        {
                            prefabBounds.Encapsulate(renderer.bounds);
                        }
                        // 使用prefab的实际大小来计算偏移
                        offsetDistance = Mathf.Max(prefabBounds.size.magnitude * 0.3f, gridSize * 0.3f);
                    }
                }
                
                targetPosition += targetSurfaceNormal * offsetDistance;
            }
            else
            {
                // 如果没有碰撞到任何东西，在当前高度平面上计算位置
                Plane workingPlane = new Plane(Vector3.up, new Vector3(0, currentHeight, 0));
                if (workingPlane.Raycast(ray, out float distance))
                {
                    targetPosition = ray.GetPoint(distance);
                }
                else
                {
                    targetPosition = ray.GetPoint(10f);
                    targetPosition.y = currentHeight;
                }
                targetSurfaceNormal = Vector3.up;
            }

            // 应用网格对齐
            currentGridPosition = snapToGrid ? SnapToGrid(targetPosition) : targetPosition;

            // 处理键盘输入
            if (e.type == EventType.KeyDown)
            {
                switch (e.keyCode)
                {
                    case KeyCode.R:
                        rotationY += 90f;
                        if (rotationY >= 360f) rotationY -= 360f;
                        e.Use();
                        break;

                    case KeyCode.G:
                        showGrid = !showGrid;
                        e.Use();
                        break;

                    case KeyCode.Q:
                        currentHeight += heightStep;
                        e.Use();
                        break;

                    case KeyCode.E:
                        currentHeight -= heightStep;
                        e.Use();
                        break;

                    case KeyCode.Escape:
                        selectedPrefab = null;
                        DestroyPreviewObject();
                        isPainting = false;
                        e.Use();
                        break;
                }
            }

            // 处理鼠标点击
            if (e.type == EventType.MouseDown && e.button == 0)
            {
                if (isCtrlPressed)
                {
                    // Ctrl + 点击 = 删除
                    DeleteTile();
                }
                else if (isAltPressed)
                {
                    // Alt + 点击 = 场景拾取
                    PickFromScene();
                }
                else
                {
                    // 普通点击 = 放置
                    PlaceTile();
                    
                    // 如果按住Shift，开始绘制模式
                    if (isShiftPressed)
                    {
                        isPainting = true;
                    }
                }
                e.Use();
            }

            // 处理Shift绘制模式的拖拽
            if (isShiftPressed && isPainting)
            {
                if (e.type == EventType.MouseDrag && e.button == 0)
                {
                    PlaceTile();
                    e.Use();
                }
                else if (e.type == EventType.MouseUp && e.button == 0)
                {
                    isPainting = false;
                    e.Use();
                }
            }

            // 如果松开Shift键，停止绘制
            if (!isShiftPressed && isPainting)
            {
                isPainting = false;
            }
        }

        private Vector3 SnapToGrid(Vector3 position)
        {
            float snappedX = Mathf.Round(position.x / gridSize) * gridSize;
            float snappedY = Mathf.Round(position.y / gridSize) * gridSize;
            float snappedZ = Mathf.Round(position.z / gridSize) * gridSize;
            return new Vector3(snappedX, snappedY, snappedZ);
        }        private void UpdatePreview()
        {
            // 在放置模式下显示预览，在删除模式下显示删除预览
            if (selectedPrefab != null && !isAltPressed)
            {
                if (isCtrlPressed)
                {
                    // 删除模式：显示要删除的对象预览
                    DestroyPreviewObject(); // 先销毁放置预览
                    ShowDeletePreview();
                }
                else
                {
                    // 放置模式：显示放置预览
                    ShowPlacePreview();
                }
            }
            else
            {
                DestroyPreviewObject();
            }
        }

        private void ShowPlacePreview()
        {
            if (previewObject == null)
            {
                previewObject = Instantiate(selectedPrefab);
                previewObject.name = "[PREVIEW]";
                
                // 移除所有碰撞器，避免干扰检测
                Collider[] colliders = previewObject.GetComponentsInChildren<Collider>();
                foreach (var col in colliders)
                {
                    col.enabled = false;
                }
                
                // 设置预览对象的透明度
                MakeObjectTransparent(previewObject);
            }
            
            previewObject.transform.position = currentGridPosition;
            
            // 改进的表面法线旋转计算
            Quaternion finalRotation = CalculateSurfaceAlignedRotation();
            previewObject.transform.rotation = finalRotation;
        }

        private void ShowDeletePreview()
        {
            // 在删除模式下高亮显示要删除的对象
            GameObject targetObject = GetTileAtPosition(currentGridPosition);
            if (targetObject != null && targetObject.name != "[PREVIEW]")
            {
                // 可以在这里添加高亮效果，比如改变材质颜色
                // 暂时通过Debug信息提示
                if (Event.current.type == EventType.Repaint)
                {
                    // 绘制删除目标的红色外框
                    Bounds bounds = GetObjectBounds(targetObject);
                    if (bounds.size != Vector3.zero)
                    {
                        Handles.color = Color.red;
                        Handles.DrawWireCube(bounds.center, bounds.size * 1.1f);
                    }
                }
            }
        }

        private Bounds GetObjectBounds(GameObject obj)
        {
            Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds();
            
            Bounds bounds = renderers[0].bounds;
            foreach (var renderer in renderers)
            {
                bounds.Encapsulate(renderer.bounds);
            }
            return bounds;
        }

        private Quaternion CalculateSurfaceAlignedRotation()
        {
            // 改进的表面对齐旋转计算
            Quaternion surfaceRotation = Quaternion.identity;
            
            // 只有当表面不是水平向上时才进行对齐
            if (Vector3.Dot(targetSurfaceNormal, Vector3.up) < 0.9f)
            {
                // 计算表面对齐的旋转
                Vector3 forward = Vector3.Cross(targetSurfaceNormal, Vector3.right);
                if (forward.magnitude < 0.1f)
                {
                    forward = Vector3.Cross(targetSurfaceNormal, Vector3.forward);
                }
                forward.Normalize();
                
                surfaceRotation = Quaternion.LookRotation(forward, targetSurfaceNormal);
            }
            
            // 应用用户旋转
            Quaternion userRotation = Quaternion.Euler(0, rotationY, 0);
            return surfaceRotation * userRotation;
        }

        /// <summary>
        /// 从场景中拾取对象，将其设为当前选中的prefab
        /// </summary>
        private void PickFromScene()
        {
            GameObject hitObject = GetTileAtPosition(currentGridPosition);
            if (hitObject != null && hitObject.name != "[PREVIEW]")
            {
                // 尝试找到对应的prefab
                GameObject prefab = null;
                
                // 首先尝试通过PrefabUtility获取
                string prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(hitObject);
                if (!string.IsNullOrEmpty(prefabPath))
                {
                    prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                }
                
                // 如果没找到，尝试通过名称匹配
                if (prefab == null)
                {
                    foreach (var categoryList in categorizedPrefabs.Values)
                    {
                        prefab = categoryList.FirstOrDefault(p => p.name == hitObject.name);
                        if (prefab != null) break;
                    }
                }
                
                if (prefab != null)
                {
                    selectedPrefab = prefab;
                    
                    // 同时获取该对象的旋转
                    Vector3 eulerAngles = hitObject.transform.eulerAngles;
                    rotationY = eulerAngles.y;
                    
                    Debug.Log($"已拾取: {prefab.name}");
                }
            }
        }

        private void MakeObjectTransparent(GameObject obj)
        {
            Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
            foreach (Renderer renderer in renderers)
            {
                Material[] materials = renderer.materials;
                for (int i = 0; i < materials.Length; i++)
                {
                    Material mat = new Material(materials[i]);
                    mat.color = new Color(mat.color.r, mat.color.g, mat.color.b, 0.5f);
                    materials[i] = mat;
                }
                renderer.materials = materials;
            }
        }

        private void DestroyPreviewObject()
        {
            if (previewObject != null)
            {
                DestroyImmediate(previewObject);
                previewObject = null;
            }        }
        #endregion

        #region 地图操作
        private void PlaceTile()
        {
            if (selectedPrefab == null) return;
            
            // 检查位置是否已经有对象（使用更精确的检测）
            if (IsTileAtPosition(currentGridPosition)) return;
            
            // 使用改进的旋转计算
            Quaternion finalRotation = CalculateSurfaceAlignedRotation();
            
            GameObject instance = Instantiate(selectedPrefab, currentGridPosition, finalRotation);
            instance.name = selectedPrefab.name;
            
            // 注册到撤销系统
            Undo.RegisterCreatedObjectUndo(instance, "Place Tile");
            
            // 记录到地图数据
            TileData tileData = new TileData
            {
                prefabName = selectedPrefab.name,
                position = currentGridPosition,
                rotation = finalRotation
            };
            currentMapTiles.Add(tileData);
            
            SceneView.RepaintAll();
        }

        private void DeleteTile()
        {
            GameObject tileToDelete = GetTileAtPosition(currentGridPosition);
            if (tileToDelete != null)
            {
                Undo.DestroyObjectImmediate(tileToDelete);
                
                // 从地图数据中移除
                currentMapTiles.RemoveAll(t => Vector3.Distance(t.position, currentGridPosition) < 0.1f);
                
                SceneView.RepaintAll();
            }
        }

        private bool IsTileAtPosition(Vector3 position)
        {
            return currentMapTiles.Any(t => Vector3.Distance(t.position, position) < 0.1f);
        }

        private GameObject GetTileAtPosition(Vector3 position)
        {
            Collider[] colliders = Physics.OverlapSphere(position, 0.5f);
            foreach (Collider col in colliders)
            {
                if (col.gameObject.name != "[PREVIEW]")
                    return col.gameObject;
            }
            return null;
        }

        private void NewMap()
        {
            if (EditorUtility.DisplayDialog("新建地图", "确定要新建地图吗？当前未保存的更改将丢失。", "确定", "取消"))
            {
                ClearMap();
                currentMapName = "NewMap";
            }
        }

        private void ClearMap()
        {
            // 删除场景中的所有tile
            foreach (TileData tile in currentMapTiles)
            {
                GameObject obj = GetTileAtPosition(tile.position);
                if (obj != null)
                    DestroyImmediate(obj);
            }
            
            currentMapTiles.Clear();
            SceneView.RepaintAll();
        }

        private void RefreshMapData()
        {
            // 重新扫描场景中的所有相关对象
            currentMapTiles.Clear();
            
            // 这里可以根据你的需要实现扫描逻辑
            // 例如查找带有特定标签或组件的对象
        }
        #endregion

        #region 数据序列化
        private void SaveMap()
        {
            if (!Directory.Exists(MAP_DATA_FOLDER))
                Directory.CreateDirectory(MAP_DATA_FOLDER);
            
            string filePath = Path.Combine(MAP_DATA_FOLDER, currentMapName + ".json");
            
            MapData mapData = new MapData
            {
                mapName = currentMapName,
                tiles = currentMapTiles.ToArray()
            };
            
            string json = JsonUtility.ToJson(mapData, true);
            File.WriteAllText(filePath, json);
            
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("保存成功", $"地图已保存到: {filePath}", "确定");
        }

        private void LoadMap()
        {
            string filePath = EditorUtility.OpenFilePanel("加载地图", MAP_DATA_FOLDER, "json");
            if (string.IsNullOrEmpty(filePath)) return;
            
            try
            {
                string json = File.ReadAllText(filePath);
                MapData mapData = JsonUtility.FromJson<MapData>(json);
                
                ClearMap();
                
                currentMapName = mapData.mapName;
                currentMapTiles = mapData.tiles.ToList();
                
                // 在场景中重建地图
                foreach (TileData tile in currentMapTiles)
                {
                    GameObject prefab = categorizedPrefabs.Values
                        .SelectMany(list => list)
                        .FirstOrDefault(p => p.name == tile.prefabName);
                    
                    if (prefab != null)
                    {
                        GameObject instance = Instantiate(prefab, tile.position, tile.rotation);
                        instance.name = prefab.name;
                    }
                }
                
                EditorUtility.DisplayDialog("加载成功", $"地图 '{mapData.mapName}' 已加载", "确定");
            }
            catch (System.Exception e)
            {
                EditorUtility.DisplayDialog("加载失败", $"无法加载地图文件:\n{e.Message}", "确定");
            }
        }
        #endregion

        #region 场景绘制辅助方法
        private void DrawGrid()
        {
            Handles.color = new Color(1, 1, 1, 0.3f);
            Vector3 center = currentGridPosition;
            int gridExtent = 20;
            
            for (int x = -gridExtent; x <= gridExtent; x++)
            {
                Vector3 start = new Vector3(center.x + x * this.gridSize, center.y, center.z - gridExtent * this.gridSize);
                Vector3 end = new Vector3(center.x + x * this.gridSize, center.y, center.z + gridExtent * this.gridSize);
                Handles.DrawLine(start, end);
            }
            
            for (int z = -gridExtent; z <= gridExtent; z++)
            {
                Vector3 start = new Vector3(center.x - gridExtent * this.gridSize, center.y, center.z + z * this.gridSize);
                Vector3 end = new Vector3(center.x + gridExtent * this.gridSize, center.y, center.z + z * this.gridSize);
                Handles.DrawLine(start, end);
            }
        }

        private void DrawSceneGizmos()
        {
            // 根据当前模式绘制不同颜色的网格位置指示器
            Color indicatorColor = Color.yellow; // 默认放置模式
            
            if (isCtrlPressed)
                indicatorColor = Color.red; // 删除模式
            else if (isAltPressed)
                indicatorColor = Color.cyan; // 拾取模式
            else if (isShiftPressed && isPainting)
                indicatorColor = Color.green; // 绘制模式
            
            Handles.color = indicatorColor;
            Handles.DrawWireCube(currentGridPosition, Vector3.one * gridSize * 0.9f);
              // 绘制表面法线指示器和智能堆叠信息
            if (targetSurfaceNormal != Vector3.up)
            {
                Handles.color = Color.magenta;
                Vector3 normalEnd = currentGridPosition + targetSurfaceNormal * gridSize;
                Handles.DrawLine(currentGridPosition, normalEnd);
                Handles.DrawWireCube(normalEnd, Vector3.one * 0.2f);
                
                // 绘制法线信息文本
                Handles.Label(normalEnd + Vector3.up * 0.5f, 
                    $"法线: {targetSurfaceNormal:F2}", EditorStyles.whiteLabel);
            }
            
            // 绘制当前工作高度平面指示器（仅在普通放置模式下）
            if (!isCtrlPressed && !isAltPressed)
            {
                Handles.color = new Color(0, 1, 1, 0.3f);
                Vector3 planeCenter = new Vector3(currentGridPosition.x, currentHeight, currentGridPosition.z);
                Handles.DrawWireCube(planeCenter, new Vector3(gridSize * 5, 0.1f, gridSize * 5));
                
                // 显示高度信息
                Handles.Label(planeCenter + Vector3.up * 0.5f, 
                    $"工作高度: {currentHeight:F1}", EditorStyles.whiteLabel);
            }
            
            // 绘制坐标轴和位置信息
            Handles.color = Color.red;
            Handles.DrawLine(currentGridPosition, currentGridPosition + Vector3.right * 0.5f);
            Handles.color = Color.green;
            Handles.DrawLine(currentGridPosition, currentGridPosition + Vector3.up * 0.5f);
            Handles.color = Color.blue;
            Handles.DrawLine(currentGridPosition, currentGridPosition + Vector3.forward * 0.5f);
            
            // 显示当前位置信息
            Handles.Label(currentGridPosition + Vector3.up * 1f, 
                $"位置: {currentGridPosition:F1}", EditorStyles.whiteLabel);
        }

        private void ProcessEvents()
        {
            // 处理窗口级别的事件
            Event e = Event.current;
            if (e.type == EventType.KeyDown)
            {
                // 保留一些快捷键用于快速操作
                switch (e.keyCode)
                {
                    case KeyCode.F1:
                        // 显示帮助信息
                        EditorUtility.DisplayDialog("TileMapEditor 帮助", 
                            "实时交互模式:\n" +
                            "• 普通模式: 点击放置\n" +
                            "• Shift + 拖拽: 连续绘制\n" +
                            "• Ctrl + 点击: 删除\n" +
                            "• Alt + 点击: 场景拾取\n\n" +
                            "快捷键:\n" +
                            "• R: 旋转prefab\n" +
                            "• G: 切换网格显示\n" +
                            "• Q/E: 调整高度\n" +
                            "• ESC: 取消选择", "确定");
                        e.Use();
                        break;
                }
            }
        }
        #endregion

        #region 验证与设置
        /// <summary>
        /// 验证必要的文件夹和资源是否存在
        /// </summary>
        private bool ValidateSetup()
        {
            // 检查模型文件夹
            if (!AssetDatabase.IsValidFolder(TILE_FOLDER_PATH))
            {
                EditorUtility.DisplayDialog("设置错误", 
                    $"未找到模型文件夹: {TILE_FOLDER_PATH}\n请确保KayKit模型包已正确导入。", "确定");
                return false;
            }

            // 检查是否有模型文件
            string[] guids = AssetDatabase.FindAssets("t:GameObject", new[] { TILE_FOLDER_PATH });
            if (guids.Length == 0)
            {
                EditorUtility.DisplayDialog("设置错误", 
                    $"在 {TILE_FOLDER_PATH} 中未找到任何模型文件。", "确定");
                return false;
            }

            // 创建地图数据文件夹
            if (!AssetDatabase.IsValidFolder(MAP_DATA_FOLDER))
            {
                AssetDatabase.CreateFolder("Assets", "MapData");
                AssetDatabase.Refresh();
            }

            return true;
        }

        /// <summary>
        /// 获取当前网格大小（供外部访问）
        /// </summary>
        public float GetGridSize() => gridSize;
        #endregion
    }

    #region 数据结构
    [System.Serializable]
    public class TileData
    {
        public string prefabName;
        public Vector3 position;
        public Quaternion rotation;
    }

    [System.Serializable]
    public class MapData
    {
        public string mapName;
        public TileData[] tiles;
    }
    #endregion
}
