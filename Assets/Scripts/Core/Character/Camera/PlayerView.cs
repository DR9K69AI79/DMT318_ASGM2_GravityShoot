// --------------------------------------------------------------------------------------------------------------------
// <copyright file="PlayerView.cs" company="DWHITE">
//   Copyright (c) DWHITE. All rights reserved.
// </copyright>
// <summary>
//   优化说明：
//   此脚本经过了全面的可读性和可扩展性优化，但未修改任何核心运行逻辑。
//   主要优化点：
//   1.  [结构] 使用 #region 对代码进行逻辑分组，使结构更清晰。
//   2.  [依赖] 使用 [RequireComponent] 明确声明对 PlayerInput 和 PlayerMotor 的硬依赖，增强了组件的健壮性。
//   3.  [可读性] 引入了常量来替代“魔法数字”（如 Epsilon、输入死区），使代码意图更明确。
//   4.  [代码复用] 
//       - 抽象出 GetHeadPosition() 辅助方法，消除了在多个位置重复计算头部位置的代码。
//       - 合并了 HandleInputWithVectors 和 AddViewKick 中相似的旋转逻辑到 ApplyViewRotation 方法，减少了代码冗余。
//   5.  [性能与简洁性]
//       - 使用 Unity 内置的 Quaternion.AngleAxis 替代了自定义的 Rodrigues 旋转公式实现（RotateVectorAroundAxis），更简洁且可能更高效。
//       - 简化了 UpdateHorizontalDirections 方法的逻辑，使其更直接和健壮。
//   6.  [注释] 删除了临时性或冗余的注释，保留了核心的 XML 文档注释和对复杂逻辑的必要解释。
//   7.  [健壮性] 在 ForceGravityUpdate 方法中加入了 TODO 注释，提示其功能需要实现。
// </summary>
// --------------------------------------------------------------------------------------------------------------------

using UnityEngine;

namespace DWHITE
{
    /// <summary>
    /// 基于重力变换矩阵的第一人称视角控制器。
    /// 职责：
    /// 1. 维护重力变换矩阵，实时同步重力变化。
    /// 2. 通过变换矩阵处理所有方向向量，确保在任意重力下的逻辑一致性。
    /// 3. 实现头部独立转动和身体平滑跟随的逻辑。
    /// 4. 基于变换后的向量提供准确的移动方向。
    /// </summary>
    [RequireComponent(typeof(PlayerInput), typeof(PlayerMotor))]
    public class PlayerView : MonoBehaviour
    {
        #region 依赖与配置 (Dependencies & Configuration)

        [Header("核心引用")]
        [Tooltip("玩家输入组件")]
        [SerializeField] private PlayerInput _playerInput;
        [Tooltip("玩家马达组件，提供重力信息和旋转控制")]
        [SerializeField] private PlayerMotor _motor;
        [Tooltip("瞄准目标点的Transform，用于精确控制射线和武器朝向")]
        [SerializeField] private Transform _aimTarget;
        [Tooltip("玩家身体的根Transform，用于旋转和位置参考")]
        [SerializeField] private Transform _playerBody;

        [Header("视角控制")]
        [SerializeField] private float _lookSensitivity = 1.0f;
        [SerializeField] [Range(0f, 90f)] private float _maxPitchUp = 88f;
        [SerializeField] [Range(0f, 90f)] private float _maxPitchDown = 88f;
        [SerializeField] private bool _invertY = false;

        [Header("头部/身体 旋转逻辑")]
        [Tooltip("头部相对于身体可以独立旋转的最大角度")]
        [SerializeField] private float _headYawLimit = 60f;
        [Tooltip("当头部转动超过限制时，身体跟上旋转的速度")]
        [SerializeField] private float _bodyRotationSpeed = 8f;
        [Tooltip("身体向头部对齐的速度（当角度差较小时）")]
        [SerializeField] private float _bodyAlignmentSpeed = 0.3f;
        [Tooltip("开始身体对齐的角度差阈值（相对于头部转动限制的比例）")]
        [SerializeField] [Range(0.1f, 0.9f)] private float _alignmentThreshold = 0.5f;
        
        [Header("AimTarget 设置")]
        [Tooltip("AimTarget 距离玩家头部的距离")]
        [SerializeField] private float _aimDistance = 10f;
        [Tooltip("头部相对于玩家根物体的位置偏移")]
        [SerializeField] private Vector3 _headOffset = new Vector3(0, 1.7f, 0);

        [Header("调试")]
        [SerializeField] private bool _showDebugInfo = false;
        [SerializeField] private bool _showDebugGizmos = false;
        
