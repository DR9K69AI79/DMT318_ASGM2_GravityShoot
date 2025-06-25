using UnityEngine;
using UnityEngine.UI;
using DWHITE.Weapons;

namespace DWHITE.UI
{
    /// <summary>
    /// 轻量UI系统测试和演示脚本
    /// 用于验证UI组件的正确工作
    /// </summary>
    public class UISystemDemo : MonoBehaviour
    {
        [Header("测试控制")]
        [SerializeField] private KeyCode _testHealthChangeKey = KeyCode.H;
        [SerializeField] private KeyCode _testAmmoChangeKey = KeyCode.A;
        [SerializeField] private KeyCode _testWeaponSwitchKey = KeyCode.W;
        [SerializeField] private KeyCode _testHitMarkerKey = KeyCode.M;

        [Header("测试参数")]
        [SerializeField] private float _testHealthDamage = 20f;
        [SerializeField] private int _testAmmoDecrement = 5;

        private PlayerStatusManager _statusManager;
        private GameUIManager _uiManager;

        private void Start()
        {
            FindComponents();
        }

        private void Update()
        {
            HandleTestInputs();
        }

        /// <summary>
        /// 查找必要的组件
        /// </summary>
        private void FindComponents()
        {
            _statusManager = FindObjectOfType<PlayerStatusManager>();
            _uiManager = FindObjectOfType<GameUIManager>();

            if (_statusManager == null)
            {
                Debug.LogWarning("[UISystemDemo] 未找到PlayerStatusManager");
            }

            if (_uiManager == null)
            {
                Debug.LogWarning("[UISystemDemo] 未找到GameUIManager");
            }
            else
            {
                Debug.Log($"[UISystemDemo] UI系统状态: {_uiManager.GetSystemStatus()}");
            }
        }

        /// <summary>
        /// 处理测试输入
        /// </summary>
        private void HandleTestInputs()
        {
            if (_statusManager == null) return;

            // 测试生命值变化
            if (Input.GetKeyDown(_testHealthChangeKey))
            {
                TestHealthChange();
            }

            // 测试弹药变化
            if (Input.GetKeyDown(_testAmmoChangeKey))
            {
                TestAmmoChange();
            }

            // 测试武器切换
            if (Input.GetKeyDown(_testWeaponSwitchKey))
            {
                TestWeaponSwitch();
            }

            // 测试命中标记
            if (Input.GetKeyDown(_testHitMarkerKey))
            {
                TestHitMarker();
            }
        }

        /// <summary>
        /// 测试生命值变化
        /// </summary>
        private void TestHealthChange()
        {
            float damage = _testHealthDamage;
            _statusManager.TakeDamage(damage, Vector3.zero, Vector3.zero);
            Debug.Log($"[UISystemDemo] 测试生命值变化: -{damage}");
        }

        /// <summary>
        /// 测试弹药变化
        /// </summary>
        private void TestAmmoChange()
        {
            // 这里需要通过武器控制器来改变弹药
            var weaponController = FindObjectOfType<PlayerWeaponController>();
            if (weaponController?.CurrentWeapon != null)
            {
                var weapon = weaponController.CurrentWeapon;
                int newAmmo = Mathf.Max(0, weapon.CurrentAmmo - _testAmmoDecrement);

                // 直接设置弹药（需要确保武器有相应的方法）
                // weapon.SetAmmo(newAmmo);

                Debug.Log($"[UISystemDemo] 测试弹药变化: -{_testAmmoDecrement}");
            }
            else
            {
                Debug.LogWarning("[UISystemDemo] 未找到当前武器，无法测试弹药变化");
            }
        }

        /// <summary>
        /// 测试武器切换
        /// </summary>
        private void TestWeaponSwitch()
        {
            var weaponController = FindObjectOfType<PlayerWeaponController>();
            if (weaponController != null)
            {
                int currentIndex = weaponController.CurrentWeaponIndex;
                int nextIndex = (currentIndex + 1) % weaponController.WeaponCount;
                weaponController.SwitchToWeapon(nextIndex);

                Debug.Log($"[UISystemDemo] 测试武器切换: {currentIndex} -> {nextIndex}");
            }
            else
            {
                Debug.LogWarning("[UISystemDemo] 未找到武器控制器，无法测试武器切换");
            }
        }

        /// <summary>
        /// 测试命中标记
        /// </summary>
        private void TestHitMarker()
        {
            var hitMarkerUI = _uiManager?.GetUIElement<HitMarkerUI>();
            if (hitMarkerUI != null)
            {
                hitMarkerUI.TriggerHitMarker();
                Debug.Log("[UISystemDemo] 测试命中标记");
            }
            else
            {
                Debug.LogWarning("[UISystemDemo] 未找到命中标记UI组件");
            }
        }

        /// <summary>
        /// 在Inspector中显示帮助信息
        /// </summary>
        private void OnGUI()
        {
            if (!Application.isPlaying) return;

            GUILayout.BeginArea(new Rect(10, 10, 300, 200));
            GUILayout.Label("UI系统测试控制:", new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold });
            GUILayout.Label($"按 {_testHealthChangeKey} 键测试生命值变化");
            GUILayout.Label($"按 {_testAmmoChangeKey} 键测试弹药变化");
            GUILayout.Label($"按 {_testWeaponSwitchKey} 键测试武器切换");
            GUILayout.Label($"按 {_testHitMarkerKey} 键测试命中标记");

            if (_uiManager != null)
            {
                GUILayout.Space(10);
                GUILayout.Label("UI系统状态:", new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold });
                GUILayout.Label(_uiManager.GetSystemStatus(), new GUIStyle(GUI.skin.label) { fontSize = 10 });
            }

            GUILayout.EndArea();
        }
    }
}
