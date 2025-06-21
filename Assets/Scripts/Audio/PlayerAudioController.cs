using UnityEngine;


namespace DWHITE.Audio
{
    /// <summary>
    /// Player音效控制器 - 基于事件驱动的音效系统
    /// 订阅PlayerStateManager的状态变化，独立管理角色音效
    /// 基于最小可行原则，专注核心音效功能
    /// </summary>
    public partial class PlayerAudioController : MonoBehaviour
    {
        #region 配置与引用
        
        [Header("音效配置")]
        [SerializeField] private GameObject _playerRoot; 
        [SerializeField] private PlayerAudioData _audioData;
        
        [Header("AudioSource组件")]
        [SerializeField] private AudioSource _footstepAudioSource;
        [SerializeField] private AudioSource _jumpAudioSource;
        [SerializeField] private AudioSource _landingAudioSource;
        [SerializeField] private AudioSource _environmentAudioSource;
        [SerializeField] private AudioSource _breathingAudioSource;
        
        [Header("调试")]
        [SerializeField] private bool _showDebugInfo = false;
        
        #endregion
        
        #region 组件引用
        
        private PlayerStateManager _stateManager;
        private NetworkPlayerController _networkPlayerController;
        private bool _isNetworkPlayer;
        
        #endregion
        
        #region 状态变量
        
        // 脚步音效状态
        private float _lastFootstepTime;
        private bool _isPlayingFootsteps;
        
        // 环境音效状态
        private bool _isPlayingAirWhoosh;
        
        // 呼吸音效状态
        private float _lastBreathingTime;
        private Coroutine _breathingCoroutine;
        
        // 缓存状态（用于状态变化检测）
        private bool _wasGrounded = true;
        private bool _wasMoving = false;
        private bool _wasSprinting = false;
        private float _lastLandingSpeed = 0f;
        
        #endregion
        
        #region Unity生命周期
        
        private void Awake()
        {
            // 获取组件引用
            _stateManager = _playerRoot.GetComponent<PlayerStateManager>();
            _networkPlayerController = _playerRoot.GetComponent<NetworkPlayerController>();
            _isNetworkPlayer = _networkPlayerController != null;
            
            // 验证配置
            ValidateConfiguration();
            
            // 初始化AudioSource
            InitializeAudioSources();
        }
        
        private void OnEnable()
        {
            // 订阅状态变化事件
            SubscribeToStateEvents();
        }
        
        private void OnDisable()
        {
            // 取消订阅事件
            UnsubscribeFromStateEvents();
            
            // 停止所有协程
            StopAllCoroutines();
        }
        
        private void Update()
        {
            if (_audioData == null || _stateManager == null) return;
            
            // 更新持续性音效
            UpdateContinuousAudio();
        }
        
        #endregion
        
        #region 事件订阅管理
        
        private void SubscribeToStateEvents()
        {
            if (_stateManager == null) return;
            
            // 订阅通用状态变化
            PlayerStateManager.OnStateChanged += HandleStateChanged;
            
            // 订阅特定状态变化
            PlayerStateManager.OnGroundStateChanged += HandleGroundStateChanged;
            PlayerStateManager.OnMovementChanged += HandleMovementChanged;
            
            if (_showDebugInfo)
                Debug.Log("[PlayerAudioController] 已订阅状态变化事件");
        }
        
        private void UnsubscribeFromStateEvents()
        {
            // 取消订阅通用状态变化
            PlayerStateManager.OnStateChanged -= HandleStateChanged;
            
            // 取消订阅特定状态变化
            PlayerStateManager.OnGroundStateChanged -= HandleGroundStateChanged;
            PlayerStateManager.OnMovementChanged -= HandleMovementChanged;
            
            if (_showDebugInfo)
                Debug.Log("[PlayerAudioController] 已取消订阅状态变化事件");
        }
        
        #endregion
        
        #region 状态事件处理器
        
        private void HandleStateChanged(PlayerStateChangedEventArgs args)
        {
            // 通用状态变化处理
            UpdateBreathing(args.CurrentState);
            UpdateEnvironmentAudio(args.CurrentState);
        }
        
