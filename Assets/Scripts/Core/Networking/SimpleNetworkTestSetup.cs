using UnityEngine;

namespace DWHITE
{
    /// <summary>
    /// 简单的网络测试场景设置 - 用于快速设置基础网络测试环境
    /// </summary>
    public class SimpleNetworkTestSetup : MonoBehaviour
    {
        [Header("场景设置")]
        [SerializeField] private bool _autoSetupOnStart = true;
        [SerializeField] private bool _showSetupInfo = true;
        
        [Header("网络管理器")]
        [SerializeField] private bool _createNetworkManager = true;
        [SerializeField] private string _gameVersion = "1.0";
        [SerializeField] private byte _maxPlayers = 4;
        
        private void Start()
        {
            if (_autoSetupOnStart)
            {
                SetupNetworkTestScene();
            }
        }
        
        /// <summary>
        /// 设置网络测试场景
        /// </summary>
        public void SetupNetworkTestScene()
        {
            LogSetup("开始设置简单网络测试场景...");
            
            // 确保有网络管理器
            if (_createNetworkManager)
            {
                EnsureSimpleNetworkManager();
            }
            
            LogSetup("网络测试场景设置完成");
        }
        
        /// <summary>
        /// 确保场景中有简单网络管理器
        /// </summary>
        private void EnsureSimpleNetworkManager()
        {
            SimpleNetworkManager networkManager = FindObjectOfType<SimpleNetworkManager>();
            if (networkManager == null)
            {
                GameObject nmObj = new GameObject("SimpleNetworkManager");
                networkManager = nmObj.AddComponent<SimpleNetworkManager>();
                
                // 设置基本配置（通过反射或公共方法）
                LogSetup("创建了 SimpleNetworkManager");
            }
            else
            {
                LogSetup("发现已存在的 SimpleNetworkManager");
            }
        }
        
        /// <summary>
        /// 手动触发网络连接（用于测试）
        /// </summary>
        [ContextMenu("连接到网络")]
        public void ConnectToNetwork()
        {
            SimpleNetworkManager networkManager = FindObjectOfType<SimpleNetworkManager>();
            if (networkManager != null)
            {
                networkManager.ConnectToPhoton();
                LogSetup("尝试连接到网络");
            }
            else
            {
                LogSetup("未找到 SimpleNetworkManager");
            }
        }
        
        /// <summary>
        /// 加入或创建房间（用于测试）
        /// </summary>
        [ContextMenu("加入/创建房间")]  
        public void JoinOrCreateRoom()
        {
            SimpleNetworkManager networkManager = FindObjectOfType<SimpleNetworkManager>();
            if (networkManager != null)
            {
                networkManager.JoinOrCreateRoom("TestRoom");
                LogSetup("尝试加入/创建房间");
            }
            else
            {
                LogSetup("未找到 SimpleNetworkManager");
            }
        }
        
        private void LogSetup(string message)
        {
            if (_showSetupInfo)
            {
                Debug.Log($"[SimpleNetworkTestSetup] {message}");
            }
        }
        
        private void OnGUI()
        {
            if (!_showSetupInfo) return;
            
            GUILayout.BeginArea(new Rect(10, 150, 300, 200));
            
            GUILayout.Label("=== 简单网络测试设置 ===");
            
            if (GUILayout.Button("设置测试场景"))
            {
                SetupNetworkTestScene();
            }
            
            if (GUILayout.Button("连接网络"))
            {
                ConnectToNetwork();
            }
            
            if (GUILayout.Button("加入/创建房间"))
            {
                JoinOrCreateRoom();
            }
            
            GUILayout.EndArea();
        }
    }
}
