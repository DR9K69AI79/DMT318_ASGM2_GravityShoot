using UnityEngine;

namespace DWHITE.Editor
{
    /// <summary>
    /// Tile Map Editor 配置文件
    /// 可以通过 Create > Tile Map > Editor Config 创建
    /// </summary>
    [CreateAssetMenu(fileName = "TileMapEditorConfig", menuName = "Tile Map/Editor Config")]
    public class TileMapEditorConfig : ScriptableObject
    {
        [Header("网格设置")]
        [Tooltip("网格单元大小，通常与tile模型大小匹配")]
        public float gridSize = 2f;
        
        [Tooltip("网格高度偏移，用于调整显示")]
        public float gridHeightOffset = 0.01f;
        
        [Tooltip("默认开启网格对齐")]
        public bool snapToGridByDefault = true;
        
        [Header("显示设置")]
        [Tooltip("默认显示网格线")]
        public bool showGridByDefault = true;
        
        [Tooltip("网格线颜色")]
        public Color gridColor = new Color(1f, 1f, 1f, 0.3f);
        
        [Tooltip("选中位置高亮颜色")]
        public Color highlightColor = Color.yellow;
        
        [Header("预览设置")]
        [Tooltip("预览对象透明度")]
        [Range(0.1f, 0.8f)]
        public float previewAlpha = 0.5f;
        
        [Tooltip("预览图标大小")]
        public Vector2 previewIconSize = new Vector2(40f, 40f);
        
        [Header("模型路径")]
        [Tooltip("模型资源根目录")]
        public string modelRootPath = "Assets/Art/KayKit/KayKit_SpaceBase";
        
        [Tooltip("地图数据保存目录")]
        public string mapDataPath = "Assets/MapData";
        
        [Header("快捷键设置")]
        [Tooltip("旋转快捷键")]
        public KeyCode rotateKey = KeyCode.R;
        
        [Tooltip("切换网格显示快捷键")]
        public KeyCode toggleGridKey = KeyCode.G;
        
        [Tooltip("取消选择快捷键")]
        public KeyCode cancelKey = KeyCode.Escape;
        
        [Header("地形生成预设")]
        [Tooltip("默认平台尺寸")]
        public Vector2Int defaultPlatformSize = new Vector2Int(10, 10);
        
        [Tooltip("环形地形默认半径")]
        public Vector2 defaultRingRadius = new Vector2(5f, 10f);
        
        [Header("性能设置")]
        [Tooltip("LOD距离设置")]
        public float[] lodDistances = { 0.6f, 0.3f, 0.1f };
        
        [Tooltip("批量操作时的最大撤销组数")]
        public int maxUndoGroups = 50;
        
        [Header("验证设置")]
        [Tooltip("重叠检测容差")]
        public float overlapTolerance = 0.1f;
        
        [Tooltip("孤立对象检测距离")]
        public float isolationDistance = 3f;
        
        [Header("分类配置")]
        [Tooltip("是否启用emoji图标分类")]
        public bool useEmojiCategories = true;
        
        [Tooltip("自定义分类规则")]
        public CategoryRule[] customCategoryRules = new CategoryRule[]
        {
            new CategoryRule { keyword = "terrain", category = "🌍 地形", priority = 0 },
            new CategoryRule { keyword = "basemodule", category = "🏢 基础建筑", priority = 1 },
            new CategoryRule { keyword = "tunnel", category = "🚇 隧道", priority = 2 },
        };
    }
    
    [System.Serializable]
    public class CategoryRule
    {
        [Tooltip("关键词（模型名称包含此关键词时匹配）")]
        public string keyword;
        
        [Tooltip("分类名称")]
        public string category;
        
        [Tooltip("优先级（数字越小优先级越高）")]
        public int priority;
    }
}
