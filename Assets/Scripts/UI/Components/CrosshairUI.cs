using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using DWHITE.Weapons;

namespace DWHITE.UI
{
    /// <summary>
    /// 十字准星UI组件
    /// 负责瞄准镜显示和射击动画效果
    /// </summary>
    public class CrosshairUI : UIElementBase
    {
        [Header("十字准星组件")]
        [SerializeField] private Image _crosshair;
        
        [Header("动画设置")]
        [SerializeField] private bool _enableAnimations = true;
        [SerializeField] private float _fireAnimationDuration = 0.1f;
        [SerializeField] private float _misfireAnimationDuration = 0.3f;
        [SerializeField] private float _fireScaleMultiplier = 0.8f;
        [SerializeField] private Vector2 _misfireShakeIntensity = new Vector2(2f, 2f);
        
        [Header("颜色设置")]
        [SerializeField] private Color _normalColor = Color.white;
        [SerializeField] private Color _fireColor = Color.yellow;
        [SerializeField] private Color _misfireColor = Color.red;
        
        // 原始状态
        private Vector3 _originalScale;
        private Color _originalColor;
        private Vector3 _originalPosition;
        
        // 动画协程
        private Coroutine _fireAnimationCoroutine;
        private Coroutine _misfireAnimationCoroutine;
        
        #region 事件订阅
        
        protected override void SubscribeToEvents()
        {
            if (_statusManager == null) return;
            
            // 监听武器发射相关事件
            SafeSubscribe("PlayerWeaponController.OnFireAttempt", () => 
                PlayerWeaponController.OnFireAttempt += OnFireAttempt);
            
            SafeSubscribe("WeaponBase.OnWeaponFired", () => 
                WeaponBase.OnWeaponFired += OnWeaponFired);
        }
        
        protected override void UnsubscribeFromEvents()
        {
            SafeUnsubscribe("PlayerWeaponController.OnFireAttempt", () => 
                PlayerWeaponController.OnFireAttempt -= OnFireAttempt);
            
            SafeUnsubscribe("WeaponBase.OnWeaponFired", () => 
                WeaponBase.OnWeaponFired -= OnWeaponFired);
        }
        
        #endregion
        
        #region 初始化和刷新
        
        protected override void OnInitialize()
        {
            if (_crosshair == null)
            {
                LogUI("错误: 未设置十字准星Image组件");
                return;
            }
            
            // 保存原始状态
            _originalScale = _crosshair.transform.localScale;
            _originalColor = _crosshair.color;
            _originalPosition = _crosshair.transform.localPosition;
            
            // 应用正常颜色
            _crosshair.color = _normalColor;
            
            LogUI("十字准星UI初始化完成");
        }
        
        protected override void OnRefreshUI()
        {
            if (_crosshair == null) return;
            
            // 重置为原始状态
            _crosshair.transform.localScale = _originalScale;
            _crosshair.color = _normalColor;
            _crosshair.transform.localPosition = _originalPosition;
        }
        
        protected override void OnCleanup()
        {
            // 停止所有动画协程
            StopAllAnimations();
        }
        
        #endregion
        
        #region 事件处理
        
        private void OnFireAttempt(PlayerWeaponController controller, bool success)
        {
            if (!_enableAnimations || _crosshair == null) return;
            
            // 检查是否是当前玩家的武器控制器
            if (controller.transform.root != _statusManager.transform) return;
            
            if (success)
            {
                PlayFireAnimation();
            }
            else
            {
                PlayMisfireAnimation();
            }
        }
        
        private void OnWeaponFired(WeaponBase weapon)
        {
            if (!_enableAnimations || _crosshair == null) return;
            
            // 检查是否是当前玩家的武器
            if (weapon.transform.root != _statusManager.transform) return;
            
            PlayFireAnimation();
        }
        
        #endregion
        
        #region 动画逻辑
        
        /// <summary>
        /// 播放射击动画
        /// </summary>
        private void PlayFireAnimation()
        {
            StopFireAnimation();
            _fireAnimationCoroutine = StartCoroutine(FireAnimationCoroutine());
        }
        
        /// <summary>
        /// 播放哑火动画
        /// </summary>
        private void PlayMisfireAnimation()
        {
            StopMisfireAnimation();
            _misfireAnimationCoroutine = StartCoroutine(MisfireAnimationCoroutine());
        }
        