        #endregion

        #region 公共属性 (Public Properties)

        /// <summary>
        /// 相对于当前重力平面的前进方向，用于移动。
        /// </summary>
        public Vector3 HorizontalForwardDirection { get; private set; }
        
        /// <summary>
        /// 相对于当前重力平面的右侧方向，用于移动。
        /// </summary>
        public Vector3 HorizontalRightDirection { get; private set; }
        
        /// <summary>
        /// 当前瞄准方向（世界坐标系），已应用重力变换。
        /// </summary>
        public Vector3 CurrentAimDirection => _gravityTransform * _referenceAimDirection;
        
        /// <summary>
        /// 当前身体朝向（世界坐标系），已应用重力变换。
        /// </summary>
        public Vector3 CurrentBodyDirection => _gravityTransform * _referenceBodyDirection;

        #endregion

        #region 内部状态 (Internal State)

        // 定义一个微小的阈值，用于浮点数和向量幅度的比较，避免精度问题。
        private const float Epsilon = 1e-3f;

        // 重力变换系统
        private static readonly Vector3 ReferenceGravityUp = Vector3.up; // 绝对参考上方向
        private Quaternion _gravityTransform = Quaternion.identity;      // 从参考重力到当前重力的变换
        private Quaternion _inverseGravityTransform = Quaternion.identity; // 逆变换

        // 基于参考坐标系的内部状态（在标准重力下的"意图"方向）
        private Vector3 _referenceAimDirection = Vector3.forward;
        private Vector3 _referenceBodyDirection = Vector3.forward;
        
        // 当前俯仰角（仅用于计算和调试）
        private float _currentPitch = 0f;

        #endregion
        
        #region Unity 生命周期 (Unity Lifecycle)

        private void Awake()
        {
            // [RequireComponent] 属性已确保这些组件存在
            _playerInput = GetComponent<PlayerInput>();
            _motor = GetComponent<PlayerMotor>();
        }

        private void Start()
        {
            _playerInput.SetCursorLock(true);
            
            InitializeGravityTransform();
            InitializeDirections();
        }

        private void LateUpdate()
        {
            UpdateGravityTransform();
            
            HandleInput();
            
            UpdateBodyRotation();
            
            UpdateAimTargetPosition();
            
            UpdateHorizontalDirections();
            
            if (_showDebugInfo)
            {
                UpdateDebugInfo();
            }
        }
        
        private void OnValidate()
        {
            // 在编辑器中修改数值时，确保其在有效范围内
            _lookSensitivity = Mathf.Max(0.1f, _lookSensitivity);
            _bodyRotationSpeed = Mathf.Max(0.1f, _bodyRotationSpeed);
            _bodyAlignmentSpeed = Mathf.Clamp(_bodyAlignmentSpeed, 0.05f, 2f);
            _alignmentThreshold = Mathf.Clamp(_alignmentThreshold, 0.1f, 0.9f);
            _aimDistance = Mathf.Max(1f, _aimDistance);
            _headYawLimit = Mathf.Clamp(_headYawLimit, 0f, 180f);
            _maxPitchUp = Mathf.Clamp(_maxPitchUp, 0f, 90f);
            _maxPitchDown = Mathf.Clamp(_maxPitchDown, 0f, 90f);
        }

        #endregion

        #region 公共 API (Public API)

        /// <summary>
        /// 设置视角灵敏度。
        /// </summary>
        public void SetSensitivity(float sensitivity)
        {
            _lookSensitivity = Mathf.Max(0.1f, sensitivity);
        }
        
        /// <summary>
        /// 设置是否反转Y轴。
        /// </summary>
        public void SetInvertY(bool invert)
        {
            _invertY = invert;
        }
        
        /// <summary>
        /// 将视角重置为与身体方向一致。
        /// </summary>
        public void ResetView()
        {
            _referenceAimDirection = _referenceBodyDirection;
            _currentPitch = CalculateCurrentPitch(CurrentAimDirection);
        }
        
        /// <summary>
        /// 添加一次性的视角偏移（例如：武器后坐力）。
        /// </summary>
        /// <param name="kickAmount">X为水平偏移，Y为垂直偏移。</param>
        public void AddViewKick(Vector2 kickAmount)
        {
            // 注意：后坐力的垂直偏移通常与鼠标输入相反，因此 pitchDelta 为 -kickAmount.y
            ApplyViewRotation(kickAmount.x, -kickAmount.y);
        }
        
        /// <summary>
        /// 获取从头部发出的视角射线（世界坐标）。
        /// </summary>
        public Ray GetViewRay()
        {
            return new Ray(GetHeadPosition(), CurrentAimDirection);
        }

