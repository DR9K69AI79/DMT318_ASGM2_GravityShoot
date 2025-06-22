using UnityEngine;

namespace DWHITE
{
    /// <summary>
    /// Player音效配置数据 ScriptableObject
    /// 数据驱动的音效参数配置，支持设计师调参
    /// </summary>
    [CreateAssetMenu(fileName = "NewPlayerAudioData", menuName = "GravityShoot/Player Audio Data")]
    public class PlayerAudioData : ScriptableObject
    {
        [Header("脚步音效")]
        [SerializeField] private AudioClip[] _footstepClips;
        [SerializeField] private float _footstepVolume = 0.8f;
        [SerializeField] private Vector2 _footstepPitchRange = new Vector2(0.9f, 1.1f);
        [SerializeField] private float _footstepSpeedThreshold = 0.1f;
        [SerializeField] private float _footstepInterval = 0.5f;
        
        [Header("跳跃音效")]
        [SerializeField] private AudioClip _jumpClip;
        [SerializeField] private float _jumpVolume = 1f;
        [SerializeField] private Vector2 _jumpPitchRange = new Vector2(0.95f, 1.05f);
        
        [Header("着地音效")]
        [SerializeField] private AudioClip[] _landingClips;
        [SerializeField] private float _landingVolume = 1f;
        [SerializeField] private Vector2 _landingPitchRange = new Vector2(0.9f, 1.1f);
        [SerializeField] private float _hardLandingSpeedThreshold = 15f;
        
        [Header("奔跑音效")]
        [SerializeField] private AudioClip[] _sprintFootstepClips;
        [SerializeField] private float _sprintFootstepVolume = 0.9f;
        [SerializeField] private float _sprintFootstepInterval = 0.3f;
        
        [Header("环境音效")]
        [SerializeField] private AudioClip _airWhooshClip;
        [SerializeField] private float _airWhooshVolume = 0.6f;
        [SerializeField] private float _airSpeedThreshold = 10f;
        
        [Header("呼吸音效")]
        [SerializeField] private AudioClip[] _breathingClips;
        [SerializeField] private float _breathingVolume = 0.4f;
        [SerializeField] private float _breathingInterval = 3f;
        [SerializeField] private float _sprintBreathingInterval = 1.5f;
        
        [Header("3D音效设置")]
        [SerializeField] private float _spatialBlend = 0f; // 0=2D, 1=3D
        [SerializeField] private float _dopplerLevel = 0.5f;
        [SerializeField] private float _minDistance = 1f;
        [SerializeField] private float _maxDistance = 10f;
        [SerializeField] private AnimationCurve _rolloffCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
        
        #region 属性访问器
        
        // 脚步音效
        public AudioClip[] FootstepClips => _footstepClips;
        public float FootstepVolume => _footstepVolume;
        public Vector2 FootstepPitchRange => _footstepPitchRange;
        public float FootstepSpeedThreshold => _footstepSpeedThreshold;
        public float FootstepInterval => _footstepInterval;
        
        // 跳跃音效
        public AudioClip JumpClip => _jumpClip;
        public float JumpVolume => _jumpVolume;
        public Vector2 JumpPitchRange => _jumpPitchRange;
        
        // 着地音效
        public AudioClip[] LandingClips => _landingClips;
        public float LandingVolume => _landingVolume;
        public Vector2 LandingPitchRange => _landingPitchRange;
        public float HardLandingSpeedThreshold => _hardLandingSpeedThreshold;
        
        // 奔跑音效
        public AudioClip[] SprintFootstepClips => _sprintFootstepClips;
        public float SprintFootstepVolume => _sprintFootstepVolume;
        public float SprintFootstepInterval => _sprintFootstepInterval;
        
        // 环境音效
        public AudioClip AirWhooshClip => _airWhooshClip;
        public float AirWhooshVolume => _airWhooshVolume;
        public float AirSpeedThreshold => _airSpeedThreshold;
        
        // 呼吸音效
        public AudioClip[] BreathingClips => _breathingClips;
        public float BreathingVolume => _breathingVolume;
        public float BreathingInterval => _breathingInterval;
        public float SprintBreathingInterval => _sprintBreathingInterval;
        
