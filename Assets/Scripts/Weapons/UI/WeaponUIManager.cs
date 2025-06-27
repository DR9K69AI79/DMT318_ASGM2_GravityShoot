using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System;

namespace DWHITE.Weapons.UI
{
    /// <summary>
    /// 武器UI管理器
    /// 处理武器切换、弹药显示、瞄准界面等UI功能
    /// </summary>
    public class WeaponUIManager : MonoBehaviour
    {
        [Header("UI引用")]
        [SerializeField] private Canvas _weaponUICanvas;
        [SerializeField] private GameObject _weaponHUDPanel;
        [SerializeField] private GameObject _weaponSwitchPanel;
        
        [Header("弹药显示")]
        [SerializeField] private Text _ammoCountText;
        [SerializeField] private Text _weaponNameText;
        [SerializeField] private Image _weaponIcon;
        [SerializeField] private Slider _reloadProgressBar;
        
        [Header("瞄准界面")]
        [SerializeField] private Image _crosshair;
        [SerializeField] private GameObject _hitMarker;
        [SerializeField] private float _hitMarkerDuration = 0.2f;
        
        [Header("武器切换")]
        [SerializeField] private Transform _weaponSlotsParent;
        [SerializeField] private GameObject _weaponSlotPrefab;
        [SerializeField] private float _weaponSwitchDisplayTime = 2f;
        
        [Header("动画设置")]
        [SerializeField] private bool _enableAnimations = true;
        [SerializeField] private float _animationSpeed = 2f;
        
        [Header("调试")]
        [SerializeField] private bool _showDebugInfo = false;
        
        // 组件引用
        private PlayerWeaponController _weaponController;
        
        // UI状态
        private List<WeaponSlotUI> _weaponSlots = new List<WeaponSlotUI>();
        private Coroutine _hideWeaponSwitchCoroutine;
        private Coroutine _hideHitMarkerCoroutine;
        private bool _isWeaponSwitchVisible = false;
        
        // 动画相关
        private Vector3 _originalCrosshairScale;
        private Color _originalCrosshairColor;
        
        #region Unity 生命周期
        
        private void Awake()
        {
            InitializeUIManager();
        }
        
        private void Start()
        {
            SetupWeaponController();
            CreateWeaponSlots();
            InitializeUI();
        }
        
        private void Update()
        {
            UpdateUI();
            HandleInput();
        }
        
        private void OnDestroy()
        {
            UnsubscribeFromEvents();
        }
        
        #endregion
        
        #region 初始化
        
        private void InitializeUIManager()
        {
            // 确保有UI画布
            if (_weaponUICanvas == null)
            {
                _weaponUICanvas = FindObjectOfType<Canvas>();
            }
            
            // 保存瞄准镜原始状态
            if (_crosshair != null)
            {
                _originalCrosshairScale = _crosshair.transform.localScale;
                _originalCrosshairColor = _crosshair.color;
            }
            
            // 初始状态设置
            if (_weaponSwitchPanel != null)
            {
                _weaponSwitchPanel.SetActive(false);
            }
            
            if (_hitMarker != null)
            {
                _hitMarker.SetActive(false);
            }
            
            LogUI("武器UI管理器已初始化");
        }
        
        private void SetupWeaponController()
        {
            _weaponController = FindObjectOfType<PlayerWeaponController>();
            if (_weaponController == null)
            {
                LogUI("警告: 未找到PlayerWeaponController组件");
                return;
            }
            
            // 订阅武器事件
            SubscribeToEvents();
        }
        
        private void CreateWeaponSlots()
        {
            if (_weaponController == null || _weaponSlotsParent == null || _weaponSlotPrefab == null)
                return;
            
            // 清理现有槽位
            foreach (Transform child in _weaponSlotsParent)
            {
                Destroy(child.gameObject);
            }
            _weaponSlots.Clear();
            
            // 创建武器槽位
            for (int i = 0; i < _weaponController.WeaponCount; i++)
            {
                GameObject slotObj = Instantiate(_weaponSlotPrefab, _weaponSlotsParent);
                WeaponSlotUI slotUI = slotObj.GetComponent<WeaponSlotUI>();
                
                if (slotUI == null)
                {
                    slotUI = slotObj.AddComponent<WeaponSlotUI>();
                }
                
                slotUI.Initialize(i, _weaponController);
                _weaponSlots.Add(slotUI);
            }
            
            LogUI($"创建了 {_weaponSlots.Count} 个武器槽位");
        }
        