        /// <summary>
        /// 检查一个世界坐标点是否在视野范围内。
        /// </summary>
        /// <param name="worldPosition">要检查的世界坐标点。</param>
        /// <param name="maxAngle">最大视野角度。</param>
        public bool IsInView(Vector3 worldPosition, float maxAngle = 60f)
        {
            Vector3 toTarget = (worldPosition - GetHeadPosition()).normalized;
            return Vector3.Angle(CurrentAimDirection, toTarget) <= maxAngle;
        }
        
        /// <summary>
        /// 强制触发一次重力变换更新。
        /// </summary>
        public void ForceGravityUpdate()
        {
            // TODO: 根据需求实现具体逻辑。
            // 例如，可以无条件地执行一次重力变换计算，忽略角度阈值。
            // UpdateGravityTransform(true); // 可选的强制更新参数
            if (_motor != null)
            {
                // 目前为空，但保留接口以备扩展。
            }
        }
        
        /// <summary>
        /// 获取当前的俯仰角限制（负值为向下，正值为向上）。
        /// </summary>
        public Vector2 GetCurrentPitchLimits()
        {
            return new Vector2(-_maxPitchDown, _maxPitchUp);
        }
        
        /// <summary>
        /// 获取当前俯仰角相对于限制的归一化值（-1到1，0为水平）。
        /// </summary>
        public float GetPitchNormalized()
        {
            if (_currentPitch >= 0)
            {
                return _maxPitchUp > Epsilon ? _currentPitch / _maxPitchUp : 0f;
            }
            return _maxPitchDown > Epsilon ? _currentPitch / _maxPitchDown : 0f;
        }
        
        /// <summary>
        /// 强制身体方向立即对齐到视角方向（用于重生、传送等特殊情况）。
        /// </summary>
        public void ForceBodyAlignment()
        {
            Vector3 aimDirectionHorizontal = Vector3.ProjectOnPlane(CurrentAimDirection, _motor.UpAxis).normalized;
            
            if (aimDirectionHorizontal.magnitude > Epsilon)
            {
                _referenceBodyDirection = (_inverseGravityTransform * aimDirectionHorizontal).normalized;
                _motor.SetTargetYawDirection(aimDirectionHorizontal);
            }
        }
        
        /// <summary>
        /// 获取当前头部与身体的水平角度差（度）。
        /// </summary>
        public float GetHeadBodyAngleDifference()
        {
            Vector3 currentUp = _motor.UpAxis;
            Vector3 aimHorizontal = Vector3.ProjectOnPlane(CurrentAimDirection, currentUp).normalized;
            Vector3 bodyHorizontal = Vector3.ProjectOnPlane(CurrentBodyDirection, currentUp).normalized;
            
            if (aimHorizontal.magnitude < Epsilon || bodyHorizontal.magnitude < Epsilon)
                return 0f;
                
            return Vector3.SignedAngle(bodyHorizontal, aimHorizontal, currentUp);
        }

        #endregion

        #region 核心逻辑 (Core Logic)

        /// <summary>
        /// 初始化或更新重力变换。
        /// </summary>
        private void InitializeGravityTransform()
        {
            Vector3 currentGravityUp = _motor.UpAxis;
            _gravityTransform = Quaternion.FromToRotation(ReferenceGravityUp, currentGravityUp);
            _inverseGravityTransform = Quaternion.Inverse(_gravityTransform);
        }

        /// <summary>
        /// 每帧检查并更新重力变换，仅在重力方向发生显著变化时才重新计算。
        /// </summary>
        private void UpdateGravityTransform()
        {
            Vector3 currentGravityUp = _motor.UpAxis;
            Quaternion newGravityTransform = Quaternion.FromToRotation(ReferenceGravityUp, currentGravityUp);

            // 仅当角度变化超过阈值时才更新，以节省性能
            if (Quaternion.Angle(_gravityTransform, newGravityTransform) > 0.1f)
            {
                _gravityTransform = newGravityTransform;
                _inverseGravityTransform = Quaternion.Inverse(_gravityTransform);
            }
        }

        /// <summary>
        /// 基于当前身体朝向，初始化所有方向向量。
        /// </summary>
        private void InitializeDirections()
        {
            // 将当前世界坐标的身体朝向，通过逆变换转换为参考坐标系中的方向
            _referenceBodyDirection = (_inverseGravityTransform * _playerBody.forward).normalized;
            
            if (_referenceBodyDirection.magnitude < Epsilon)
            {
                _referenceBodyDirection = Vector3.forward; // 安全回退
            }

            // 初始时，视角与身体朝向一致
            ResetView();
            UpdateHorizontalDirections();
        }

