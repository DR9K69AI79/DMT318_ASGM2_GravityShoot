using UnityEngine;

namespace DWHITE.UI
{
    /// <summary>
    /// UI元素基础接口
    /// 定义所有UI组件的通用行为
    /// </summary>
    public interface IUIElement
    {
        /// <summary>
        /// 初始化UI元素
        /// </summary>
        /// <param name="statusManager">玩家状态管理器，用于事件订阅</param>
        void Initialize(PlayerStatusManager statusManager);
        
        /// <summary>
        /// 清理资源和取消事件订阅
        /// </summary>
        void Cleanup();
        
        /// <summary>
        /// 设置UI元素的可见性
        /// </summary>
        /// <param name="visible">是否可见</param>
        void SetVisible(bool visible);
        
        /// <summary>
        /// 强制刷新UI显示
        /// </summary>
        void RefreshUI();
        
        /// <summary>
        /// UI元素是否已初始化
        /// </summary>
        bool IsInitialized { get; }
    }
}