        private void InitializeUI()
        {
            // 初始化UI显示
            UpdateWeaponInfo();
            UpdateAmmoDisplay();
            UpdateWeaponSlots();
        }
        
        #endregion
        
        #region 事件订阅
        
        private void SubscribeToEvents()
        {
            if (_weaponController == null) return;
            
            PlayerWeaponController.OnWeaponSwitched += OnWeaponSwitched;
            PlayerWeaponController.OnFireAttempt += OnFireAttempt;
            WeaponBase.OnWeaponFired += OnWeaponFired;
            WeaponBase.OnReloadStarted += OnReloadStarted;
            WeaponBase.OnReloadCompleted += OnReloadCompleted;
            ProjectileBase.OnProjectileHit += OnProjectileHit;
        }
        
        private void UnsubscribeFromEvents()
        {
            PlayerWeaponController.OnWeaponSwitched -= OnWeaponSwitched;
            PlayerWeaponController.OnFireAttempt -= OnFireAttempt;
            WeaponBase.OnWeaponFired -= OnWeaponFired;
            WeaponBase.OnReloadStarted -= OnReloadStarted;
            WeaponBase.OnReloadCompleted -= OnReloadCompleted;
            ProjectileBase.OnProjectileHit -= OnProjectileHit;
        }
        
        #endregion
        
        #region 事件处理
        
        private void OnWeaponSwitched(PlayerWeaponController controller, WeaponBase weapon)
        {
            if (controller != _weaponController) return;
            
            UpdateWeaponInfo();
            UpdateAmmoDisplay();
            UpdateWeaponSlots();
            ShowWeaponSwitchPanel();
            
            LogUI($"武器切换UI更新: {weapon?.WeaponData?.WeaponName}");
        }
        
        private void OnFireAttempt(PlayerWeaponController controller, bool success)
        {
            if (controller != _weaponController) return;
            
            if (success)
            {
                AnimateCrosshairFire();
            }
            else
            {
                AnimateCrosshairMisfire();
            }
        }
        
        private void OnWeaponFired(WeaponBase weapon)
        {
            // 更新弹药显示
            UpdateAmmoDisplay();
            
            // 播放射击动画
            if (_enableAnimations)
            {
                AnimateCrosshairFire();
            }
        }
        
        private void OnReloadStarted(WeaponBase weapon)
        {
            if (_reloadProgressBar != null)
            {
                _reloadProgressBar.gameObject.SetActive(true);
                _reloadProgressBar.value = 0f;
            }
        }
        
        private void OnReloadCompleted(WeaponBase weapon)
        {
            if (_reloadProgressBar != null)
            {
                _reloadProgressBar.gameObject.SetActive(false);
            }
            
            UpdateAmmoDisplay();
        }
        
        private void OnProjectileHit(ProjectileBase projectile, ProjectileHitInfo hitInfo)
        {
            // 显示命中标记
            ShowHitMarker();
        }
        
        #endregion
        
        #region UI更新
        
        private void UpdateUI()
        {
            UpdateAmmoDisplay();
            UpdateReloadProgress();
        }
        
        private void UpdateWeaponInfo()
        {
            if (_weaponController?.CurrentWeapon?.WeaponData == null) return;
            
            var weaponData = _weaponController.CurrentWeapon.WeaponData;
            
            // 更新武器名称
            if (_weaponNameText != null)
            {
                _weaponNameText.text = weaponData.WeaponName;
            }
            
            // 更新武器图标
            if (_weaponIcon != null && weaponData.WeaponIcon != null)
            {
                _weaponIcon.sprite = weaponData.WeaponIcon;
            }
        }
        
        private void UpdateAmmoDisplay()
        {
            if (_weaponController?.CurrentWeapon == null || _ammoCountText == null) return;
            
            var weapon = _weaponController.CurrentWeapon;
            var weaponData = weapon.WeaponData;
            
            string ammoText;
            if (weaponData.MagazineSize <= 0)
            {
                // 无限弹药
                ammoText = "∞";
            }
            else
            {
                // 显示当前弹药/总弹药
                ammoText = $"{weapon.CurrentAmmo}/{weaponData.MagazineSize}";
            }
            
            _ammoCountText.text = ammoText;
            
            // 根据弹药量改变颜色
            if (weaponData.MagazineSize > 0)
            {
                float ammoRatio = (float)weapon.CurrentAmmo / weaponData.MagazineSize;
                if (ammoRatio < 0.25f)
                {
                    _ammoCountText.color = Color.red;
                }
                else if (ammoRatio < 0.5f)
                {
                    _ammoCountText.color = Color.yellow;
                }
                else
                {
                    _ammoCountText.color = Color.white;
                }
            }
        }
        