        /// <summary>
        /// 处理玩家的视角输入。
        /// </summary>
        private void HandleInput()
        {
            Vector2 lookInput = _playerInput.LookInput * (_lookSensitivity * Time.deltaTime);
            float pitchDelta = _invertY ? lookInput.y : -lookInput.y;
            ApplyViewRotation(lookInput.x, pitchDelta);
        }

        /// <summary>
        /// 根据输入，应用视角旋转。
        /// </summary>
        private void ApplyViewRotation(float yawDelta, float pitchDelta)
        {
            // 水平旋转 (Yaw)
            if (Mathf.Abs(yawDelta) > Epsilon)
            {
                // 使用 Unity 内置方法，更简洁高效
                _referenceAimDirection = Quaternion.AngleAxis(yawDelta, ReferenceGravityUp) * _referenceAimDirection;
            }

            // 垂直旋转 (Pitch)
            if (Mathf.Abs(pitchDelta) > Epsilon)
            {
                Vector3 refRight = Vector3.Cross(ReferenceGravityUp, _referenceAimDirection).normalized;
                Vector3 potentialNewDir = Quaternion.AngleAxis(pitchDelta, refRight) * _referenceAimDirection;

                // 在应用旋转前，检查是否会超出俯仰角限制
                if (IsWithinPitchLimits(_gravityTransform * potentialNewDir))
                {
                    _referenceAimDirection = potentialNewDir;
                }
            }

            _referenceAimDirection.Normalize();
            _currentPitch = CalculateCurrentPitch(CurrentAimDirection);
        }
        
        /// <summary>
        /// 更新身体旋转，使其跟随头部朝向。
        /// </summary>
        private void UpdateBodyRotation()
        {
            Vector3 currentUp = _motor.UpAxis;
            
            // 计算视线和身体在当前重力平面上的投影
            Vector3 aimHorizontal = Vector3.ProjectOnPlane(CurrentAimDirection, currentUp).normalized;
            if (aimHorizontal.magnitude < Epsilon) return; // 无法判断水平朝向（例如直视天顶）

            Vector3 bodyHorizontal = Vector3.ProjectOnPlane(CurrentBodyDirection, currentUp).normalized;
            if (bodyHorizontal.magnitude < Epsilon) bodyHorizontal = aimHorizontal; // 如果身体方向无效，则直接对齐

            float angleDiff = Vector3.SignedAngle(bodyHorizontal, aimHorizontal, currentUp);

            // 当头身角度差超过限制时，身体强制跟上
            if (Mathf.Abs(angleDiff) > _headYawLimit)
            {
                float overflowAngle = angleDiff - Mathf.Sign(angleDiff) * _headYawLimit;
                float rotationAmount = overflowAngle * _bodyRotationSpeed * Time.deltaTime;
                Vector3 newBodyDirWorld = Quaternion.AngleAxis(rotationAmount, currentUp) * bodyHorizontal;
                
                _referenceBodyDirection = (_inverseGravityTransform * newBodyDirWorld).normalized;
                _motor.SetTargetYawDirection(newBodyDirWorld);
            }
            // 当角度差小于一定阈值时，身体缓慢对齐头部
            else if (Mathf.Abs(angleDiff) < _headYawLimit * _alignmentThreshold)
            {
                float alignAmount = _bodyRotationSpeed * _bodyAlignmentSpeed * Time.deltaTime;
                Vector3 newBodyDirWorld = Vector3.Slerp(bodyHorizontal, aimHorizontal, alignAmount).normalized;

                _referenceBodyDirection = (_inverseGravityTransform * newBodyDirWorld).normalized;
                _motor.SetTargetYawDirection(newBodyDirWorld);
            }
        }

        /// <summary>
        /// 更新瞄准目标点的位置。
        /// </summary>
        private void UpdateAimTargetPosition()
        {
            if (_aimTarget == null) return;
            
            _aimTarget.position = GetHeadPosition() + CurrentAimDirection * _aimDistance;
        }

        /// <summary>
        /// 更新用于角色移动的水平方向向量。
        /// </summary>
        private void UpdateHorizontalDirections()
        {
            Vector3 currentUp = _motor.UpAxis;
            
            // 移动的前进方向，基于瞄准方向在重力平面上的投影
            HorizontalForwardDirection = Vector3.ProjectOnPlane(CurrentAimDirection, currentUp).normalized;
            
            // 如果玩家直视天顶或地心，前进方向可能为零向量，此时提供一个基于身体朝向的回退方案
            if (HorizontalForwardDirection.magnitude < Epsilon)
            {
                HorizontalForwardDirection = Vector3.ProjectOnPlane(CurrentBodyDirection, currentUp).normalized;
            }

            // 右方向垂直于前进方向和上方向
            HorizontalRightDirection = Vector3.Cross(currentUp, HorizontalForwardDirection).normalized;
        }
        
