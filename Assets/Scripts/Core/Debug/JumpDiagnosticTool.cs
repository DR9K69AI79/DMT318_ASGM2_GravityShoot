using UnityEngine;
using System.Collections;
using Photon.Pun;

namespace DWHITE
{
    /// <summary>
    /// 跳跃功能诊断工具 - 用于调试和分析跳跃失效问题
    /// </summary>
    public class JumpDiagnosticTool : MonoBehaviour
    {
        [Header("诊断配置")]
        [SerializeField] private bool _enableRealTimeDiagnostic = true;
        [SerializeField] private bool _showDebugGUI = true;
        [SerializeField] private float _diagnosticInterval = 1f;
        [SerializeField] private KeyCode _runDiagnosticKey = KeyCode.F9;
        
        [Header("组件引用")]
        [SerializeField] private PlayerInput _playerInput;
        [SerializeField] private PlayerMotor _playerMotor;
        
        private bool _lastJumpPressed = false;
        private float _lastDiagnosticTime = 0f;
        private string _diagnosticResult = "";
          private void Start()
        {
            // 不在Start中查找组件，因为此时玩家还未生成
            // 组件查找将在Update中动态进行
            Debug.Log("[JumpDiagnostic] 诊断工具已启动，等待玩家生成...");
        }
          private void Update()
        {
            // 动态查找本地玩家组件
            TryFindLocalPlayerComponents();
            
            if (_enableRealTimeDiagnostic)
            {
                MonitorJumpInput();
            }
            
            if (Input.GetKeyDown(_runDiagnosticKey))
            {
                RunFullDiagnostic();
            }
            
            if (Time.time - _lastDiagnosticTime > _diagnosticInterval)
            {
                _lastDiagnosticTime = Time.time;
                UpdateDiagnosticResult();
            }
        }
        
        private void MonitorJumpInput()
        {
            if (_playerInput != null)
            {
                bool currentJumpPressed = _playerInput.JumpPressed;
                if (currentJumpPressed && !_lastJumpPressed)
                {
                    Debug.Log($"[JumpDiagnostic] 🎯 跳跃输入检测到 - 时间: {Time.time:F2}");
                    Debug.Log($"[JumpDiagnostic] 输入状态: Pressed={currentJumpPressed}, Held={_playerInput.JumpHeld}");
                    Debug.Log($"[JumpDiagnostic] PlayerInputEnabled: {_playerInput.InputEnabled}");

                    if (InputManager.Instance != null)
                    {
                        Debug.Log($"[JumpDiagnostic] InputManager: {InputManager.Instance.gameObject.name}");
                        Debug.Log($"[JumpDiagnostic] InputManager状态: Enabled={InputManager.Instance?.InputEnabled}, JumpPressed={InputManager.Instance?.JumpPressed}, JumpHeld={InputManager.Instance?.JumpHeld}");
                    }else
                    {
                        Debug.LogWarning("[JumpDiagnostic] InputManager未找到或未初始化");
                    }
                    
                    if (_playerMotor != null)
                    {
                        Debug.Log($"[JumpDiagnostic] 地面状态: IsGrounded={_playerMotor.IsGrounded}");
                    }
                }
                _lastJumpPressed = currentJumpPressed;
            }
        }

