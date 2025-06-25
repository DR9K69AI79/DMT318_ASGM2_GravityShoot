using UnityEngine;
using DWHITE.Weapons;

namespace DWHITE.UI
{    /// <summary>
    /// 弹药系统调试工具
    /// 用于调试弹药显示问题
    /// </summary>
    public class AmmoDebugger : MonoBehaviour
    {
        [Header("调试设置")]
        [SerializeField] private KeyCode _debugKey = KeyCode.F1;
        [SerializeField] private KeyCode _toggleGUIKey = KeyCode.F2;
        [SerializeField] private bool _showDebugGUI = true;
        [SerializeField] private bool _logAmmoChanges = true;
        [SerializeField] private bool _autoRefreshGUI = true;
        [SerializeField] private float _autoRefreshInterval = 0.5f;
        
        private PlayerStatusManager _statusManager;
        private PlayerWeaponController _weaponController;
        private AmmoDisplay _ammoDisplay;
        private float _lastAutoRefreshTime;
        
        private void Start()
        {
            FindComponents();
            SubscribeToEvents();
        }
          private void Update()
        {
            if (Input.GetKeyDown(_debugKey))
            {
                DebugCurrentAmmoState();
            }
            
            if (Input.GetKeyDown(_toggleGUIKey))
            {
                _showDebugGUI = !_showDebugGUI;
                Debug.Log($"[AmmoDebugger] 调试GUI显示: {_showDebugGUI}");
            }
            
            // 自动刷新
            if (_autoRefreshGUI && Time.time - _lastAutoRefreshTime > _autoRefreshInterval)
            {
                _lastAutoRefreshTime = Time.time;
                // 触发GUI重绘不需要特殊操作，OnGUI会自动调用
            }
        }
        
        private void FindComponents()
        {
            _statusManager = FindObjectOfType<PlayerStatusManager>();
            _weaponController = FindObjectOfType<PlayerWeaponController>();
            _ammoDisplay = FindObjectOfType<AmmoDisplay>();
        }
        
        private void SubscribeToEvents()
        {
            if (_statusManager != null)
            {
                _statusManager.OnAmmoChanged += OnAmmoChanged;
                _statusManager.OnReloadStateChanged += OnReloadStateChanged;
                _statusManager.OnWeaponChanged += OnWeaponChanged;
            }
            
            if (_weaponController != null)
            {
                WeaponBase.OnAmmoChanged += OnWeaponAmmoChanged;
                WeaponBase.OnReloadCompleted += OnWeaponReloadCompleted;
                WeaponBase.OnWeaponFired += OnWeaponFired;
            }
        }
        
        private void OnDestroy()
        {
            if (_statusManager != null)
            {
                _statusManager.OnAmmoChanged -= OnAmmoChanged;
                _statusManager.OnReloadStateChanged -= OnReloadStateChanged;
                _statusManager.OnWeaponChanged -= OnWeaponChanged;
            }
            
            WeaponBase.OnAmmoChanged -= OnWeaponAmmoChanged;
            WeaponBase.OnReloadCompleted -= OnWeaponReloadCompleted;
            WeaponBase.OnWeaponFired -= OnWeaponFired;
        }
        
        private void OnAmmoChanged(int currentAmmo, int maxAmmo)
        {
            if (_logAmmoChanges)
            {
                Debug.Log($"[AmmoDebugger] StatusManager.OnAmmoChanged: {currentAmmo}/{maxAmmo}");
                DebugCurrentAmmoState();
            }
        }
        
        private void OnReloadStateChanged(bool isReloading)
        {
            if (_logAmmoChanges)
            {
                Debug.Log($"[AmmoDebugger] ReloadStateChanged: {isReloading}");
                if (!isReloading)
                {
                    // 换弹完成，延迟一帧检查状态
                    StartCoroutine(DelayedAmmoCheck());
                }
            }
        }
        
        private void OnWeaponChanged(int weaponIndex)
        {
            if (_logAmmoChanges)
            {
                Debug.Log($"[AmmoDebugger] WeaponChanged: {weaponIndex}");
                DebugCurrentAmmoState();
            }
        }
        
