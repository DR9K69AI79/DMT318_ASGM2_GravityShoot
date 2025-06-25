using UnityEngine;
using UnityEngine.UI;

namespace DWHITE.UI
{
    /// <summary>
    /// 生命值显示UI组件
    /// 负责显示玩家的生命值信息
    /// </summary>
    public class HealthBarUI : UIElementBase
    {
        [Header("生命值显示组件")]
        [SerializeField] private Slider _healthBar;
        [SerializeField] private Text _healthText;
        [SerializeField] private Image _healthBarFill;
        
        [Header("颜色设置")]
        [SerializeField] private Color _fullHealthColor = Color.green;
        [SerializeField] private Color _mediumHealthColor = Color.yellow;
        [SerializeField] private Color _lowHealthColor = Color.red;
        [SerializeField] private float _mediumHealthThreshold = 0.6f;
        [SerializeField] private float _lowHealthThreshold = 0.3f;
        
        [Header("显示设置")]
        [SerializeField] private bool _showHealthText = true;
        [SerializeField] private bool _showPercentage = false;
        [SerializeField] private bool _smoothTransition = true;
        [SerializeField] private float _transitionSpeed = 2f;
        
        // 当前显示状态
        private float _displayedHealth;
        private float _targetHealth;
        
        #region 事件订阅
        
        protected override void SubscribeToEvents()
        {
            if (_statusManager == null) return;
            
            SafeSubscribe("OnHealthChanged", () => 
                _statusManager.OnHealthChanged += OnHealthChanged);
        }
        
        protected override void UnsubscribeFromEvents()
        {
            if (_statusManager == null) return;
            
            SafeUnsubscribe("OnHealthChanged", () => 
                _statusManager.OnHealthChanged -= OnHealthChanged);
        }
        
        #endregion
        
        #region Unity 生命周期
        
        private void Update()
        {
            if (!_isInitialized || !_smoothTransition) return;
            
            // 平滑血量变化
            if (Mathf.Abs(_displayedHealth - _targetHealth) > 0.1f)
            {
                _displayedHealth = Mathf.Lerp(_displayedHealth, _targetHealth, Time.deltaTime * _transitionSpeed);
                UpdateHealthDisplay(_displayedHealth);
            }
        }
        
        #endregion
        
        #region 初始化和刷新
        
        protected override void OnInitialize()
        {
            if (_healthBar == null)
            {
                LogUI("警告: 未设置生命值滑动条组件");
            }
            
            // 获取当前生命值
            var currentState = _statusManager.CurrentState;
            _displayedHealth = currentState.currentHealth;
            _targetHealth = currentState.currentHealth;
            
            // 初始显示
            UpdateHealthDisplay(currentState.currentHealth);
            
            LogUI("生命值UI初始化完成");
        }
        
        protected override void OnRefreshUI()
        {
            if (_statusManager == null) return;
            
            var currentState = _statusManager.CurrentState;
            _targetHealth = currentState.currentHealth;
            
            if (!_smoothTransition)
            {
                _displayedHealth = _targetHealth;
                UpdateHealthDisplay(_displayedHealth);
            }
        }
        
        #endregion
        
        #region 事件处理
        
        private void OnHealthChanged(float currentHealth, float maxHealth)
        {
            LogUI($"生命值变化: {currentHealth}/{maxHealth}");
            
            _targetHealth = currentHealth;
            
            if (!_smoothTransition)
            {
                _displayedHealth = currentHealth;
                UpdateHealthDisplay(currentHealth);
            }
        }
        
        #endregion
        
        #region UI更新逻辑
        
        /// <summary>
        /// 更新生命值显示
        /// </summary>
        /// <param name="currentHealth">当前生命值</param>
        private void UpdateHealthDisplay(float currentHealth)
        {
            if (_statusManager == null) return;
            
            var currentState = _statusManager.CurrentState;
            float maxHealth = currentState.maxHealth;
            
            // 防止除零
            if (maxHealth <= 0)
            {
                LogUI("警告: 最大生命值为0");
                return;
            }
            
            float healthRatio = Mathf.Clamp01(currentHealth / maxHealth);
            
            // 更新滑动条
            if (_healthBar != null)
            {
                _healthBar.value = healthRatio;
            }
            
            // 更新文本显示
            UpdateHealthText(currentHealth, maxHealth);
            
            // 更新颜色
            UpdateHealthColor(healthRatio);
        }
        