        /// <summary>
        /// 运行完整的跳跃功能诊断
        /// </summary>
        [ContextMenu("运行跳跃诊断")]
        public void RunFullDiagnostic()
        {
            Debug.Log("=== 跳跃功能完整诊断 ===");
            
            // 0. 网络状态检查
            Debug.Log("0. 网络状态:");
            Debug.Log($"   PhotonNetwork.IsConnected: {PhotonNetwork.IsConnected}");
            Debug.Log($"   PhotonNetwork.InRoom: {PhotonNetwork.InRoom}");
            if (PhotonNetwork.InRoom)
            {
                Debug.Log($"   房间玩家数: {PhotonNetwork.CurrentRoom.PlayerCount}");
                Debug.Log($"   本地玩家: {PhotonNetwork.LocalPlayer.NickName}");
            }
            
            // 强制查找组件
            TryFindLocalPlayerComponents();
            
            // 1. 检查组件存在性
            Debug.Log("1. 组件检查:");
            Debug.Log($"   PlayerInput: {(_playerInput != null ? "✅ 存在" : "❌ 缺失")}");
            Debug.Log($"   PlayerMotor: {(_playerMotor != null ? "✅ 存在" : "❌ 缺失")}");
            
            if (_playerInput != null)
            {
                Debug.Log($"   PlayerInput GameObject: {_playerInput.gameObject.name}");
                var pv = _playerInput.GetComponent<PhotonView>();
                if (pv != null)
                {
                    Debug.Log($"   PhotonView: ViewID={pv.ViewID}, IsMine={pv.IsMine}");
                }
            }
            
            if (_playerInput == null || _playerMotor == null)
            {
                Debug.Log("❌ 关键组件缺失，可能原因:");
                Debug.Log("   - 玩家尚未生成 (按F3生成玩家)");
                Debug.Log("   - 玩家对象被意外销毁");
                Debug.Log("   - 这不是本地玩家的组件");
                return;
            }
            
            // ...existing code...
            
            // 2. 检查输入系统状态
            Debug.Log("2. 输入系统状态:");
            Debug.Log($"   InputEnabled: {_playerInput.InputEnabled}");
            Debug.Log($"   JumpPressed: {_playerInput.JumpPressed}");
            Debug.Log($"   JumpHeld: {_playerInput.JumpHeld}");
            Debug.Log($"   MoveInput: {_playerInput.MoveInput}");
            
            // 3. 检查InputManager连接
            InputManager inputManager = InputManager.Instance;
            Debug.Log("3. InputManager状态:");
            Debug.Log($"   Instance存在: {(inputManager != null ? "✅" : "❌")}");
            if (inputManager != null)
            {
                Debug.Log($"   InputEnabled: {inputManager.InputEnabled}");
                Debug.Log($"   JumpPressed: {inputManager.JumpPressed}");
                Debug.Log($"   JumpHeld: {inputManager.JumpHeld}");
            }
            
            // 4. 检查物理状态
            Debug.Log("4. 物理状态:");
            Debug.Log($"   IsGrounded: {_playerMotor.IsGrounded}");
            Debug.Log($"   Velocity: {_playerMotor.Velocity}");
            Debug.Log($"   UpAxis: {_playerMotor.UpAxis}");
            
            // 5. 检查GameObject状态
            Debug.Log("5. GameObject状态:");
            Debug.Log($"   Active: {gameObject.activeInHierarchy}");
            Debug.Log($"   PlayerInput Enabled: {(_playerInput != null ? _playerInput.enabled : false)}");
            Debug.Log($"   PlayerMotor Enabled: {(_playerMotor != null ? _playerMotor.enabled : false)}");
            
            Debug.Log("=== 诊断完成 ===");
            
            // 6. 提供修复建议
            ProvideFix();
        }
        
