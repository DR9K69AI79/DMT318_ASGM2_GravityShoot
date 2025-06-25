using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Collections;
using DWHITE.Weapons;

namespace DWHITE.UI
{
    /// <summary>
    /// 武器切换UI组件
    /// 负责显示武器切换面板和武器槽位
    /// </summary>
    public class WeaponSwitchUI : UIElementBase
    {
        [Header("武器切换面板")]
        [SerializeField] private GameObject _weaponSwitchPanel;
        [SerializeField] private Transform _weaponSlotsParent;
        [SerializeField] private GameObject _weaponSlotPrefab;
        
        [Header("显示设置")]
        [SerializeField] private float _autoHideDelay = 2f;
        [SerializeField] private bool _showOnWeaponChange = true;
        [SerializeField] private KeyCode _toggleKey = KeyCode.Tab;
        
        // 武器槽位管理
        private List<WeaponSlotUI> _weaponSlots = new List<WeaponSlotUI>();
        private Coroutine _autoHideCoroutine;
        private bool _isManuallyVisible = false;
        
        #region 事件订阅
        
        protected override void SubscribeToEvents()
        {
            if (_statusManager == null) return;
            
            SafeSubscribe("OnWeaponChanged", () => 
                _statusManager.OnWeaponChanged += OnWeaponChanged);
        }
        
        protected override void UnsubscribeFromEvents()
        {
            if (_statusManager == null) return;
            
            SafeUnsubscribe("OnWeaponChanged", () => 
                _statusManager.OnWeaponChanged -= OnWeaponChanged);
        }
        
        #endregion
        
        #region Unity 生命周期
        
        private void Update()
        {
            if (!_isInitialized) return;
            
            // 处理手动切换输入
            if (Input.GetKeyDown(_toggleKey))
            {
                TogglePanel();
            }
        }
        
        #endregion
        
        #region 初始化和刷新
        
        protected override void OnInitialize()
        {
            if (_weaponSwitchPanel == null)
            {
                LogUI("警告: 未设置武器切换面板");
                return;
            }
            
            if (_weaponSlotsParent == null)
            {
                LogUI("警告: 未设置武器槽位父对象");
                return;
            }
            
            // 初始状态隐藏面板
            _weaponSwitchPanel.SetActive(false);
            
            // 创建武器槽位
            CreateWeaponSlots();
            
            LogUI("武器切换UI初始化完成");
        }
        
        protected override void OnRefreshUI()
        {
            UpdateWeaponSlots();
        }
        
        protected override void OnCleanup()
        {
            // 停止自动隐藏协程
            if (_autoHideCoroutine != null)
            {
                StopCoroutine(_autoHideCoroutine);
                _autoHideCoroutine = null;
            }
            
            // 清理武器槽位
            ClearWeaponSlots();
        }
        
        #endregion
        
        #region 事件处理
        
        private void OnWeaponChanged(int weaponIndex)
        {
            LogUI($"武器切换到索引: {weaponIndex}");
            
            // 更新槽位显示
            UpdateWeaponSlots();
            
            // 如果启用自动显示，则显示面板
            if (_showOnWeaponChange)
            {
                ShowPanel(true);
            }
        }
        
        #endregion
        
        #region 武器槽位管理
        
        /// <summary>
        /// 创建武器槽位
        /// </summary>
        private void CreateWeaponSlots()
        {
            // 清理现有槽位
            ClearWeaponSlots();
            
            // 获取武器控制器
            var weaponController = FindObjectOfType<PlayerWeaponController>();
            if (weaponController == null)
            {
                LogUI("警告: 未找到武器控制器");
                return;
            }
            
            // 为每个武器创建槽位
            for (int i = 0; i < weaponController.WeaponCount; i++)
            {
                CreateWeaponSlot(i, weaponController);
            }
            
            LogUI($"创建了 {_weaponSlots.Count} 个武器槽位");
        }
        
        /// <summary>
        /// 创建单个武器槽位
        /// </summary>
        /// <param name="slotIndex">槽位索引</param>
        /// <param name="weaponController">武器控制器</param>
        private void CreateWeaponSlot(int slotIndex, PlayerWeaponController weaponController)
        {
            if (_weaponSlotPrefab == null)
            {
                LogUI("警告: 未设置武器槽位预制体");
                return;
            }
            
            GameObject slotObj = Instantiate(_weaponSlotPrefab, _weaponSlotsParent);
            WeaponSlotUI slotUI = slotObj.GetComponent<WeaponSlotUI>();
            
            if (slotUI == null)
            {
                slotUI = slotObj.AddComponent<WeaponSlotUI>();
            }
            
            slotUI.Initialize(slotIndex, weaponController);
            _weaponSlots.Add(slotUI);
        }
        
        /// <summary>
        /// 更新武器槽位显示
        /// </summary>
        private void UpdateWeaponSlots()
        {
            if (_statusManager == null) return;
            
            var currentState = _statusManager.CurrentState;
            int currentWeaponIndex = currentState.currentWeaponIndex;
            
            for (int i = 0; i < _weaponSlots.Count; i++)
            {
                bool isSelected = (i == currentWeaponIndex);
                _weaponSlots[i].SetSelected(isSelected);
            }
        }
        
        /// <summary>
        /// 清理武器槽位
        /// </summary>
        private void ClearWeaponSlots()
        {
            foreach (var slot in _weaponSlots)
            {
                if (slot != null && slot.gameObject != null)
                {
                    DestroyImmediate(slot.gameObject);
                }
            }
            
            _weaponSlots.Clear();
        }
        