        private void OnWeaponAmmoChanged(WeaponBase weapon, int currentAmmo, int maxAmmo)
        {
            if (_logAmmoChanges)
            {
                Debug.Log($"[AmmoDebugger] WeaponBase.OnAmmoChanged: {weapon.name} {currentAmmo}/{maxAmmo}");
            }
        }
        
        private void OnWeaponReloadCompleted(WeaponBase weapon)
        {
            if (_logAmmoChanges)
            {
                Debug.Log($"[AmmoDebugger] WeaponBase.OnReloadCompleted: {weapon.name}");
                StartCoroutine(DelayedAmmoCheck());
            }
        }
        
        private void OnWeaponFired(WeaponBase weapon)
        {
            if (_logAmmoChanges)
            {
                Debug.Log($"[AmmoDebugger] WeaponBase.OnWeaponFired: {weapon.name}");
                DebugCurrentAmmoState();
            }
        }
        
        private System.Collections.IEnumerator DelayedAmmoCheck()
        {
            yield return null; // 等待一帧
            DebugCurrentAmmoState();
        }
        
        private void DebugCurrentAmmoState()
        {
            if (_weaponController?.CurrentWeapon == null)
            {
                Debug.Log("[AmmoDebugger] 无当前武器");
                return;
            }
            
            var weapon = _weaponController.CurrentWeapon;
            var statusState = _statusManager?.CurrentState;
            
            Debug.Log("=== 弹药状态调试 ===");
            Debug.Log($"武器名称: {weapon.WeaponData?.WeaponName ?? "Unknown"}");
            Debug.Log($"武器CurrentAmmo: {weapon.CurrentAmmo}");
            Debug.Log($"武器MaxAmmo: {weapon.MaxAmmo}");
            Debug.Log($"武器弹匣大小: {weapon.WeaponData?.MagazineSize ?? 0}");
            Debug.Log($"武器是否换弹中: {weapon.IsReloading}");
            
            if (statusState.HasValue)
            {
                Debug.Log($"StatusManager currentAmmo: {statusState.Value.currentAmmo}");
                Debug.Log($"StatusManager maxAmmo: {statusState.Value.maxAmmo}");
                Debug.Log($"StatusManager isReloading: {statusState.Value.isReloading}");
            }
            
            Debug.Log("===================");
        }
        
        private void OnGUI()
        {
            if (!_showDebugGUI || !Application.isPlaying) return;
            
            GUILayout.BeginArea(new Rect(10, 300, 400, 300));
            GUILayout.Label("弹药调试工具", new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold });
            
            if (_weaponController?.CurrentWeapon != null)
            {
                var weapon = _weaponController.CurrentWeapon;
                var statusState = _statusManager?.CurrentState;
                
                GUILayout.Label($"武器: {weapon.WeaponData?.WeaponName ?? "Unknown"}");
                GUILayout.Label($"武器弹药: {weapon.CurrentAmmo}/{weapon.MaxAmmo}");
                GUILayout.Label($"弹匣大小: {weapon.WeaponData?.MagazineSize ?? 0}");
                GUILayout.Label($"是否换弹: {weapon.IsReloading}");
                
                if (statusState.HasValue)
                {
                    GUILayout.Label($"状态弹药: {statusState.Value.currentAmmo}/{statusState.Value.maxAmmo}");
                    GUILayout.Label($"状态换弹: {statusState.Value.isReloading}");
                }
                  if (GUILayout.Button("射击一发"))
                {
                    if (weapon.CanFire)
                    {
                        weapon.TryFire(_weaponController.CurrentAimDirection);
                    }
                }
                
                if (GUILayout.Button("开始换弹"))
                {
                    if (!weapon.IsReloading)
                    {
                        weapon.TryReload();
                    }
                }
            }
            else
            {
                GUILayout.Label("无当前武器");
            }
            
            GUILayout.Label($"按 {_debugKey} 键打印详细状态");
            
            GUILayout.EndArea();
        }
    }
}