        private void HandleGroundStateChanged(PlayerStateChangedEventArgs args)
        {
            var currentState = args.CurrentState;
            var previousState = args.PreviousState;
            
            // 检测着地
            if (currentState.isGrounded && !previousState.isGrounded)
            {
                PlayLandingSound(previousState.velocity.magnitude);
                
                if (_showDebugInfo)
                    Debug.Log($"[PlayerAudioController] 检测到着地，速度: {previousState.velocity.magnitude:F2}");
            }
            
            // 检测离地（跳跃）
            if (!currentState.isGrounded && previousState.isGrounded && currentState.isJumping)
            {
                PlayJumpSound();
                
                if (_showDebugInfo)
                    Debug.Log("[PlayerAudioController] 检测到跳跃");
            }
        }
        
        private void HandleMovementChanged(PlayerStateChangedEventArgs args)
        {
            var currentState = args.CurrentState;
            
            // 更新脚步声
            UpdateFootsteps(currentState);
        }
        
        #endregion
        
        #region 音效播放方法
        
        /// <summary>
        /// 播放跳跃音效
        /// </summary>
        private void PlayJumpSound()
        {
            if (_audioData.JumpClip == null || _jumpAudioSource == null) return;
            
            _jumpAudioSource.clip = _audioData.JumpClip;
            _jumpAudioSource.volume = _audioData.JumpVolume;
            _jumpAudioSource.pitch = _audioData.GetRandomPitch(_audioData.JumpPitchRange);
            _jumpAudioSource.Play();
            
            if (_showDebugInfo)
                Debug.Log("[PlayerAudioController] 播放跳跃音效");
        }
        
        /// <summary>
        /// 播放着地音效
        /// </summary>
        private void PlayLandingSound(float landingSpeed)
        {
            if (_landingAudioSource == null) return;
            
            AudioClip landingClip = _audioData.GetRandomLandingClip();
            if (landingClip == null) return;
            
            // 根据着地速度调整音量和音调
            bool isHardLanding = landingSpeed > _audioData.HardLandingSpeedThreshold;
            float volumeMultiplier = isHardLanding ? 1.2f : 1f;
            float pitchMultiplier = isHardLanding ? 0.9f : 1f;
            
            _landingAudioSource.clip = landingClip;
            _landingAudioSource.volume = _audioData.LandingVolume * volumeMultiplier;
            _landingAudioSource.pitch = _audioData.GetRandomPitch(_audioData.LandingPitchRange, pitchMultiplier);
            _landingAudioSource.Play();
            
            if (_showDebugInfo)
                Debug.Log($"[PlayerAudioController] 播放着地音效，速度: {landingSpeed:F2}, 重着地: {isHardLanding}");
        }
        
        /// <summary>
        /// 更新脚步声
        /// </summary>
        private void UpdateFootsteps(PlayerStateData state)
        {
            if (_footstepAudioSource == null) return;
            
            bool shouldPlayFootsteps = state.isGrounded && 
                                     state.speed > _audioData.FootstepSpeedThreshold;
            
            if (shouldPlayFootsteps)
            {
                float footstepInterval = state.isSprinting ? 
                    _audioData.SprintFootstepInterval : _audioData.FootstepInterval;
                
                // 根据速度调整脚步间隔
                float speedMultiplier = Mathf.Clamp(state.speed / 8f, 0.5f, 2f);
                footstepInterval /= speedMultiplier;
                
                if (Time.time - _lastFootstepTime >= footstepInterval)
                {
                    PlayFootstepSound(state.isSprinting, speedMultiplier);
                    _lastFootstepTime = Time.time;
                }
                
                _isPlayingFootsteps = true;
            }
            else
            {
                _isPlayingFootsteps = false;
            }
        }
        