        #endregion
        
        #region 面板显示控制
        
        /// <summary>
        /// 显示面板
        /// </summary>
        /// <param name="autoHide">是否自动隐藏</param>
        public void ShowPanel(bool autoHide = true)
        {
            if (_weaponSwitchPanel == null) return;
            
            _weaponSwitchPanel.SetActive(true);
            
            // 停止之前的自动隐藏协程
            if (_autoHideCoroutine != null)
            {
                StopCoroutine(_autoHideCoroutine);
                _autoHideCoroutine = null;
            }
            
            // 如果需要自动隐藏且不是手动显示状态
            if (autoHide && !_isManuallyVisible && _autoHideDelay > 0)
            {
                _autoHideCoroutine = StartCoroutine(AutoHideCoroutine());
            }
            
            LogUI("显示武器切换面板");
        }
        
        /// <summary>
        /// 隐藏面板
        /// </summary>
        public void HidePanel()
        {
            if (_weaponSwitchPanel == null) return;
            
            _weaponSwitchPanel.SetActive(false);
            _isManuallyVisible = false;
            
            // 停止自动隐藏协程
            if (_autoHideCoroutine != null)
            {
                StopCoroutine(_autoHideCoroutine);
                _autoHideCoroutine = null;
            }
            
            LogUI("隐藏武器切换面板");
        }
        
        /// <summary>
        /// 切换面板显示状态
        /// </summary>
        public void TogglePanel()
        {
            if (_weaponSwitchPanel == null) return;
            
            bool isActive = _weaponSwitchPanel.activeSelf;
            
            if (isActive)
            {
                HidePanel();
            }
            else
            {
                _isManuallyVisible = true;
                ShowPanel(false); // 手动显示时不自动隐藏
            }
        }
        
        /// <summary>
        /// 自动隐藏协程
        /// </summary>
        private IEnumerator AutoHideCoroutine()
        {
            yield return new WaitForSeconds(_autoHideDelay);
            
            if (!_isManuallyVisible)
            {
                HidePanel();
            }
            
            _autoHideCoroutine = null;
        }
        
        #endregion
        
        #region 公共接口
        
        /// <summary>
        /// 设置自动隐藏延迟
        /// </summary>
        /// <param name="delay">延迟时间（秒）</param>
        public void SetAutoHideDelay(float delay)
        {
            _autoHideDelay = Mathf.Max(0, delay);
        }
        
        /// <summary>
        /// 设置是否在武器变化时显示
        /// </summary>
        /// <param name="show">是否显示</param>
        public void SetShowOnWeaponChange(bool show)
        {
            _showOnWeaponChange = show;
        }
        
        /// <summary>
        /// 设置切换按键
        /// </summary>
        /// <param name="key">按键</param>
        public void SetToggleKey(KeyCode key)
        {
            _toggleKey = key;
        }
        
        /// <summary>
        /// 强制重建武器槽位
        /// </summary>
        public void RebuildWeaponSlots()
        {
            CreateWeaponSlots();
            UpdateWeaponSlots();
        }
        
        #endregion
    }
    
    /// <summary>
    /// 武器槽位UI组件
    /// </summary>
    public class WeaponSlotUI : MonoBehaviour
    {
        [Header("槽位组件")]
        [SerializeField] private Image _weaponIcon;
        [SerializeField] private Text _weaponName;
        [SerializeField] private Text _slotNumber;
        [SerializeField] private GameObject _selectedIndicator;
        
        private int _slotIndex;
        private PlayerWeaponController _weaponController;
        
        /// <summary>
        /// 初始化槽位
        /// </summary>
        /// <param name="slotIndex">槽位索引</param>
        /// <param name="weaponController">武器控制器</param>
        public void Initialize(int slotIndex, PlayerWeaponController weaponController)
        {
            _slotIndex = slotIndex;
            _weaponController = weaponController;
            
            UpdateSlotInfo();
        }
        
        /// <summary>
        /// 更新槽位信息
        /// </summary>
        private void UpdateSlotInfo()
        {
            if (_weaponController == null || _slotIndex >= _weaponController.WeaponCount) return;
            
            // 更新槽位号
            if (_slotNumber != null)
            {
                _slotNumber.text = (_slotIndex + 1).ToString();
            }
            
            // 获取武器信息
            var weapon = _weaponController.GetWeaponAtIndex(_slotIndex);
            
            if (weapon?.WeaponData != null)
            {
                // 更新武器图标
                if (_weaponIcon != null && weapon.WeaponData.WeaponIcon != null)
                {
                    _weaponIcon.sprite = weapon.WeaponData.WeaponIcon;
                    _weaponIcon.gameObject.SetActive(true);
                }
                
                // 更新武器名称
                if (_weaponName != null)
                {
                    _weaponName.text = weapon.WeaponData.WeaponName;
                }
            }
            else
            {
                // 空槽位
                if (_weaponIcon != null)
                {
                    _weaponIcon.gameObject.SetActive(false);
                }
                
                if (_weaponName != null)
                {
                    _weaponName.text = "空";
                }
            }
        }
        
        /// <summary>
        /// 设置选中状态
        /// </summary>
        /// <param name="selected">是否选中</param>
        public void SetSelected(bool selected)
        {
            if (_selectedIndicator != null)
            {
                _selectedIndicator.SetActive(selected);
            }
        }
    }
}