        private void ProvideFix()
        {
            Debug.Log("🔧 修复建议:");
            
            if (_playerInput != null && !_playerInput.InputEnabled)
            {
                Debug.Log("   - PlayerInput未启用，尝试调用ForceReinitialize()");
                _playerInput.ForceReinitialize();
            }
            
            if (InputManager.Instance == null)
            {
                Debug.Log("   - InputManager缺失，请确保场景中有InputManager");
            }
            else if (!InputManager.Instance.InputEnabled)
            {
                Debug.Log("   - InputManager未启用，检查设置");
            }
            
            Debug.Log("   - 如果问题持续，尝试重新生成玩家");
            Debug.Log("   - 或使用NetworkTestHelper的修复功能 (F4键)");
        }
          private void UpdateDiagnosticResult()
        {
            if (!PhotonNetwork.InRoom)
            {
                _diagnosticResult = "未在房间中";
                return;
            }
            
            if (_playerInput == null || _playerMotor == null)
            {
                _diagnosticResult = "等待玩家生成";
                return;
            }
            
            string status = "正常";
            if (!_playerInput.InputEnabled)
                status = "输入系统异常";
            else if (InputManager.Instance == null)
                status = "InputManager缺失";
            else if (!InputManager.Instance.InputEnabled)
                status = "InputManager未启用";
                
            _diagnosticResult = status;
        }
          private void OnGUI()
        {
            if (!_showDebugGUI) return;
            
            GUILayout.BeginArea(new Rect(10, Screen.height - 200, 450, 200));
            GUILayout.BeginVertical("box");
            
            GUILayout.Label("跳跃功能诊断", GUI.skin.label);
            GUILayout.Space(5);
            
            // 网络状态
            string networkStatus = PhotonNetwork.InRoom ? "在房间中" : "未在房间";
            GUILayout.Label($"网络: {networkStatus}");
            
            // 组件状态
            string componentStatus = (_playerInput != null && _playerMotor != null) ? "已找到" : "未找到";
            GUILayout.Label($"玩家组件: {componentStatus}");
            
            // 诊断结果
            GUILayout.Label($"状态: {_diagnosticResult}");
            
            if (_playerInput != null)
            {
                GUILayout.Label($"输入: {(_playerInput.InputEnabled ? "正常" : "异常")}");
                GUILayout.Label($"Jump: P={_playerInput.JumpPressed} H={_playerInput.JumpHeld}");
            }
            
            if (_playerMotor != null)
            {
                GUILayout.Label($"地面: {(_playerMotor.IsGrounded ? "是" : "否")}");
            }
            
            GUILayout.Space(5);
            if (GUILayout.Button("运行完整诊断 (F9)"))
            {
                RunFullDiagnostic();
            }
            
            if (_playerInput != null && !_playerInput.InputEnabled)
            {
                if (GUILayout.Button("尝试修复输入"))
                {
                    _playerInput.ForceReinitialize();
                }
            }
            
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }
        
        /// <summary>
        /// 尝试动态查找本地玩家的组件
        /// </summary>
        private void TryFindLocalPlayerComponents()
        {
            // 如果已经找到了组件且组件仍然有效，就不需要重新查找
            if (_playerInput != null && _playerMotor != null && 
                _playerInput.gameObject != null && _playerMotor.gameObject != null)
            {
                return;
            }
            
            // 重置引用（可能之前的玩家被销毁了）
            if (_playerInput != null && _playerInput.gameObject == null)
                _playerInput = null;
            if (_playerMotor != null && _playerMotor.gameObject == null)
                _playerMotor = null;
            
            // 在网络环境中，需要查找属于本地玩家的组件
            if (Application.isPlaying)
            {
                // 先尝试查找有PhotonView且IsMine的玩家对象
                var photonViews = FindObjectsOfType<Photon.Pun.PhotonView>();
                
                foreach (var pv in photonViews)
                {
                    if (pv.IsMine) // 只关注本地玩家
                    {
                        var playerInput = pv.GetComponent<PlayerInput>();
                        var playerMotor = pv.GetComponent<PlayerMotor>();
                        
                        if (playerInput != null && playerMotor != null)
                        {
                            bool wasNull = _playerInput == null || _playerMotor == null;
                            _playerInput = playerInput;
                            _playerMotor = playerMotor;
                            
                            if (wasNull)
                            {
                                Debug.Log($"[JumpDiagnostic] ✅ 找到本地玩家组件: {pv.gameObject.name}");
                            }
                            return;
                        }
                    }
                }
                
                // 如果没有找到网络玩家，回退到查找任意PlayerInput/PlayerMotor（单机模式）
                if (_playerInput == null)
                {
                    var foundInput = FindObjectOfType<PlayerInput>();
                    if (foundInput != null && foundInput != _playerInput)
                    {
                        _playerInput = foundInput;
                        Debug.Log($"[JumpDiagnostic] ✅ 找到PlayerInput组件（单机模式）: {foundInput.gameObject.name}");
                    }
                }
                
                if (_playerMotor == null)
                {
                    var foundMotor = FindObjectOfType<PlayerMotor>();
                    if (foundMotor != null && foundMotor != _playerMotor)
                    {
                        _playerMotor = foundMotor;
                        Debug.Log($"[JumpDiagnostic] ✅ 找到PlayerMotor组件（单机模式）: {foundMotor.gameObject.name}");
                    }
                }
            }
        }
    }
}