        private void UpdateReloadProgress()
        {
            if (_weaponController?.CurrentWeapon == null || _reloadProgressBar == null) return;
            
            var weapon = _weaponController.CurrentWeapon;
            if (weapon.IsReloading)
            {
                float progress = weapon.ReloadProgress;
                _reloadProgressBar.value = progress;
            }
        }
        
        private void UpdateWeaponSlots()
        {
            for (int i = 0; i < _weaponSlots.Count; i++)
            {
                bool isSelected = i == _weaponController.CurrentWeaponIndex;
                _weaponSlots[i].SetSelected(isSelected);
            }
        }
        
        #endregion
        
        #region 武器切换面板
        
        private void ShowWeaponSwitchPanel()
        {
            if (_weaponSwitchPanel == null) return;
            
            _weaponSwitchPanel.SetActive(true);
            _isWeaponSwitchVisible = true;
            
            // 取消之前的隐藏协程
            if (_hideWeaponSwitchCoroutine != null)
            {
                StopCoroutine(_hideWeaponSwitchCoroutine);
            }
            
            // 启动新的隐藏协程
            _hideWeaponSwitchCoroutine = StartCoroutine(HideWeaponSwitchPanelDelayed());
        }
        
        private System.Collections.IEnumerator HideWeaponSwitchPanelDelayed()
        {
            yield return new WaitForSeconds(_weaponSwitchDisplayTime);
            
            if (_weaponSwitchPanel != null)
            {
                _weaponSwitchPanel.SetActive(false);
                _isWeaponSwitchVisible = false;
            }
        }
        
        #endregion
        
        #region 瞄准界面动画
        
        private void AnimateCrosshairFire()
        {
            if (_crosshair == null || !_enableAnimations) return;
            
            StartCoroutine(CrosshairFireAnimation());
        }
        
        private void AnimateCrosshairMisfire()
        {
            if (_crosshair == null || !_enableAnimations) return;
            
            StartCoroutine(CrosshairMisfireAnimation());
        }
        
        private System.Collections.IEnumerator CrosshairFireAnimation()
        {
            // 射击时瞄准镜缩小并闪烁
            Vector3 targetScale = _originalCrosshairScale * 0.8f;
            Color targetColor = Color.yellow;
            
            float elapsed = 0f;
            float duration = 0.1f;
            
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float progress = elapsed / duration;
                
                _crosshair.transform.localScale = Vector3.Lerp(_originalCrosshairScale, targetScale, progress);
                _crosshair.color = Color.Lerp(_originalCrosshairColor, targetColor, progress);
                
                yield return null;
            }
            
            // 恢复原状
            elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float progress = elapsed / duration;
                
                _crosshair.transform.localScale = Vector3.Lerp(targetScale, _originalCrosshairScale, progress);
                _crosshair.color = Color.Lerp(targetColor, _originalCrosshairColor, progress);
                
                yield return null;
            }
            
