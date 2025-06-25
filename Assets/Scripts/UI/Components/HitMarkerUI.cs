using UnityEngine;
using System.Collections;
using DWHITE.Weapons;

namespace DWHITE.UI
{
    /// <summary>
    /// 命中标记UI组件
    /// 负责显示射击命中的视觉反馈
    /// </summary>
    public class HitMarkerUI : UIElementBase
    {
        [Header("命中标记组件")]
        [SerializeField] private GameObject _hitMarker;
        [SerializeField] private float _displayDuration = 0.2f;
        
        [Header("动画设置")]
        [SerializeField] private bool _enableScaleAnimation = true;
        [SerializeField] private bool _enableFadeAnimation = true;
        [SerializeField] private AnimationCurve _scaleCurve = AnimationCurve.EaseInOut(0, 0.5f, 1, 1.2f);
        [SerializeField] private AnimationCurve _fadeCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);
        
        // 组件引用
        private CanvasGroup _canvasGroup;
        private Transform _markerTransform;
        
        // 动画状态
        private Coroutine _displayCoroutine;
        private Vector3 _originalScale;
        
        #region 事件订阅
        
        protected override void SubscribeToEvents()
        {
            SafeSubscribe("ProjectileBase.OnProjectileHit", () => 
                ProjectileBase.OnProjectileHit += OnProjectileHit);
        }
        
        protected override void UnsubscribeFromEvents()
        {
            SafeUnsubscribe("ProjectileBase.OnProjectileHit", () => 
                ProjectileBase.OnProjectileHit -= OnProjectileHit);
        }
        
        #endregion
        
        #region 初始化和刷新
        
        protected override void OnInitialize()
        {
            if (_hitMarker == null)
            {
                LogUI("警告: 未设置命中标记GameObject");
                return;
            }
            
            // 获取或添加CanvasGroup组件
            _canvasGroup = _hitMarker.GetComponent<CanvasGroup>();
            if (_canvasGroup == null && _enableFadeAnimation)
            {
                _canvasGroup = _hitMarker.AddComponent<CanvasGroup>();
            }
            
            // 获取Transform组件
            _markerTransform = _hitMarker.transform;
            _originalScale = _markerTransform.localScale;
            
            // 初始状态设置为隐藏
            _hitMarker.SetActive(false);
            
            LogUI("命中标记UI初始化完成");
        }
        
        protected override void OnRefreshUI()
        {
            // 重置状态
            if (_hitMarker != null)
            {
                _hitMarker.SetActive(false);
                
                if (_markerTransform != null)
                {
                    _markerTransform.localScale = _originalScale;
                }
                
                if (_canvasGroup != null)
                {
                    _canvasGroup.alpha = 1f;
                }
            }
        }
        
        protected override void OnCleanup()
        {
            // 停止显示协程
            if (_displayCoroutine != null)
            {
                StopCoroutine(_displayCoroutine);
                _displayCoroutine = null;
            }
        }
        
        #endregion
        
        #region 事件处理
        
        private void OnProjectileHit(ProjectileBase projectile, RaycastHit hit)
        {
            // 检查是否是当前玩家发射的投射物
            if (projectile.transform.root != _statusManager.transform) return;
            
            LogUI("检测到投射物命中，显示命中标记");
            ShowHitMarker();
        }
        
        #endregion
        
        #region 显示逻辑
        
        /// <summary>
        /// 显示命中标记
        /// </summary>
        public void ShowHitMarker()
        {
            if (_hitMarker == null) return;
            
            // 停止之前的显示协程
            if (_displayCoroutine != null)
            {
                StopCoroutine(_displayCoroutine);
            }
            
            // 启动新的显示协程
            _displayCoroutine = StartCoroutine(DisplayHitMarkerCoroutine());
        }
        
        /// <summary>
        /// 命中标记显示协程
        /// </summary>
        private IEnumerator DisplayHitMarkerCoroutine()
        {
            // 激活标记
            _hitMarker.SetActive(true);
            
            // 重置初始状态
            if (_markerTransform != null)
            {
                _markerTransform.localScale = _originalScale;
            }
            
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 1f;
            }
            
            // 播放动画
            float elapsed = 0f;
            
            while (elapsed < _displayDuration)
            {
                elapsed += Time.deltaTime;
                float progress = elapsed / _displayDuration;
                
                // 缩放动画
                if (_enableScaleAnimation && _markerTransform != null)
                {
                    float scaleMultiplier = _scaleCurve.Evaluate(progress);
                    _markerTransform.localScale = _originalScale * scaleMultiplier;
                }
                
                // 淡出动画
                if (_enableFadeAnimation && _canvasGroup != null)
                {
                    _canvasGroup.alpha = _fadeCurve.Evaluate(progress);
                }
                
                yield return null;
            }
            
            // 隐藏标记
            _hitMarker.SetActive(false);
            
            // 重置状态
            if (_markerTransform != null)
            {
                _markerTransform.localScale = _originalScale;
            }
            
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 1f;
            }
            
            _displayCoroutine = null;
        }
        
        #endregion
        
        #region 公共接口
        
        /// <summary>
        /// 设置显示持续时间
        /// </summary>
        /// <param name="duration">持续时间（秒）</param>
        public void SetDisplayDuration(float duration)
        {
            _displayDuration = Mathf.Max(0.01f, duration);
        }
        
        /// <summary>
        /// 设置动画选项
        /// </summary>
        /// <param name="enableScale">是否启用缩放动画</param>
        /// <param name="enableFade">是否启用淡出动画</param>
        public void SetAnimationOptions(bool enableScale, bool enableFade)
        {
            _enableScaleAnimation = enableScale;
            _enableFadeAnimation = enableFade;
            
            // 如果需要淡出动画但没有CanvasGroup，则添加
            if (_enableFadeAnimation && _hitMarker != null && _canvasGroup == null)
            {
                _canvasGroup = _hitMarker.AddComponent<CanvasGroup>();
            }
        }
        
        /// <summary>
        /// 设置动画曲线
        /// </summary>
        /// <param name="scaleCurve">缩放动画曲线</param>
        /// <param name="fadeCurve">淡出动画曲线</param>
        public void SetAnimationCurves(AnimationCurve scaleCurve, AnimationCurve fadeCurve)
        {
            if (scaleCurve != null)
            {
                _scaleCurve = scaleCurve;
            }
            
            if (fadeCurve != null)
            {
                _fadeCurve = fadeCurve;
            }
        }
        
        /// <summary>
        /// 手动触发命中标记（用于测试或特殊情况）
        /// </summary>
        public void TriggerHitMarker()
        {
            ShowHitMarker();
            LogUI("手动触发命中标记");
        }
        
        #endregion
    }
}