        /// <summary>
        /// 更新生命值文本
        /// </summary>
        /// <param name="currentHealth">当前生命值</param>
        /// <param name="maxHealth">最大生命值</param>
        private void UpdateHealthText(float currentHealth, float maxHealth)
        {
            if (_healthText == null || !_showHealthText) return;
            
            string healthText;
            
            if (_showPercentage)
            {
                float percentage = (currentHealth / maxHealth) * 100f;
                healthText = $"{percentage:F0}%";
            }
            else
            {
                healthText = $"{currentHealth:F0}/{maxHealth:F0}";
            }
            
            _healthText.text = healthText;
        }
        
        /// <summary>
        /// 更新生命值颜色
        /// </summary>
        /// <param name="healthRatio">生命值比例</param>
        private void UpdateHealthColor(float healthRatio)
        {
            Color targetColor;
            
            if (healthRatio > _mediumHealthThreshold)
            {
                targetColor = _fullHealthColor;
            }
            else if (healthRatio > _lowHealthThreshold)
            {
                // 中等生命值：在绿色和黄色之间插值
                float t = (healthRatio - _lowHealthThreshold) / (_mediumHealthThreshold - _lowHealthThreshold);
                targetColor = Color.Lerp(_mediumHealthColor, _fullHealthColor, t);
            }
            else
            {
                // 低生命值：在红色和黄色之间插值
                float t = healthRatio / _lowHealthThreshold;
                targetColor = Color.Lerp(_lowHealthColor, _mediumHealthColor, t);
            }
            
            // 应用颜色
            if (_healthBarFill != null)
            {
                _healthBarFill.color = targetColor;
            }
            else if (_healthBar?.fillRect?.GetComponent<Image>() != null)
            {
                _healthBar.fillRect.GetComponent<Image>().color = targetColor;
            }
            
            // 文本颜色也可以跟随变化
            if (_healthText != null)
            {
                _healthText.color = targetColor;
            }
        }
        
        #endregion
        
        #region 公共接口
        
        /// <summary>
        /// 设置生命值颜色阈值
        /// </summary>
        /// <param name="mediumThreshold">中等生命值阈值</param>
        /// <param name="lowThreshold">低生命值阈值</param>
        public void SetHealthThresholds(float mediumThreshold, float lowThreshold)
        {
            _mediumHealthThreshold = Mathf.Clamp01(mediumThreshold);
            _lowHealthThreshold = Mathf.Clamp01(lowThreshold);
            
            // 确保阈值顺序正确
            if (_lowHealthThreshold > _mediumHealthThreshold)
            {
                _lowHealthThreshold = _mediumHealthThreshold;
            }
            
            RefreshUI();
        }
        
        /// <summary>
        /// 设置生命值颜色
        /// </summary>
        /// <param name="full">满血颜色</param>
        /// <param name="medium">中等血量颜色</param>
        /// <param name="low">低血量颜色</param>
        public void SetHealthColors(Color full, Color medium, Color low)
        {
            _fullHealthColor = full;
            _mediumHealthColor = medium;
            _lowHealthColor = low;
            
            RefreshUI();
        }
        
        /// <summary>
        /// 设置显示选项
        /// </summary>
        /// <param name="showText">是否显示文本</param>
        /// <param name="showPercentage">是否显示百分比</param>
        /// <param name="smoothTransition">是否平滑过渡</param>
        public void SetDisplayOptions(bool showText, bool showPercentage, bool smoothTransition)
        {
            _showHealthText = showText;
            _showPercentage = showPercentage;
            _smoothTransition = smoothTransition;
            
            if (_healthText != null)
            {
                _healthText.gameObject.SetActive(_showHealthText);
            }
            
            RefreshUI();
        }
        
        /// <summary>
        /// 设置过渡速度
        /// </summary>
        /// <param name="speed">过渡速度</param>
        public void SetTransitionSpeed(float speed)
        {
            _transitionSpeed = Mathf.Max(0.1f, speed);
        }
        
        #endregion
    }
}