            _crosshair.transform.localScale = _originalCrosshairScale;
            _crosshair.color = _originalCrosshairColor;
        }
        
        private System.Collections.IEnumerator CrosshairMisfireAnimation()
        {
            // 哑火时瞄准镜变红并抖动
            Color targetColor = Color.red;
            float duration = 0.3f;
            float elapsed = 0f;
            
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float progress = elapsed / duration;
                
                _crosshair.color = Color.Lerp(_originalCrosshairColor, targetColor, Mathf.PingPong(progress * 4, 1f));
                
                // 轻微抖动
                Vector3 shake = UnityEngine.Random.insideUnitCircle * 2f;
                _crosshair.transform.localPosition = shake;
                
                yield return null;
            }
            
            _crosshair.color = _originalCrosshairColor;
            _crosshair.transform.localPosition = Vector3.zero;
        }
        
        #endregion
        
        #region 命中标记
        
        private void ShowHitMarker()
        {
            if (_hitMarker == null) return;
            
            _hitMarker.SetActive(true);
            
            // 取消之前的隐藏协程
            if (_hideHitMarkerCoroutine != null)
            {
                StopCoroutine(_hideHitMarkerCoroutine);
            }
            
            // 启动新的隐藏协程
            _hideHitMarkerCoroutine = StartCoroutine(HideHitMarkerDelayed());
        }
        
        private System.Collections.IEnumerator HideHitMarkerDelayed()
        {
            yield return new WaitForSeconds(_hitMarkerDuration);
            
            if (_hitMarker != null)
            {
                _hitMarker.SetActive(false);
            }
        }
        
        #endregion
        
        #region 输入处理
        
        private void HandleInput()
        {
            // 处理武器切换面板的显示/隐藏
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                ToggleWeaponSwitchPanel();
            }
        }
        
        private void ToggleWeaponSwitchPanel()
        {
            if (_weaponSwitchPanel == null) return;
            
            _isWeaponSwitchVisible = !_isWeaponSwitchVisible;
            _weaponSwitchPanel.SetActive(_isWeaponSwitchVisible);
            
            if (_isWeaponSwitchVisible)
            {
                // 如果显示，取消自动隐藏
                if (_hideWeaponSwitchCoroutine != null)
                {
                    StopCoroutine(_hideWeaponSwitchCoroutine);
                }
            }
        }
        
        #endregion
        
        #region 公共接口
        
        /// <summary>
        /// 设置UI可见性
        /// </summary>
        public void SetUIVisible(bool visible)
        {
            if (_weaponHUDPanel != null)
            {
                _weaponHUDPanel.SetActive(visible);
            }
        }
        
        /// <summary>
        /// 设置瞄准镜可见性
        /// </summary>
        public void SetCrosshairVisible(bool visible)
        {
            if (_crosshair != null)
            {
                _crosshair.gameObject.SetActive(visible);
            }
        }
        
        /// <summary>
        /// 强制更新所有UI
        /// </summary>
        public void RefreshUI()
        {
            UpdateWeaponInfo();
            UpdateAmmoDisplay();
            UpdateWeaponSlots();
        }
        
        #endregion
        
        #region 调试
        
        private void LogUI(string message)
        {
            if (_showDebugInfo)
            {
                Debug.Log($"[武器UI] {message}");
            }
        }
        
        #endregion
    }
    
    /// <summary>
    /// 武器槽位UI组件
    /// </summary>
    public class WeaponSlotUI : MonoBehaviour
    {
        [SerializeField] private Image _weaponIcon;
        [SerializeField] private Text _weaponName;
        [SerializeField] private Text _slotNumber;
        [SerializeField] private GameObject _selectedIndicator;
        
        private int _slotIndex;
        private PlayerWeaponController _weaponController;
        
        public void Initialize(int slotIndex, PlayerWeaponController weaponController)
        {
            _slotIndex = slotIndex;
            _weaponController = weaponController;
            
            UpdateSlotInfo();
        }
          private void UpdateSlotInfo()
        {
            if (_weaponController == null || _slotIndex >= _weaponController.WeaponCount) return;
            
            // 获取武器信息
            var weapon = _weaponController.GetWeaponAtIndex(_slotIndex);
            
            // 更新槽位号
            if (_slotNumber != null)
            {
                _slotNumber.text = (_slotIndex + 1).ToString();
            }
            
            // 更新武器图标和名称
            if (weapon?.WeaponData != null)
            {
                if (_weaponIcon != null && weapon.WeaponData.WeaponIcon != null)
                {
                    _weaponIcon.sprite = weapon.WeaponData.WeaponIcon;
                    _weaponIcon.gameObject.SetActive(true);
                }
                
                if (_weaponName != null)
                {
                    _weaponName.text = weapon.WeaponData.WeaponName;
                }
            }
            else
            {
                // 没有武器时隐藏图标
                if (_weaponIcon != null)
                {
                    _weaponIcon.gameObject.SetActive(false);
                }
                
                if (_weaponName != null)
                {
                    _weaponName.text = "Empty";
                }
            }
        }
        
        public void SetSelected(bool selected)
        {
            if (_selectedIndicator != null)
            {
                _selectedIndicator.SetActive(selected);
            }
        }
    }
}