        /// <summary>
        /// 播放脚步音效
        /// </summary>
        private void PlayFootstepSound(bool isSprinting, float speedMultiplier)
        {
            AudioClip footstepClip = isSprinting ? 
                _audioData.GetRandomSprintFootstepClip() : 
                _audioData.GetRandomFootstepClip();
                
            if (footstepClip == null) return;
            
            float volume = isSprinting ? 
                _audioData.SprintFootstepVolume : 
                _audioData.FootstepVolume;
            
            _footstepAudioSource.clip = footstepClip;
            _footstepAudioSource.volume = volume;
            _footstepAudioSource.pitch = _audioData.GetRandomPitch(_audioData.FootstepPitchRange, speedMultiplier);
            _footstepAudioSource.Play();
            
            if (_showDebugInfo)
                Debug.Log($"[PlayerAudioController] 播放脚步音效，奔跑: {isSprinting}, 速度倍率: {speedMultiplier:F2}");
        }
        
        /// <summary>
        /// 更新环境音效（如空气阻力声）
        /// </summary>
        private void UpdateEnvironmentAudio(PlayerStateData state)
        {
            if (_environmentAudioSource == null || _audioData.AirWhooshClip == null) return;
            
            bool shouldPlayAirWhoosh = !state.isGrounded && 
                                     state.speed > _audioData.AirSpeedThreshold;
            
            if (shouldPlayAirWhoosh && !_isPlayingAirWhoosh)
            {
                _environmentAudioSource.clip = _audioData.AirWhooshClip;
                _environmentAudioSource.volume = _audioData.AirWhooshVolume;
                _environmentAudioSource.loop = true;
                _environmentAudioSource.Play();
                _isPlayingAirWhoosh = true;
                
                if (_showDebugInfo)
                    Debug.Log("[PlayerAudioController] 开始播放空气阻力音效");
            }
            else if (!shouldPlayAirWhoosh && _isPlayingAirWhoosh)
            {
                _environmentAudioSource.Stop();
                _isPlayingAirWhoosh = false;
                
                if (_showDebugInfo)
                    Debug.Log("[PlayerAudioController] 停止播放空气阻力音效");
            }
            
            // 根据速度调整音量和音调
            if (_isPlayingAirWhoosh)
            {
                float volumeMultiplier = Mathf.Clamp(state.speed / 20f, 0.3f, 1f);
                float pitchMultiplier = Mathf.Clamp(state.speed / 15f, 0.8f, 1.2f);
                
                _environmentAudioSource.volume = _audioData.AirWhooshVolume * volumeMultiplier;
                _environmentAudioSource.pitch = pitchMultiplier;
            }
        }
        
        /// <summary>
        /// 更新呼吸音效
        /// </summary>
        private void UpdateBreathing(PlayerStateData state)
        {
            if (_breathingAudioSource == null) return;
            
            float breathingInterval = state.isSprinting ? 
                _audioData.SprintBreathingInterval : 
                _audioData.BreathingInterval;
            
            if (Time.time - _lastBreathingTime >= breathingInterval)
            {
                PlayBreathingSound(state.isSprinting);
                _lastBreathingTime = Time.time;
            }
        }
        
        /// <summary>
        /// 播放呼吸音效
        /// </summary>
        private void PlayBreathingSound(bool isSprinting)
        {
            AudioClip breathingClip = _audioData.GetRandomBreathingClip();
            if (breathingClip == null) return;
            
            float volumeMultiplier = isSprinting ? 1.2f : 1f;
            
            _breathingAudioSource.clip = breathingClip;
            _breathingAudioSource.volume = _audioData.BreathingVolume * volumeMultiplier;
            _breathingAudioSource.pitch = Random.Range(0.95f, 1.05f);
            _breathingAudioSource.Play();
            
            if (_showDebugInfo)
                Debug.Log($"[PlayerAudioController] 播放呼吸音效，奔跑: {isSprinting}");
        }
        
        #endregion
        
        #region 持续性音效更新
        
        private void UpdateContinuousAudio()
        {
            // 这里可以添加需要每帧更新的音效逻辑
            // 例如根据距离调整音量等
        }
        
        #endregion
        
        #region 初始化和配置
        
        private void ValidateConfiguration()
        {
            if (_audioData == null)
            {
                Debug.LogError($"[PlayerAudioController] {gameObject.name} 缺少 PlayerAudioData 配置");
            }
        }
        
