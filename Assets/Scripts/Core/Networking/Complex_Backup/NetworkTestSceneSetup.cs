using UnityEngine;
using Photon.Pun;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DWHITE
{
    /// <summary>
    /// 网络测试场景配置器 - 快速设置网络测试环境
    /// </summary>
    public class NetworkTestSceneSetup : MonoBehaviour
    {
        [Header("自动设置配置")]
        [SerializeField] private bool _autoSetupOnStart = true;
        [SerializeField] private bool _createSpawnPoints = true;
        [SerializeField] private bool _addTestHelper = true;
        [SerializeField] private bool _configureCamera = true;
        
        [Header("生成点配置")]
        [SerializeField] private int _spawnPointCount = 4;
        [SerializeField] private float _spawnRadius = 10f;
        [SerializeField] private float _spawnHeight = 2f;
        
        [Header("测试对象")]
        [SerializeField] private GameObject _playerPrefab;
        [SerializeField] private GameObject _testObjectPrefab;
        
        [Space]
        [Header("手动操作")]
        [SerializeField] private bool _setupScene = false;
        [SerializeField] private bool _clearScene = false;
        
        private Transform[] _spawnPoints;
        private NetworkTestHelper _testHelper;
        
        private void Start()
        {
            if (_autoSetupOnStart)
            {
                SetupTestScene();
            }
        }
        
        private void OnValidate()
        {
            if (_setupScene)
            {
                _setupScene = false;
                SetupTestScene();
            }
            
            if (_clearScene)
            {
                _clearScene = false;
                ClearTestScene();
            }
        }
        
        /// <summary>
        /// 设置测试场景
        /// </summary>
        [ContextMenu("设置测试场景")]
        public void SetupTestScene()
        {
            Debug.Log("开始设置网络测试场景...");
            
            if (_createSpawnPoints)
            {
                CreateSpawnPoints();
            }
            
            if (_addTestHelper)
            {
                AddNetworkTestHelper();
            }
            
            if (_configureCamera)
            {
                ConfigureCamera();
            }
            
            // 确保有 NetworkManager
            EnsureNetworkManager();
            
            Debug.Log("网络测试场景设置完成！");
            LogInstructions();
        }
        
        /// <summary>
        /// 清理测试场景
        /// </summary>
        [ContextMenu("清理测试场景")]
        public void ClearTestScene()
        {
            Debug.Log("清理测试场景...");
            
            // 清理生成点
            GameObject spawnContainer = GameObject.Find("SpawnPoints");
            if (spawnContainer != null)
            {
                DestroyImmediate(spawnContainer);
            }
            
            // 清理测试助手
            NetworkTestHelper existingHelper = FindObjectOfType<NetworkTestHelper>();
            if (existingHelper != null)
            {
                DestroyImmediate(existingHelper.gameObject);
            }
            
            Debug.Log("测试场景清理完成");
        }
        
        private void CreateSpawnPoints()
        {
            // 创建生成点容器
            GameObject spawnContainer = GameObject.Find("SpawnPoints");
            if (spawnContainer == null)
            {
                spawnContainer = new GameObject("SpawnPoints");
            }
            
            // 清理旧的生成点
            for (int i = spawnContainer.transform.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(spawnContainer.transform.GetChild(i).gameObject);
            }
            
            // 创建新的生成点
            _spawnPoints = new Transform[_spawnPointCount];
            
            for (int i = 0; i < _spawnPointCount; i++)
            {
                float angle = (360f / _spawnPointCount) * i;
                Vector3 position = new Vector3(
                    Mathf.Cos(angle * Mathf.Deg2Rad) * _spawnRadius,
                    _spawnHeight,
                    Mathf.Sin(angle * Mathf.Deg2Rad) * _spawnRadius
                );
                
                GameObject spawnPoint = new GameObject($"SpawnPoint_{i + 1}");
                spawnPoint.transform.position = position;
                spawnPoint.transform.parent = spawnContainer.transform;
                
                // 添加可视化
                var gizmo = spawnPoint.AddComponent<SpawnPointGizmo>();
                gizmo.color = Color.green;
                gizmo.size = 1f;
                
                _spawnPoints[i] = spawnPoint.transform;
            }
            
            Debug.Log($"创建了 {_spawnPointCount} 个生成点");
        }
        
        private void AddNetworkTestHelper()
        {
            // 检查是否已存在
            NetworkTestHelper existingHelper = FindObjectOfType<NetworkTestHelper>();
            if (existingHelper != null)
            {
                _testHelper = existingHelper;
                Debug.Log("发现已存在的 NetworkTestHelper");
            }
            else
            {
                // 创建新的测试助手
                GameObject helperObj = new GameObject("NetworkTestHelper");
                _testHelper = helperObj.AddComponent<NetworkTestHelper>();
                Debug.Log("创建了新的 NetworkTestHelper");
            }
            
            // 配置测试助手
            ConfigureTestHelper();
        }
        
        private void ConfigureTestHelper()
        {
            if (_testHelper == null) return;
            
            // 使用反射设置私有字段（仅在编辑器中）
#if UNITY_EDITOR
            var helperType = typeof(NetworkTestHelper);
            
            // 设置玩家预制体
            if (_playerPrefab != null)
            {
                var playerPrefabField = helperType.GetField("_playerPrefab", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                playerPrefabField?.SetValue(_testHelper, _playerPrefab);
            }
            
            // 设置生成点
            if (_spawnPoints != null && _spawnPoints.Length > 0)
            {
                var spawnPointsField = helperType.GetField("_spawnPoints", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                spawnPointsField?.SetValue(_testHelper, _spawnPoints);
            }
            
            // 设置测试对象
            if (_testObjectPrefab != null)
            {
                var testObjectField = helperType.GetField("_testObjectPrefab", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                testObjectField?.SetValue(_testHelper, _testObjectPrefab);
            }
            
            Debug.Log("NetworkTestHelper 配置完成");
#endif
        }
        
        private void ConfigureCamera()
        {
            Camera mainCamera = Camera.main;
            if (mainCamera == null)
            {
                mainCamera = FindObjectOfType<Camera>();
            }
            
            if (mainCamera != null)
            {
                // 设置相机位置以便观察整个测试区域
                mainCamera.transform.position = new Vector3(0, 15, -15);
                mainCamera.transform.rotation = Quaternion.Euler(30, 0, 0);
                
                Debug.Log("配置了主相机位置");
            }
        }
        
        private void EnsureNetworkManager()
        {
            NetworkManager networkManager = FindObjectOfType<NetworkManager>();
            if (networkManager == null)
            {
                GameObject nmObj = new GameObject("NetworkManager");
                nmObj.AddComponent<NetworkManager>();
                Debug.Log("创建了 NetworkManager");
            }
            else
            {
                Debug.Log("发现已存在的 NetworkManager");
            }
        }
        
        private void LogInstructions()
        {
            Debug.Log("=== 网络测试场景使用说明 ===");
            Debug.Log("1. 运行场景后，NetworkTestHelper 会自动连接到 Photon");
            Debug.Log("2. 使用以下快捷键进行测试：");
            Debug.Log("   F1 - 快速加入房间");
            Debug.Log("   F2 - 离开房间");
            Debug.Log("   F3 - 生成玩家");
            Debug.Log("3. 或使用屏幕左上角的 GUI 按钮");
            Debug.Log("4. 建议同时运行 Editor 和 Build 版本进行多客户端测试");
            Debug.Log("========================");
        }
    }
    
    /// <summary>
    /// 生成点可视化组件
    /// </summary>
    public class SpawnPointGizmo : MonoBehaviour
    {
        public Color color = Color.green;
        public float size = 1f;
        
        private void OnDrawGizmos()
        {
            Gizmos.color = color;
            Gizmos.DrawWireSphere(transform.position, size);
            Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 2f);
        }
        
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(transform.position, size * 0.8f);
        }
    }
}

#if UNITY_EDITOR
namespace DWHITE.Editor
{
    [CustomEditor(typeof(NetworkTestSceneSetup))]
    public class NetworkTestSceneSetupEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            
            GUILayout.Space(10);
            
            NetworkTestSceneSetup setup = (NetworkTestSceneSetup)target;
            
            if (GUILayout.Button("设置测试场景", GUILayout.Height(30)))
            {
                setup.SetupTestScene();
            }
            
            if (GUILayout.Button("清理测试场景", GUILayout.Height(30)))
            {
                setup.ClearTestScene();
            }
            
            GUILayout.Space(5);
            
            EditorGUILayout.HelpBox(
                "使用此脚本可以快速设置网络测试环境：\n" +
                "1. 创建玩家生成点\n" +
                "2. 添加网络测试助手\n" +
                "3. 配置相机位置\n" +
                "4. 确保网络管理器存在",
                MessageType.Info
            );
            
            if (Application.isPlaying)
            {
                GUILayout.Space(5);
                EditorGUILayout.HelpBox(
                    "运行时快捷键：\n" +
                    "F1 - 快速加入房间\n" +
                    "F2 - 离开房间\n" +
                    "F3 - 生成玩家",
                    MessageType.None
                );
            }
        }
    }
}
#endif
