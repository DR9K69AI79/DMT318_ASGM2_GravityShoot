using UnityEngine;
using UnityEngine.UI;
using DWHITE.Weapons;

namespace DWHITE.UI
{
    /// <summary>
    /// 弹药显示UI组件
    /// 负责显示当前武器的弹药信息
    /// </summary>
    public class AmmoDisplay : UIElementBase
    {
        [Header("弹药显示组件")]
        [SerializeField] private Text _ammoCountText;
        [SerializeField] private Text _weaponNameText;
        [SerializeField] private Image _weaponIcon;
        
        [Header("颜色设置")]
        [SerializeField] private Color _normalAmmoColor = Color.white;
        [SerializeField] private Color _lowAmmoColor = Color.yellow;
        [SerializeField] private Color _criticalAmmoColor = Color.red;
        [SerializeField] private float _lowAmmoThreshold = 0.5f;
        [SerializeField] private float _criticalAmmoThreshold = 0.25f;
        
        #region 事件订阅
          protected override void SubscribeToEvents()
        {
            if (_statusManager == null) return;
            
            SafeSubscribe("OnWeaponChanged", () => 
                _statusManager.OnWeaponChanged += OnWeaponChanged);
            
            SafeSubscribe("OnAmmoChanged", () => 
                _statusManager.OnAmmoChanged += OnAmmoChanged);
            
            SafeSubscribe("OnReloadStateChanged", () => 
                _statusManager.OnReloadStateChanged += OnReloadStateChanged);
        }
          protected override void UnsubscribeFromEvents()
        {
            if (_statusManager == null) return;
            
            SafeUnsubscribe("OnWeaponChanged", () => 
                _statusManager.OnWeaponChanged -= OnWeaponChanged);
            
            SafeUnsubscribe("OnAmmoChanged", () => 
                _statusManager.OnAmmoChanged -= OnAmmoChanged);
            
            SafeUnsubscribe("OnReloadStateChanged", () => 
                _statusManager.OnReloadStateChanged -= OnReloadStateChanged);
        }
        
        #endregion
        
        #region 初始化和刷新
        
        protected override void OnInitialize()
        {
            // 验证必要的UI组件
            if (_ammoCountText == null)
            {
                LogUI("警告: 未设置弹药数量文本组件");
            }
            
            // 初始显示
            UpdateAmmoDisplay();
            UpdateWeaponInfo();
        }
        
        protected override void OnRefreshUI()
        {
            UpdateAmmoDisplay();
            UpdateWeaponInfo();
        }
        
        #endregion
        
        #region 事件处理
        
        private void OnWeaponChanged(int weaponIndex)
        {
            LogUI($"武器切换到索引: {weaponIndex}");
            UpdateWeaponInfo();
            UpdateAmmoDisplay();
        }
          private void OnAmmoChanged(int currentAmmo, int maxAmmo)
        {
            LogUI($"弹药变化: {currentAmmo}/{maxAmmo}");
            UpdateAmmoDisplay();
        }
        
        private void OnReloadStateChanged(bool isReloading)
        {
            LogUI($"换弹状态变化: {(isReloading ? "开始换弹" : "换弹完成")}");
            if (!isReloading)
            {
                // 换弹完成，更新弹药显示
                UpdateAmmoDisplay();
            }
        }
        
        #endregion
        
