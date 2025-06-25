using UnityEngine;

namespace DWHITE.UI
{
    /// <summary>
    /// UI元素基类
    /// 提供通用的UI组件实现
    /// </summary>
    public abstract class UIElementBase : MonoBehaviour, IUIElement
    {
        [Header("UI元素基础设置")]
        [SerializeField] protected bool _showDebugInfo = false;
        [SerializeField] protected GameObject _rootGameObject;
        
        protected PlayerStatusManager _statusManager;
        protected bool _isInitialized = false;
        
        #region IUIElement 接口实现
        
        public virtual void Initialize(PlayerStatusManager statusManager)
        {
            if (_isInitialized)
            {
                LogUI("UI元素已经初始化，跳过重复初始化");
                return;
            }
            
            _statusManager = statusManager;
            
            if (_statusManager == null)
            {
                LogUI("警告: StatusManager为空，无法初始化UI元素");
                return;
            }
            
            // 如果没有指定根对象，使用当前GameObject
            if (_rootGameObject == null)
            {
                _rootGameObject = gameObject;
            }
            
            // 订阅事件
            SubscribeToEvents();
            
            // 执行具体的初始化逻辑
            OnInitialize();
            
            _isInitialized = true;
            LogUI($"{GetType().Name} UI元素初始化完成");
        }
        
        public virtual void Cleanup()
        {
            if (!_isInitialized) return;
            
            // 取消事件订阅
            UnsubscribeFromEvents();
            
            // 执行具体的清理逻辑
            OnCleanup();
            
            _isInitialized = false;
            _statusManager = null;
            
            LogUI($"{GetType().Name} UI元素已清理");
        }
        
        public virtual void SetVisible(bool visible)
        {
            if (_rootGameObject != null)
            {
                _rootGameObject.SetActive(visible);
            }
        }
        
        public virtual void RefreshUI()
        {
            if (!_isInitialized)
            {
                LogUI("UI元素未初始化，无法刷新");
                return;
            }
            
            OnRefreshUI();
        }
        
        public bool IsInitialized => _isInitialized;
        
        #endregion
        
        #region Unity 生命周期
        
        protected virtual void OnDestroy()
        {
            Cleanup();
        }
        
        #endregion
        
        #region 抽象方法 - 子类必须实现
        
        /// <summary>
        /// 订阅所需的事件
        /// </summary>
        protected abstract void SubscribeToEvents();
        
        /// <summary>
        /// 取消事件订阅
        /// </summary>
        protected abstract void UnsubscribeFromEvents();
        
        /// <summary>
        /// 执行具体的初始化逻辑
        /// </summary>
        protected abstract void OnInitialize();
        
        /// <summary>
        /// 执行具体的清理逻辑
        /// </summary>
        protected virtual void OnCleanup() { }
        
        /// <summary>
        /// 刷新UI显示
        /// </summary>
        protected abstract void OnRefreshUI();
        
        #endregion
        
        #region 工具方法
        
        /// <summary>
        /// 安全的事件订阅
        /// </summary>
        /// <param name="eventName">事件名称，用于调试</param>
        /// <param name="subscribeAction">订阅操作</param>
        protected void SafeSubscribe(string eventName, System.Action subscribeAction)
        {
            try
            {
                subscribeAction?.Invoke();
                LogUI($"成功订阅事件: {eventName}");
            }
            catch (System.Exception e)
            {
                LogUI($"订阅事件失败 {eventName}: {e.Message}");
            }
        }
        
        /// <summary>
        /// 安全的事件取消订阅
        /// </summary>
        /// <param name="eventName">事件名称，用于调试</param>
        /// <param name="unsubscribeAction">取消订阅操作</param>
        protected void SafeUnsubscribe(string eventName, System.Action unsubscribeAction)
        {
            try
            {
                unsubscribeAction?.Invoke();
                LogUI($"成功取消订阅事件: {eventName}");
            }
            catch (System.Exception e)
            {
                LogUI($"取消订阅事件失败 {eventName}: {e.Message}");
            }
        }
        
        /// <summary>
        /// 调试日志输出
        /// </summary>
        /// <param name="message">日志信息</param>
        protected void LogUI(string message)
        {
            if (_showDebugInfo)
            {
                Debug.Log($"[{GetType().Name}] {message}");
            }
        }
        
        #endregion
    }
}
