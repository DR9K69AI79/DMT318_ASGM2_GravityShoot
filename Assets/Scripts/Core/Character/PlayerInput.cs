using UnityEngine;
using System.Collections;

namespace DWHITE
{
    /// <summary>
    /// 玩家输入接口 - 提供简化的输入访问
    /// 作为 InputManager 的前端接口，支持输入过滤和本地化控制
    /// 适用于需要独立输入控制的角色组件
    /// 
    /// 架构说明：
    /// - 本组件只负责输入过滤和状态暴露
    /// - 不主动转发事件到其他组件
    /// - Controller组件应主动拉取输入状态
    /// </summary>
    public class PlayerInput : MonoBehaviour
    {
        [Header("输入过滤设置")]
        [SerializeField] private bool _enableMove = true;
        [SerializeField] private bool _enableLook = true;
        [SerializeField] private bool _enableFire = true;
        [SerializeField] private bool _enableSprint = true;
        [SerializeField] private bool _enableReload = true;
        [SerializeField] private bool _enableWeaponSwitch = true;

        [Header("调试信息")]
        [SerializeField] private bool _showDebugInfo = false;

        // 输入管理器引用
        private InputManager _inputManager;
        
        // 过滤后的输入状态
        private Vector2 _filteredMoveInput;
        private bool _filteredJumpPressed;
        private Vector2 _filteredLookInput;
        private bool _filteredFirePressed;
        private bool _filteredSprintPressed;
        private bool _filteredReloadPressed;
        private float _weaponSwitchInput;

        #region 输入属性访问
        /// <summary>移动输入 (已过滤)</summary>
        public Vector2 MoveInput => _enableMove ? _filteredMoveInput : Vector2.zero;

        /// <summary>跳跃按下 (已过滤)</summary>
        public bool JumpPressed => _enableMove ? _filteredJumpPressed : false;

        /// <summary>跳跃持续按住 (已过滤)</summary>
        public bool JumpHeld => _enableMove && _inputManager != null ? _inputManager.JumpHeld : false;

        /// <summary>视角输入 (已过滤)</summary>
        public Vector2 LookInput => _enableLook ? _filteredLookInput : Vector2.zero;

        /// <summary>开火按下 (已过滤，单帧)</summary>
        public bool FirePressed => _enableFire ? _filteredFirePressed : false;

        /// <summary>开火持续按住 (已过滤)</summary>
        public bool FireHeld => _enableFire && _inputManager != null ? _inputManager.FireHeld : false;

        /// <summary>奔跑按下 (已过滤，单帧)</summary>
        public bool SprintPressed => _enableSprint ? _filteredSprintPressed : false;

        /// <summary>奔跑持续按住 (已过滤)</summary>
        public bool SprintHeld => _enableSprint && _inputManager != null ? _inputManager.SprintHeld : false;

        /// <summary>装弹按下 (已过滤，单帧)</summary>
        public bool ReloadPressed => _enableReload ? _filteredReloadPressed : false;

        /// <summary>武器切换输入 (已过滤)</summary>
        public float WeaponSwitchInput => _enableWeaponSwitch ? _weaponSwitchInput : 0f;

        // 便捷属性访问
        /// <summary>鼠标灵敏度</summary>
        public float MouseSensitivity => _inputManager != null ? _inputManager.MouseSensitivity : 1f;

        /// <summary>Y轴是否反转</summary>
        public bool InvertY => _inputManager != null ? _inputManager.InvertY : false;

        /// <summary>输入死区</summary>
        public float DeadZone => _inputManager != null ? _inputManager.DeadZone : 0.1f;

        /// <summary>输入是否完全启用</summary>
        public bool InputEnabled => _inputManager != null && _inputManager.InputEnabled;
        #endregion

        #region Unity 生命周期
        
        private void Start()
        {
            _inputManager = InputManager.Instance;
        }

        private void OnEnable()
        {
            // 订阅输入事件
            SubscribeToInputEvents();
        }

        private void OnDisable()
        {
            // 取消订阅输入事件
            UnsubscribeFromInputEvents();
        }

        private void Update()
        {
            // 调试信息
            if (_showDebugInfo)
            {
                ShowDebugInfo();
            }
        }

        private void LateUpdate()
        {
            // 在 LateUpdate 中重置每帧的按下状态
            // 确保所有脚本的 Update() 都有机会读取到这一帧的按下事件
            _filteredFirePressed = false;
            _filteredJumpPressed = false;
            _filteredSprintPressed = false;
            _filteredReloadPressed = false;
            _weaponSwitchInput = 0f; // 重置武器切换输入
        }
        #endregion