        #region UI更新逻辑
          /// <summary>
        /// 更新弹药显示
        /// </summary>
        private void UpdateAmmoDisplay()
        {
            if (_ammoCountText == null || _statusManager == null) return;
            
            // 获取当前状态数据
            var currentState = _statusManager.CurrentState;
            
            // 也直接从武器控制器获取数据进行对比
            var weaponController = FindObjectOfType<PlayerWeaponController>();
            int actualCurrentAmmo = weaponController?.CurrentWeapon?.CurrentAmmo ?? 0;
            int actualMaxAmmo = weaponController?.CurrentWeapon?.MaxAmmo ?? 0;
            
            LogUI($"状态数据弹药: {currentState.currentAmmo}/{currentState.maxAmmo}");
            LogUI($"武器实际弹药: {actualCurrentAmmo}/{actualMaxAmmo}");
            
            string ammoText;
            Color ammoColor = _normalAmmoColor;
            
            // 使用实际武器数据而不是状态数据
            int displayCurrentAmmo = actualCurrentAmmo;
            int displayMaxAmmo = actualMaxAmmo;
            
            // 处理弹药显示
            if (displayMaxAmmo <= 0)
            {
                // 无限弹药
                ammoText = "∞";
                ammoColor = _normalAmmoColor;
            }
            else
            {
                // 有限弹药
                ammoText = $"{displayCurrentAmmo}/{displayMaxAmmo}";
                
                // 根据弹药比例设置颜色
                float ammoRatio = (float)displayCurrentAmmo / displayMaxAmmo;
                
                if (ammoRatio <= _criticalAmmoThreshold)
                {
                    ammoColor = _criticalAmmoColor;
                }
                else if (ammoRatio <= _lowAmmoThreshold)
                {
                    ammoColor = _lowAmmoColor;
                }
                else
                {
                    ammoColor = _normalAmmoColor;
                }
            }
            
            _ammoCountText.text = ammoText;
            _ammoCountText.color = ammoColor;
            
            LogUI($"显示弹药: {ammoText}");
        }
        
        /// <summary>
        /// 更新武器信息显示
        /// </summary>
        private void UpdateWeaponInfo()
        {
            if (_statusManager == null) return;
            
            var currentState = _statusManager.CurrentState;
            
            // 更新武器名称
            if (_weaponNameText != null)
            {
                string weaponName = string.IsNullOrEmpty(currentState.weaponName) ? "无武器" : currentState.weaponName;
                _weaponNameText.text = weaponName;
            }
            
            // 更新武器图标（这里需要额外的武器数据获取逻辑）
            UpdateWeaponIcon();
        }
        
        /// <summary>
        /// 更新武器图标
        /// </summary>
        private void UpdateWeaponIcon()
        {
            if (_weaponIcon == null || _statusManager == null) return;
            
            // 尝试从武器控制器获取武器图标
            var weaponController = FindObjectOfType<PlayerWeaponController>();
            if (weaponController?.CurrentWeapon?.WeaponData?.WeaponIcon != null)
            {
            _weaponIcon.sprite = weaponController.CurrentWeapon.WeaponData.WeaponIcon;
            _weaponIcon.gameObject.SetActive(true);
            
            // 保持高度不变，匹配宽度
            _weaponIcon.preserveAspect = true;
            _weaponIcon.type = Image.Type.Simple;
            }
            else
            {
            _weaponIcon.gameObject.SetActive(false);
            }
        }
        
        #endregion
        
        #region 公共接口
        
        /// <summary>
        /// 设置颜色阈值
        /// </summary>
        /// <param name="lowAmmoThreshold">低弹药阈值</param>
        /// <param name="criticalAmmoThreshold">危险弹药阈值</param>
        public void SetAmmoColorThresholds(float lowAmmoThreshold, float criticalAmmoThreshold)
        {
            _lowAmmoThreshold = Mathf.Clamp01(lowAmmoThreshold);
            _criticalAmmoThreshold = Mathf.Clamp01(criticalAmmoThreshold);
            
            // 确保危险阈值小于低弹药阈值
            if (_criticalAmmoThreshold > _lowAmmoThreshold)
            {
                _criticalAmmoThreshold = _lowAmmoThreshold;
            }
            
            RefreshUI();
        }
        
        /// <summary>
        /// 设置弹药颜色
        /// </summary>
        /// <param name="normal">正常颜色</param>
        /// <param name="low">低弹药颜色</param>
        /// <param name="critical">危险颜色</param>
        public void SetAmmoColors(Color normal, Color low, Color critical)
        {
            _normalAmmoColor = normal;
            _lowAmmoColor = low;
            _criticalAmmoColor = critical;
            
            RefreshUI();
        }
        
        #endregion
    }
}