        /// <summary>
        /// 射击动画协程
        /// </summary>
        private IEnumerator FireAnimationCoroutine()
        {
            Vector3 targetScale = _originalScale * _fireScaleMultiplier;
            float halfDuration = _fireAnimationDuration * 0.5f;
            
            // 缩小阶段
            float elapsed = 0f;
            while (elapsed < halfDuration)
            {
                elapsed += Time.deltaTime;
                float progress = elapsed / halfDuration;
                
                _crosshair.transform.localScale = Vector3.Lerp(_originalScale, targetScale, progress);
                _crosshair.color = Color.Lerp(_normalColor, _fireColor, progress);
                
                yield return null;
            }
            
            // 恢复阶段
            elapsed = 0f;
            while (elapsed < halfDuration)
            {
                elapsed += Time.deltaTime;
                float progress = elapsed / halfDuration;
                
                _crosshair.transform.localScale = Vector3.Lerp(targetScale, _originalScale, progress);
                _crosshair.color = Color.Lerp(_fireColor, _normalColor, progress);
                
                yield return null;
            }
            
            // 确保完全恢复
            _crosshair.transform.localScale = _originalScale;
            _crosshair.color = _normalColor;
            
            _fireAnimationCoroutine = null;
        }
        
        /// <summary>
        /// 哑火动画协程
        /// </summary>
        private IEnumerator MisfireAnimationCoroutine()
        {
            float elapsed = 0f;
            
            while (elapsed < _misfireAnimationDuration)
            {
                elapsed += Time.deltaTime;
                float progress = elapsed / _misfireAnimationDuration;
                
                // 颜色变化
                _crosshair.color = Color.Lerp(_normalColor, _misfireColor, Mathf.PingPong(progress * 4, 1f));
                
                // 抖动效果
                Vector2 shake = new Vector2(
                    Random.Range(-_misfireShakeIntensity.x, _misfireShakeIntensity.x),
                    Random.Range(-_misfireShakeIntensity.y, _misfireShakeIntensity.y)
                );
                _crosshair.transform.localPosition = _originalPosition + (Vector3)shake;
                
                yield return null;
            }
            
            // 恢复原状
            _crosshair.color = _normalColor;
            _crosshair.transform.localPosition = _originalPosition;
            
            _misfireAnimationCoroutine = null;
        }
        
        /// <summary>
        /// 停止射击动画
        /// </summary>
        private void StopFireAnimation()
        {
            if (_fireAnimationCoroutine != null)
            {
                StopCoroutine(_fireAnimationCoroutine);
                _fireAnimationCoroutine = null;
            }
        }
        
        /// <summary>
        /// 停止哑火动画
        /// </summary>
        private void StopMisfireAnimation()
        {
            if (_misfireAnimationCoroutine != null)
            {
                StopCoroutine(_misfireAnimationCoroutine);
                _misfireAnimationCoroutine = null;
            }
        }
        
        /// <summary>
        /// 停止所有动画
        /// </summary>
        private void StopAllAnimations()
        {
            StopFireAnimation();
            StopMisfireAnimation();
            
            // 恢复原始状态
            if (_crosshair != null)
            {
                _crosshair.transform.localScale = _originalScale;
                _crosshair.color = _normalColor;
                _crosshair.transform.localPosition = _originalPosition;
            }
        }
        
        #endregion
        
        #region 公共接口
        
        /// <summary>
        /// 设置是否启用动画
        /// </summary>
        /// <param name="enabled">是否启用</param>
        public void SetAnimationsEnabled(bool enabled)
        {
            _enableAnimations = enabled;
            
            if (!enabled)
            {
                StopAllAnimations();
            }
        }
        
        /// <summary>
        /// 设置动画颜色
        /// </summary>
        /// <param name="normal">正常颜色</param>
        /// <param name="fire">射击颜色</param>
        /// <param name="misfire">哑火颜色</param>
        public void SetColors(Color normal, Color fire, Color misfire)
        {
            _normalColor = normal;
            _fireColor = fire;
            _misfireColor = misfire;
            
            if (_crosshair != null)
            {
                _crosshair.color = _normalColor;
            }
        }
        
        /// <summary>
        /// 设置动画参数
        /// </summary>
        /// <param name="fireDuration">射击动画时长</param>
        /// <param name="misfireDuration">哑火动画时长</param>
        /// <param name="scaleMultiplier">缩放倍数</param>
        public void SetAnimationParameters(float fireDuration, float misfireDuration, float scaleMultiplier)
        {
            _fireAnimationDuration = Mathf.Max(0.01f, fireDuration);
            _misfireAnimationDuration = Mathf.Max(0.01f, misfireDuration);
            _fireScaleMultiplier = Mathf.Max(0.1f, scaleMultiplier);
        }
        
        #endregion
    }
}