        #region 事件订阅管理        
        private void SubscribeToInputEvents()
        {
            if (_inputManager == null)
                _inputManager = InputManager.Instance;

            if (_inputManager != null)
            {
                InputManager.OnMoveInput += OnMoveInput;
                InputManager.OnJumpPressed += OnJumpPressed;
                InputManager.OnLookInput += OnLookInput;
                InputManager.OnFirePressed += OnFirePressed;
                InputManager.OnFireReleased += OnFireReleased;
                InputManager.OnSprintPressed += OnSprintPressed;
                InputManager.OnReloadPressed += OnReloadPressed;
                InputManager.OnWeaponSwitchInput += OnWeaponSwitchInput;

                if (_showDebugInfo)
                    Debug.Log("[PlayerInput] 已订阅输入事件");
            }
        }

        private void UnsubscribeFromInputEvents()
        {
            InputManager.OnMoveInput -= OnMoveInput;
            InputManager.OnJumpPressed -= OnJumpPressed;
            InputManager.OnLookInput -= OnLookInput;
            InputManager.OnFirePressed -= OnFirePressed;
            InputManager.OnFireReleased -= OnFireReleased;
            InputManager.OnSprintPressed -= OnSprintPressed;
            InputManager.OnReloadPressed -= OnReloadPressed;
            InputManager.OnWeaponSwitchInput -= OnWeaponSwitchInput;

            if (_showDebugInfo)
                Debug.Log("[PlayerInput] 已取消订阅输入事件");
        }
        #endregion

        #region 输入事件处理器
        private void OnMoveInput(Vector2 input)
        {
            _filteredMoveInput = input;
        }

        private void OnJumpPressed()
        {
            _filteredJumpPressed = true;
        }

        private void OnLookInput(Vector2 input)
        {
            _filteredLookInput = input;
        }

        private void OnFirePressed()
        {
            _filteredFirePressed = true;
        }

        private void OnFireReleased()
        {
            // 开火释放事件处理（如果需要）
        }

        private void OnReloadPressed()
        {
            _filteredReloadPressed = true;
        }

        private void OnWeaponSwitchInput(float switchValue)
        {
            _weaponSwitchInput = switchValue;
        }

        private void OnSprintPressed()
        {
            _filteredSprintPressed = true;
        }
        #endregion

        #region 公共方法
        /// <summary>
        /// 启用/禁用特定输入类型
        /// </summary>
        /// <param name="move">移动输入</param>
        /// <param name="look">视角输入</param>
        /// <param name="fire">开火输入</param>
        /// <param name="sprint">奔跑输入</param>
        /// <param name="reload">装弹输入</param>
        /// <param name="weaponSwitch">武器切换输入</param>
        public void SetInputEnabled(bool move, bool look, bool fire, bool sprint = true, bool reload = true, bool weaponSwitch = true)
        {
            _enableMove = move;
            _enableLook = look;
            _enableFire = fire;
            _enableSprint = sprint;
            _enableReload = reload;
            _enableWeaponSwitch = weaponSwitch;

            if (_showDebugInfo)
            {
                Debug.Log($"[PlayerInput] 输入设置 - Move: {move}, Look: {look}, Fire: {fire}, Sprint: {sprint}, Reload: {reload}, Switch: {weaponSwitch}");
            }
        }

        /// <summary>
        /// 启用/禁用特定输入类型（重载保持兼容性）
        /// </summary>
        /// <param name="move">移动输入</param>
        /// <param name="look">视角输入</param>
        /// <param name="fire">开火输入</param>
        public void SetInputEnabled(bool move, bool look, bool fire)
        {
            SetInputEnabled(move, look, fire, true, true, true);
        }

        /// <summary>
        /// 启用/禁用移动输入
        /// </summary>
        public void SetMoveEnabled(bool enabled)
        {
            _enableMove = enabled;
            if (_showDebugInfo)
                Debug.Log($"[PlayerInput] 移动输入: {enabled}");
        }

        /// <summary>
        /// 启用/禁用视角输入
        /// </summary>
        public void SetLookEnabled(bool enabled)
        {
            _enableLook = enabled;
            if (_showDebugInfo)
                Debug.Log($"[PlayerInput] 视角输入: {enabled}");
        }

        /// <summary>
        /// 启用/禁用开火输入
        /// </summary>
        public void SetFireEnabled(bool enabled)
        {
            _enableFire = enabled;
            if (_showDebugInfo)
                Debug.Log($"[PlayerInput] 开火输入: {enabled}");
        }

        /// <summary>
        /// 启用/禁用奔跑输入
        /// </summary>
        public void SetSprintEnabled(bool enabled)
        {
            _enableSprint = enabled;
            if (_showDebugInfo)
                Debug.Log($"[PlayerInput] 奔跑输入: {enabled}");
        }

        /// <summary>
        /// 启用/禁用装弹输入
        /// </summary>
        public void SetReloadEnabled(bool enabled)
        {
            _enableReload = enabled;
            if (_showDebugInfo)
                Debug.Log($"[PlayerInput] 装弹输入: {enabled}");
        }

