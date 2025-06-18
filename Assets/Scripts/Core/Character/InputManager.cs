using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DWHITE {	
	/// <summary>
	/// 中心化的输入管理器 - 使用 Unity 新输入系统
	/// 基于自动生成的 GravityShoot 输入动作类
	/// 提供简洁的事件驱动输入系统
	/// </summary>
	public class InputManager : Singleton<InputManager>
	{
	    [Header("输入设置")]
	    [SerializeField] private float _mouseSensitivity = 2f;
	    [SerializeField] private bool _invertY = false;
	    [SerializeField] private float _deadZone = 0.1f;
	    [SerializeField] private bool _enableInput = true;
	    
	    // 自动生成的输入动作
	    private GravityShoot _inputActions;
	      // 当前输入状态
		private Vector2 _moveInput;
		private Vector2 _lookInput;
		private Vector2 _rawLookInput;
		private bool _fireHeld;
		private bool _jumpHeld;
		private bool _sprintHeld;
		
		// 每帧重置的按下状态
		private bool _firePressed;
		private bool _jumpPressed;
		private bool _sprintPressed;
	    
    #region 输入事件
	    /// <summary>移动输入事件 (Vector2: WASD 输入)</summary>
	    public static event Action<Vector2> OnMoveInput;
	    
	    /// <summary>跳跃按下事件 (单帧)</summary>
	    public static event Action OnJumpPressed;
	
	    /// <summary>跳跃释放事件</summary>
	    public static event Action OnJumpReleased;
	
	    /// <summary>视角输入事件 (Vector2: 鼠标移动，已应用灵敏度和反转)</summary>
	    public static event Action<Vector2> OnLookInput;
	    
	    /// <summary>开火按下事件 (单帧)</summary>
	    public static event Action OnFirePressed;
	      /// <summary>开火释放事件</summary>
		public static event Action OnFireReleased;
		
		/// <summary>奔跑按下事件 (单帧)</summary>
		public static event Action OnSprintPressed;
		
		/// <summary>奔跑释放事件</summary>
		public static event Action OnSprintReleased;
    #endregion
	    
    #region 属性访问
	    /// <summary>当前移动输入 (已应用死区)</summary>
	    public Vector2 MoveInput => _moveInput;
	
	    /// <summary>跳跃是否被按下 (单帧)</summary>
	    public bool JumpPressed => _jumpPressed;
	
	    /// <summary>跳跃是否持续按住</summary>
	    public bool JumpHeld => _jumpHeld;
	    
	    /// <summary>当前视角输入 (已应用灵敏度和反转)</summary>
	    public Vector2 LookInput => _lookInput;
	    
	    /// <summary>原始视角输入 (未处理)</summary>
	    public Vector2 RawLookInput => _rawLookInput;
	    
	    /// <summary>开火是否被按下 (单帧)</summary>
	    public bool FirePressed => _firePressed;
	      /// <summary>开火是否持续按住</summary>
		public bool FireHeld => _fireHeld;
		
		/// <summary>奔跑是否被按下 (单帧)</summary>
		public bool SprintPressed => _sprintPressed;
		
		/// <summary>奔跑是否持续按住</summary>
		public bool SprintHeld => _sprintHeld;
	    
	    /// <summary>鼠标灵敏度</summary>
	    public float MouseSensitivity => _mouseSensitivity;
	    
	    /// <summary>Y轴是否反转</summary>
	    public bool InvertY => _invertY;
	    
	    /// <summary>输入死区</summary>
	    public float DeadZone => _deadZone;
	    
	    /// <summary>输入是否启用</summary>
	    public bool InputEnabled => _enableInput;
    #endregion
	    
    #region Unity 生命周期
	    protected override void Awake()
	    {
	        base.Awake();
	        InitializeInputSystem();
	    }
	    
	    private void OnEnable()
	    {
	        EnableInput();
	    }
	    
	    private void OnDisable()
	    {
	        DisableInput();
	    }
	    
	    private void Update()
	    {
	        if (!_enableInput) return;
	        
	        // 处理输入
	        ProcessInputs();
	        
	        // 重置每帧的按下状态
	        ResetFrameInputs();
	    }
	    
	    private void OnDestroy()
	    {
	        CleanupInputSystem();
	    }
    #endregion
	    
    #region 输入系统初始化
	    private void InitializeInputSystem()
	    {
	        // 创建输入动作实例
	        _inputActions = new GravityShoot();
	        
	        // 绑定玩家输入回调
	        BindPlayerInputCallbacks();
	        
	        Debug.Log("[InputManager] 输入系统初始化完成");
	    }
	    
	    private void BindPlayerInputCallbacks()
	    {
	        var playerActions = _inputActions.Player;
	        
	        // 移动输入
	        playerActions.Move.performed += OnMoveInputReceived;
	        playerActions.Move.canceled += OnMoveInputCanceled;
	
	        // 跳跃输入
	        playerActions.Jump.performed += OnJumpInputPressed;
	        playerActions.Jump.canceled += OnJumpInputReleased;
	        
	        // 视角输入
	        playerActions.Look.performed += OnLookInputReceived;
	        playerActions.Look.canceled += OnLookInputCanceled;
	          // 开火输入
        playerActions.Fire.performed += OnFireInputPressed;
        playerActions.Fire.canceled += OnFireInputReleased;
        
        // 奔跑输入
        playerActions.Sprint.performed += OnSprintInputPressed;
        playerActions.Sprint.canceled += OnSprintInputReleased;
	    }
	    
	    private void CleanupInputSystem()
	    {
	        if (_inputActions != null)
	        {
	            _inputActions.Dispose();
	            _inputActions = null;
	        }
	    }
    #endregion
	
    #region 输入处理
	    private void ProcessInputs()
	    {
	        // 应用死区到移动输入
	        _moveInput = ApplyDeadZone(_moveInput);
	
	        // 分发输入事件
	        OnMoveInput?.Invoke(_moveInput);
	        OnLookInput?.Invoke(_lookInput);        // 分发按下事件
        if (_firePressed)
        {
            OnFirePressed?.Invoke();
        }
        if (_jumpPressed)
        {
            OnJumpPressed?.Invoke();
        }
        if (_sprintPressed)
        {
            OnSprintPressed?.Invoke();
        }
	    }    private void ResetFrameInputs()
    {
        _firePressed = false;
        _jumpPressed = false;
        _sprintPressed = false;
    }
	    
	    private Vector2 ApplyDeadZone(Vector2 input)
	    {
	        if (input.magnitude < _deadZone)
	            return Vector2.zero;
	            
	        // 重新映射输入，去除死区影响
	        float magnitude = Mathf.InverseLerp(_deadZone, 1f, input.magnitude);
	        return input.normalized * magnitude;
	    }
	    
	    private Vector2 ProcessLookInput(Vector2 rawInput)
	    {
	        return new Vector2(
	            rawInput.x * _mouseSensitivity,
	            (_invertY ? rawInput.y : -rawInput.y) * _mouseSensitivity
	        );
	    }
    #endregion
	    
    #region 输入回调函数
	    private void OnMoveInputReceived(InputAction.CallbackContext context)
	    {
	        _moveInput = context.ReadValue<Vector2>();
	    }
	    
	    private void OnMoveInputCanceled(InputAction.CallbackContext context)
	    {
	        _moveInput = Vector2.zero;
	    }
	
	    private void OnJumpInputPressed(InputAction.CallbackContext context)
	    {
	        _jumpHeld = true;
	        _jumpPressed = true;
	        OnJumpPressed?.Invoke();
	    }
	
	    private void OnJumpInputReleased(InputAction.CallbackContext context)
	    {
	        _jumpHeld = false;
	        OnJumpReleased?.Invoke();
	    }
	    
	    private void OnLookInputReceived(InputAction.CallbackContext context)
	    {
	        _rawLookInput = context.ReadValue<Vector2>();
	        _lookInput = ProcessLookInput(_rawLookInput);
	    }
	    
	    private void OnLookInputCanceled(InputAction.CallbackContext context)
	    {
	        _rawLookInput = Vector2.zero;
	        _lookInput = Vector2.zero;
	    }
	    
	    private void OnFireInputPressed(InputAction.CallbackContext context)
	    {
	        _fireHeld = true;
	        _firePressed = true;
	        OnFirePressed?.Invoke();
	    }
	      private void OnFireInputReleased(InputAction.CallbackContext context)
    {
        _fireHeld = false;
        OnFireReleased?.Invoke();
    }
    
    private void OnSprintInputPressed(InputAction.CallbackContext context)
    {
        _sprintHeld = true;
        _sprintPressed = true;
        OnSprintPressed?.Invoke();
    }
    
    private void OnSprintInputReleased(InputAction.CallbackContext context)
    {
        _sprintHeld = false;
        OnSprintReleased?.Invoke();
    }
    #endregion
	    
    #region 公共 API
	    /// <summary>
	    /// 启用输入
	    /// </summary>
	    public void EnableInput()
	    {
	        if (_inputActions != null)
	        {
	            _inputActions.Player.Enable();
	            SetCursorLock(true);
	            _enableInput = true;
	            Debug.Log("[InputManager] 输入已启用");
	        }
	    }
	    
	    /// <summary>
	    /// 禁用输入
	    /// </summary>
	    public void DisableInput()
	    {
	        if (_inputActions != null)
	        {
	            _inputActions.Player.Disable();
	            SetCursorLock(false);
	            _enableInput = false;
	              // 清空当前输入状态
            _moveInput = Vector2.zero;
            _lookInput = Vector2.zero;
            _rawLookInput = Vector2.zero;
            _fireHeld = false;
            _firePressed = false;
            _jumpHeld = false;
            _jumpPressed = false;
            _sprintHeld = false;
            _sprintPressed = false;
	            
	            Debug.Log("[InputManager] 输入已禁用");
	        }
	    }
	    
	    /// <summary>
	    /// 设置鼠标灵敏度
	    /// </summary>
	    /// <param name="sensitivity">灵敏度值 (最小 0.1)</param>
	    public void SetMouseSensitivity(float sensitivity)
	    {
	        _mouseSensitivity = Mathf.Max(0.1f, sensitivity);
	        Debug.Log($"[InputManager] 鼠标灵敏度设置为: {_mouseSensitivity}");
	    }
	    
	    /// <summary>
	    /// 设置Y轴反转
	    /// </summary>
	    /// <param name="invert">是否反转Y轴</param>
	    public void SetInvertY(bool invert)
	    {
	        _invertY = invert;
	        Debug.Log($"[InputManager] Y轴反转设置为: {_invertY}");
	    }
	    
	    /// <summary>
	    /// 设置输入死区
	    /// </summary>
	    /// <param name="deadZone">死区值 (0-1)</param>
	    public void SetDeadZone(float deadZone)
	    {
	        _deadZone = Mathf.Clamp01(deadZone);
	        Debug.Log($"[InputManager] 输入死区设置为: {_deadZone}");
	    }
	    
	    /// <summary>
	    /// 锁定/解锁鼠标光标
	    /// </summary>
	    /// <param name="locked">是否锁定光标</param>
	    public void SetCursorLock(bool locked)
	    {
	        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
	        Cursor.visible = !locked;
	    }
	    
	    /// <summary>
	    /// 切换输入控制方案
	    /// </summary>
	    /// <param name="schemeName">控制方案名称 (KeyboardMouse, Gamepad, etc.)</param>
	    public void SwitchControlScheme(string schemeName)
	    {
	        if (_inputActions == null) return;
	        
	        try
	        {
	            var schemes = _inputActions.controlSchemes;
	            foreach (var scheme in schemes)
	            {
	                if (scheme.name.Equals(schemeName, StringComparison.OrdinalIgnoreCase))
	                {
	                    _inputActions.bindingMask = InputBinding.MaskByGroup(scheme.bindingGroup);
	                    Debug.Log($"[InputManager] 切换到控制方案: {schemeName}");
	                    return;
	                }
	            }
	            Debug.LogWarning($"[InputManager] 未找到控制方案: {schemeName}");
	        }
	        catch (Exception e)
	        {
	            Debug.LogError($"[InputManager] 切换控制方案失败: {e.Message}");
	        }
	    }
	    
	    /// <summary>
	    /// 临时启用/禁用输入
	    /// </summary>
	    /// <param name="enabled">是否启用</param>
	    public void SetInputEnabled(bool enabled)
	    {
	        if (enabled)
	            EnableInput();
	        else
	            DisableInput();
	    }
	    
	    /// <summary>
	    /// 获取可用的控制方案列表
	    /// </summary>
	    /// <returns>控制方案名称数组</returns>
	    public string[] GetAvailableControlSchemes()
	    {
	        if (_inputActions == null) return new string[0];
	        
	        var schemes = _inputActions.controlSchemes;
	        string[] schemeNames = new string[schemes.Count];
	        
	        for (int i = 0; i < schemes.Count; i++)
	        {
	            schemeNames[i] = schemes[i].name;
	        }
	        
	        return schemeNames;
	    }
    #endregion
	    
    #region Inspector 验证
	    private void OnValidate()
	    {
	        _mouseSensitivity = Mathf.Max(0.1f, _mouseSensitivity);
	        _deadZone = Mathf.Clamp01(_deadZone);
	    }
    #endregion
	}
}