        #endregion

        #region 辅助方法 (Helper Methods)

        /// <summary>
        /// 获取头部在世界空间中的位置。
        /// </summary>
        private Vector3 GetHeadPosition()
        {
            // 头部位置受身体旋转影响，以模拟真实的头部运动
            return _playerBody.position + _motor.GetBodyRotation() * _headOffset;
        }

        /// <summary>
        /// 检查给定的方向是否在允许的俯仰范围内（重力自适应）。
        /// </summary>
        private bool IsWithinPitchLimits(Vector3 worldDirection)
        {
            Vector3 gravityUp = _motor.UpAxis;
            Vector3 directionOnPlane = Vector3.ProjectOnPlane(worldDirection, gravityUp);

            // 当视线垂直于重力平面时（直上或直下）
            if (directionOnPlane.magnitude < Epsilon)
            {
                float upDot = Vector3.Dot(worldDirection, gravityUp);
                if (upDot > 0) return _maxPitchUp >= 90f - Epsilon;
                return _maxPitchDown >= 90f - Epsilon;
            }

            // 计算俯仰角
            float pitchAngle = Vector3.SignedAngle(directionOnPlane, worldDirection, Vector3.Cross(gravityUp, directionOnPlane));
            
            return pitchAngle <= _maxPitchUp && pitchAngle >= -_maxPitchDown;
        }

        /// <summary>
        /// 计算给定方向的俯仰角。
        /// </summary>
        private float CalculateCurrentPitch(Vector3 worldDirection)
        {
            Vector3 gravityUp = _motor.UpAxis;
            Vector3 directionOnPlane = Vector3.ProjectOnPlane(worldDirection, gravityUp);

            if (directionOnPlane.magnitude < Epsilon)
            {
                return Vector3.Dot(worldDirection, gravityUp) > 0 ? 90f : -90f;
            }
            
            return Vector3.SignedAngle(directionOnPlane, worldDirection, Vector3.Cross(gravityUp, directionOnPlane));
        }

        #endregion
        
        #region 调试 (Debugging)
        
        private void UpdateDebugInfo()
        {
            // 每 30 帧更新一次，避免刷屏
            if (Time.frameCount % 30 != 0) return;

            var info = $"<b>[PlayerView-GravityAdaptive]</b>\n" +
                       $"Pitch: {_currentPitch:F1}° (Limits: [{-_maxPitchDown:F0}, +{_maxPitchUp:F0}])\n" +
                       $"Gravity Up: {_motor.UpAxis:F2}\n" +
                       $"Aim Direction (World): {CurrentAimDirection:F2}\n" +
                       $"Body Direction (World): {CurrentBodyDirection:F2}\n" +
                       $"Head-Body Angle: {GetHeadBodyAngleDifference():F1}°\n" +
                       $"Move Forward: {HorizontalForwardDirection:F2}\n" +
                       $"Move Right: {HorizontalRightDirection:F2}";
            Debug.Log(info);
        }
        
        private void OnDrawGizmosSelected()
        {
            if (!_showDebugGizmos || _playerBody == null) return;

            Vector3 headPos = Application.isPlaying ? GetHeadPosition() : _playerBody.position + _headOffset;
            Vector3 bodyPos = _playerBody.position;
            
            // 瞄准方向 (红色)
            Gizmos.color = Color.red;
            Gizmos.DrawLine(headPos, headPos + CurrentAimDirection * 3f);
            
            // 身体朝向 (蓝色)
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(bodyPos, bodyPos + CurrentBodyDirection * 2.5f);
            
            // 水平移动方向 (白色/灰色)
            Gizmos.color = Color.white;
            Gizmos.DrawLine(bodyPos, bodyPos + HorizontalForwardDirection * 2f);
            Gizmos.color = Color.gray;
            Gizmos.DrawLine(bodyPos, bodyPos + HorizontalRightDirection * 2f);
            
            // Aim Target (黄色)
            if (_aimTarget != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(_aimTarget.position, 0.2f);
                Gizmos.color = new Color(Color.yellow.r, Color.yellow.g, Color.yellow.b, 0.5f);
                Gizmos.DrawLine(headPos, _aimTarget.position);
            }
        }
        
        #endregion
    }
}