        /// <summary>
        /// 启用/禁用武器切换输入
        /// </summary>
        public void SetWeaponSwitchEnabled(bool enabled)
        {
            _enableWeaponSwitch = enabled;
            if (_showDebugInfo)
                Debug.Log($"[PlayerInput] 武器切换输入: {enabled}");
        }

        /// <summary>
        /// 设置鼠标灵敏度（委托给InputManager）
        /// </summary>
        public void SetMouseSensitivity(float sensitivity)
        {
            if (_inputManager != null)
            {
                _inputManager.SetMouseSensitivity(sensitivity);
                if (_showDebugInfo)
                    Debug.Log($"[PlayerInput] 鼠标灵敏度设置为: {sensitivity}");
            }
        }

        /// <summary>
        /// 设置Y轴反转（委托给InputManager）
        /// </summary>
        public void SetInvertY(bool invert)
        {
            if (_inputManager != null)
            {
                _inputManager.SetInvertY(invert);
                if (_showDebugInfo)
                    Debug.Log($"[PlayerInput] Y轴反转设置为: {invert}");
            }
        }

        /// <summary>
        /// 锁定/解锁鼠标光标（委托给InputManager）
        /// </summary>
        public void SetCursorLock(bool locked)
        {
            if (_inputManager != null)
            {
                _inputManager.SetCursorLock(locked);
                if (_showDebugInfo)
                    Debug.Log($"[PlayerInput] 鼠标锁定设置为: {locked}");
            }
        }

        /// <summary>
        /// 切换控制方案（委托给InputManager）
        /// </summary>
        public void SwitchControlScheme(string schemeName)
        {
            if (_inputManager != null)
            {
                _inputManager.SwitchControlScheme(schemeName);
                if (_showDebugInfo)
                    Debug.Log($"[PlayerInput] 切换控制方案: {schemeName}");
            }
        }

        /// <summary>
        /// 获取可用控制方案
        /// </summary>
        public string[] GetAvailableControlSchemes()
        {
            return _inputManager != null ? _inputManager.GetAvailableControlSchemes() : new string[0];
        }

        /// <summary>
        /// 临时启用/禁用所有输入
        /// </summary>
        public void SetAllInputEnabled(bool enabled)
        {
            SetInputEnabled(enabled, enabled, enabled, enabled, enabled, enabled);
        }
        #endregion

        #region 调试功能
        private void ShowDebugInfo()
        {
            if (Time.frameCount % 60 == 0) // 每秒显示一次
            {
                Debug.Log($"[PlayerInput] Move: {MoveInput}, Jump: {JumpPressed}/{JumpHeld}, Look: {LookInput}, Fire: {FirePressed}/{FireHeld}, Reload: {ReloadPressed}, Switch: {WeaponSwitchInput}");
            }
        }

        /// <summary>
        /// 在编辑器中显示调试信息
        /// </summary>
        private void OnGUI()
        {
            if (!_showDebugInfo || !Application.isPlaying) return;

            GUILayout.BeginArea(new Rect(320, 10, 300, 500));
            GUILayout.BeginVertical("box");

            GUILayout.Label("Player Input Debug", GUI.skin.label);
            GUILayout.Space(5);
            GUILayout.Label($"Move Input: {MoveInput}");
            GUILayout.Label($"Jump: Pressed={JumpPressed}, Held={JumpHeld}");
            GUILayout.Label($"Look Input: {LookInput}");
            GUILayout.Label($"Fire: Pressed={FirePressed}, Held={FireHeld}");
            GUILayout.Label($"Sprint: Pressed={SprintPressed}, Held={SprintHeld}");
            GUILayout.Label($"Reload: Pressed={ReloadPressed}");
            GUILayout.Label($"Weapon Switch: {WeaponSwitchInput}");
            GUILayout.Space(5);

            GUILayout.Label($"Filters: Move={_enableMove}, Look={_enableLook}, Fire={_enableFire}");
            GUILayout.Label($"         Sprint={_enableSprint}, Reload={_enableReload}, Switch={_enableWeaponSwitch}");
            GUILayout.Label($"Mouse Sensitivity: {MouseSensitivity}");
            GUILayout.Label($"Invert Y: {InvertY}");

            GUILayout.EndVertical();
            GUILayout.EndArea();
        }
        #endregion

        #region 强制重新初始化
        /// <summary>
        /// 强制重新初始化输入系统（用于修复初始化失败的情况）
        /// </summary>
        public void ForceReinitialize()
        {
            if (_showDebugInfo)
                Debug.Log("[PlayerInput] 强制重新初始化输入系统");

            // 取消旧的订阅
            UnsubscribeFromInputEvents();

            // 重新获取InputManager引用
            _inputManager = InputManager.Instance;

            // 重新订阅
            SubscribeToInputEvents();

            if (_showDebugInfo)
                Debug.Log($"[PlayerInput] 重新初始化完成，InputManager状态: {(_inputManager != null ? "已连接" : "未连接")}");
        }
        #endregion
    }
}