        // 3D音效设置
        public float SpatialBlend => _spatialBlend;
        public float DopplerLevel => _dopplerLevel;
        public float MinDistance => _minDistance;
        public float MaxDistance => _maxDistance;
        public AnimationCurve RolloffCurve => _rolloffCurve;
        
        #endregion
        
        #region 便捷方法
        
        /// <summary>
        /// 获取随机脚步音效
        /// </summary>
        public AudioClip GetRandomFootstepClip()
        {
            if (_footstepClips == null || _footstepClips.Length == 0) return null;
            return _footstepClips[Random.Range(0, _footstepClips.Length)];
        }
        
        /// <summary>
        /// 获取随机奔跑脚步音效
        /// </summary>
        public AudioClip GetRandomSprintFootstepClip()
        {
            if (_sprintFootstepClips == null || _sprintFootstepClips.Length == 0) 
                return GetRandomFootstepClip(); // 回退到普通脚步声
            return _sprintFootstepClips[Random.Range(0, _sprintFootstepClips.Length)];
        }
        
        /// <summary>
        /// 获取随机着地音效
        /// </summary>
        public AudioClip GetRandomLandingClip()
        {
            if (_landingClips == null || _landingClips.Length == 0) return null;
            return _landingClips[Random.Range(0, _landingClips.Length)];
        }
        
        /// <summary>
        /// 获取随机呼吸音效
        /// </summary>
        public AudioClip GetRandomBreathingClip()
        {
            if (_breathingClips == null || _breathingClips.Length == 0) return null;
            return _breathingClips[Random.Range(0, _breathingClips.Length)];
        }
        
        /// <summary>
        /// 根据速度获取随机音调
        /// </summary>
        public float GetRandomPitch(Vector2 pitchRange, float speedMultiplier = 1f)
        {
            float basePitch = Random.Range(pitchRange.x, pitchRange.y);
            return basePitch * speedMultiplier;
        }
        
        #endregion
        
        #region 验证
        
#if UNITY_EDITOR
        private void OnValidate()
        {
            // 确保音量在合理范围内
            _footstepVolume = Mathf.Clamp01(_footstepVolume);
            _jumpVolume = Mathf.Clamp01(_jumpVolume);
            _landingVolume = Mathf.Clamp01(_landingVolume);
            _sprintFootstepVolume = Mathf.Clamp01(_sprintFootstepVolume);
            _airWhooshVolume = Mathf.Clamp01(_airWhooshVolume);
            _breathingVolume = Mathf.Clamp01(_breathingVolume);
            
            // 确保音调范围合理
            _footstepPitchRange.x = Mathf.Max(0.1f, _footstepPitchRange.x);
            _footstepPitchRange.y = Mathf.Max(_footstepPitchRange.x, _footstepPitchRange.y);
            _jumpPitchRange.x = Mathf.Max(0.1f, _jumpPitchRange.x);
            _jumpPitchRange.y = Mathf.Max(_jumpPitchRange.x, _jumpPitchRange.y);
            _landingPitchRange.x = Mathf.Max(0.1f, _landingPitchRange.x);
            _landingPitchRange.y = Mathf.Max(_landingPitchRange.x, _landingPitchRange.y);
            
            // 确保时间间隔合理
            _footstepInterval = Mathf.Max(0.1f, _footstepInterval);
            _sprintFootstepInterval = Mathf.Max(0.1f, _sprintFootstepInterval);
            _breathingInterval = Mathf.Max(0.5f, _breathingInterval);
            _sprintBreathingInterval = Mathf.Max(0.5f, _sprintBreathingInterval);
            
            // 确保阈值合理
            _footstepSpeedThreshold = Mathf.Max(0f, _footstepSpeedThreshold);
            _hardLandingSpeedThreshold = Mathf.Max(0f, _hardLandingSpeedThreshold);
            _airSpeedThreshold = Mathf.Max(0f, _airSpeedThreshold);
            
            // 确保3D音效设置合理
            _spatialBlend = Mathf.Clamp01(_spatialBlend);
            _dopplerLevel = Mathf.Clamp(_dopplerLevel, 0f, 5f);
            _minDistance = Mathf.Max(0.1f, _minDistance);
            _maxDistance = Mathf.Max(_minDistance, _maxDistance);
        }
#endif
        
        #endregion
    }
}
