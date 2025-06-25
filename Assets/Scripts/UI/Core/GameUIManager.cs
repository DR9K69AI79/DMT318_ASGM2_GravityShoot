using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace DWHITE.UI
{
    /// <summary>
    /// 游戏UI管理器
    /// 负责管理所有UI组件的生命周期和统一初始化
    /// </summary>
    public class GameUIManager : MonoBehaviour
    {
        [Header("UI管理器设置")]
        [SerializeField] private bool _autoFindLocalPlayer = true;
        [SerializeField] private bool _showDebugInfo = false;
        [SerializeField] private PlayerStatusManager _targetPlayer;
        
        [Header("UI组件")]
        [SerializeField] private List<MonoBehaviour> _uiElements = new List<MonoBehaviour>();
        
        // 运行时状态
        private List<IUIElement> _managedElements = new List<IUIElement>();
        private bool _isInitialized = false;
        
        #region Unity 生命周期
        
        private void Awake()
        {
            // 自动查找UI元素
            AutoDiscoverUIElements();
        }
        
        private void Start()
        {
            InitializeUISystem();
        }
        
        private void OnDestroy()
        {
            CleanupUISystem();
        }
        
        #endregion
        
        #region 初始化和清理
        
        /// <summary>
        /// 初始化UI系统
        /// </summary>
        private void InitializeUISystem()
        {
            if (_isInitialized)
            {
                LogUI("UI系统已经初始化");
                return;
            }
            
            // 查找目标玩家
            PlayerStatusManager targetPlayer = FindTargetPlayer();
            if (targetPlayer == null)
            {
                LogUI("错误: 无法找到目标玩家的StatusManager");
                return;
            }
            
            // 初始化所有UI元素
            int successCount = 0;
            foreach (var element in _managedElements)
            {
                try
                {
                    element.Initialize(targetPlayer);
                    successCount++;
                }
                catch (System.Exception e)
                {
                    LogUI($"初始化UI元素失败: {element.GetType().Name} - {e.Message}");
                }
            }
            
            _isInitialized = true;
            LogUI($"UI系统初始化完成，成功初始化 {successCount}/{_managedElements.Count} 个UI元素");
        }
        
        /// <summary>
        /// 清理UI系统
        /// </summary>
        private void CleanupUISystem()
        {
            if (!_isInitialized) return;
            
            foreach (var element in _managedElements)
            {
                try
                {
                    element?.Cleanup();
                }
                catch (System.Exception e)
                {
                    LogUI($"清理UI元素失败: {element?.GetType().Name} - {e.Message}");
                }
            }
            
            _isInitialized = false;
            LogUI("UI系统已清理");
        }
        
        #endregion
        
        #region 玩家查找
        
        /// <summary>
        /// 查找目标玩家
        /// </summary>
        /// <returns>目标玩家的StatusManager</returns>
        private PlayerStatusManager FindTargetPlayer()
        {
            // 如果手动指定了目标玩家
            if (_targetPlayer != null)
            {
                LogUI("使用手动指定的目标玩家");
                return _targetPlayer;
            }
            
            // 自动查找本地玩家
            if (_autoFindLocalPlayer)
            {
                return FindLocalPlayer();
            }
            
            LogUI("警告: 未找到目标玩家");
            return null;
        }
        
        /// <summary>
        /// 查找本地玩家
        /// </summary>
        /// <returns>本地玩家的StatusManager</returns>
        private PlayerStatusManager FindLocalPlayer()
        {
            // 通过Photon查找本地玩家
            var allPlayers = FindObjectsOfType<PlayerStatusManager>();
            
            foreach (var player in allPlayers)
            {
                // 检查是否是本地玩家
                var photonView = player.GetComponent<Photon.Pun.PhotonView>();
                if (photonView != null && photonView.IsMine)
                {
                    LogUI($"找到本地玩家: {player.name}");
                    return player;
                }
            }
            
            // 如果没有Photon环境，返回第一个找到的玩家
            if (allPlayers.Length > 0)
            {
                LogUI($"非网络环境，使用第一个玩家: {allPlayers[0].name}");
                return allPlayers[0];
            }
            
            LogUI("警告: 未找到任何玩家StatusManager");
            return null;
        }
        
        #endregion
        
        #region UI元素管理
        
        /// <summary>
        /// 自动发现UI元素
        /// </summary>
        private void AutoDiscoverUIElements()
        {
            _managedElements.Clear();
            
            // 从序列化列表中添加
            foreach (var element in _uiElements)
            {
                if (element is IUIElement uiElement)
                {
                    _managedElements.Add(uiElement);
                }
                else
                {
                    LogUI($"警告: {element.name} 不实现IUIElement接口");
                }
            }
            
            // 自动查找子对象中的UI元素
            var childElements = GetComponentsInChildren<IUIElement>(true);
            foreach (var element in childElements)
            {
                if (!_managedElements.Contains(element))
                {
                    _managedElements.Add(element);
                    LogUI($"自动发现UI元素: {element.GetType().Name}");
                }
            }
            
            LogUI($"发现 {_managedElements.Count} 个UI元素");
        }
        
        /// <summary>
        /// 添加UI元素
        /// </summary>
        /// <param name="element">要添加的UI元素</param>
        public void AddUIElement(IUIElement element)
        {
            if (element == null || _managedElements.Contains(element))
                return;
            
            _managedElements.Add(element);
            
            // 如果系统已初始化，立即初始化新元素
            if (_isInitialized)
            {
                var targetPlayer = FindTargetPlayer();
                if (targetPlayer != null)
                {
                    element.Initialize(targetPlayer);
                }
            }
            
            LogUI($"添加UI元素: {element.GetType().Name}");
        }
        
        /// <summary>
        /// 移除UI元素
        /// </summary>
        /// <param name="element">要移除的UI元素</param>
        public void RemoveUIElement(IUIElement element)
        {
            if (element == null || !_managedElements.Contains(element))
                return;
            
            element.Cleanup();
            _managedElements.Remove(element);
            
            LogUI($"移除UI元素: {element.GetType().Name}");
        }
        
        #endregion
        
        #region 公共接口
        
        /// <summary>
        /// 设置所有UI的可见性
        /// </summary>
        /// <param name="visible">是否可见</param>
        public void SetAllUIVisible(bool visible)
        {
            foreach (var element in _managedElements)
            {
                element?.SetVisible(visible);
            }
            
            LogUI($"设置所有UI可见性: {visible}");
        }
        
        /// <summary>
        /// 刷新所有UI
        /// </summary>
        public void RefreshAllUI()
        {
            foreach (var element in _managedElements)
            {
                element?.RefreshUI();
            }
            
            LogUI("刷新所有UI");
        }
        
        /// <summary>
        /// 获取指定类型的UI元素
        /// </summary>
        /// <typeparam name="T">UI元素类型</typeparam>
        /// <returns>UI元素实例</returns>
        public T GetUIElement<T>() where T : class, IUIElement
        {
            return _managedElements.OfType<T>().FirstOrDefault();
        }
        
        /// <summary>
        /// 重新初始化UI系统
        /// </summary>
        public void ReinitializeUISystem()
        {
            CleanupUISystem();
            InitializeUISystem();
        }
        
        #endregion
        
        #region 调试
        
        /// <summary>
        /// 调试日志输出
        /// </summary>
        /// <param name="message">日志信息</param>
        private void LogUI(string message)
        {
            if (_showDebugInfo)
            {
                Debug.Log($"[GameUIManager] {message}");
            }
        }
        
        /// <summary>
        /// 获取UI系统状态信息
        /// </summary>
        /// <returns>状态信息</returns>
        public string GetSystemStatus()
        {
            return $"UI系统状态: {(_isInitialized ? "已初始化" : "未初始化")}, " +
                   $"管理元素数量: {_managedElements.Count}, " +
                   $"初始化元素数量: {_managedElements.Count(e => e.IsInitialized)}";
        }
        
        #endregion
    }
}