        private void InitializeAudioSources()
        {
            // 如果没有配置AudioSource，自动创建
            if (_footstepAudioSource == null)
                _footstepAudioSource = CreateAudioSource("FootstepAudio");
                
            if (_jumpAudioSource == null)
                _jumpAudioSource = CreateAudioSource("JumpAudio");
                
            if (_landingAudioSource == null)
                _landingAudioSource = CreateAudioSource("LandingAudio");
                
            if (_environmentAudioSource == null)
                _environmentAudioSource = CreateAudioSource("EnvironmentAudio");
                
            if (_breathingAudioSource == null)
                _breathingAudioSource = CreateAudioSource("BreathingAudio");
            
            // 配置AudioSource参数
            ConfigureAudioSource(_footstepAudioSource);
            ConfigureAudioSource(_jumpAudioSource);
            ConfigureAudioSource(_landingAudioSource);
            ConfigureAudioSource(_environmentAudioSource);
            ConfigureAudioSource(_breathingAudioSource);
        }
        
        private AudioSource CreateAudioSource(string name)
        {
            GameObject audioObj = new GameObject(name);
            audioObj.transform.SetParent(transform);
            audioObj.transform.localPosition = Vector3.zero;
            
            return audioObj.AddComponent<AudioSource>();
        }
        
        private void ConfigureAudioSource(AudioSource audioSource)
        {
            if (audioSource == null || _audioData == null) return;
            
            audioSource.spatialBlend = _audioData.SpatialBlend;
            audioSource.dopplerLevel = _audioData.DopplerLevel;
            audioSource.minDistance = _audioData.MinDistance;
            audioSource.maxDistance = _audioData.MaxDistance;
            audioSource.rolloffMode = AudioRolloffMode.Custom;
            audioSource.SetCustomCurve(AudioSourceCurveType.CustomRolloff, _audioData.RolloffCurve);
            audioSource.playOnAwake = false;
        }
        
        #endregion
        
        #region 公共API
        
        /// <summary>
        /// 设置音效数据配置
        /// </summary>
        public void SetAudioData(PlayerAudioData audioData)
        {
            _audioData = audioData;
            
            // 重新配置AudioSource
            if (_audioData != null)
            {
                InitializeAudioSources();
            }
        }
        
        /// <summary>
        /// 设置全局音量倍率
        /// </summary>
        public void SetGlobalVolumeMultiplier(float multiplier)
        {
            multiplier = Mathf.Clamp01(multiplier);
            
            if (_footstepAudioSource != null)
                _footstepAudioSource.volume *= multiplier;
            if (_jumpAudioSource != null)
                _jumpAudioSource.volume *= multiplier;
            if (_landingAudioSource != null)
                _landingAudioSource.volume *= multiplier;
            if (_environmentAudioSource != null)
                _environmentAudioSource.volume *= multiplier;
            if (_breathingAudioSource != null)
                _breathingAudioSource.volume *= multiplier;
        }
        
        /// <summary>
        /// 立即停止所有音效
        /// </summary>
        public void StopAllAudio()
        {
            if (_footstepAudioSource != null && _footstepAudioSource.isPlaying)
                _footstepAudioSource.Stop();
            if (_jumpAudioSource != null && _jumpAudioSource.isPlaying)
                _jumpAudioSource.Stop();
            if (_landingAudioSource != null && _landingAudioSource.isPlaying)
                _landingAudioSource.Stop();
            if (_environmentAudioSource != null && _environmentAudioSource.isPlaying)
                _environmentAudioSource.Stop();
            if (_breathingAudioSource != null && _breathingAudioSource.isPlaying)
                _breathingAudioSource.Stop();
                
            _isPlayingFootsteps = false;
            _isPlayingAirWhoosh = false;
        }
        
        /// <summary>
        /// 手动触发跳跃音效
        /// </summary>
        public void TriggerJumpSound()
        {
            PlayJumpSound();
        }
        
        /// <summary>
        /// 手动触发着地音效
        /// </summary>
        public void TriggerLandingSound(float landingSpeed = 5f)
        {
            PlayLandingSound(landingSpeed);
        }
        
        #endregion
        
        #region 调试功能
        
#if UNITY_EDITOR
        private void OnValidate()
        {
            // 编辑器中验证配置
            if (_audioData != null && Application.isPlaying)
            {
                InitializeAudioSources();
            }
        }
#endif
        
        #endregion
    }
}
