using UnityEngine;
using System.Collections.Generic;
using ExitGames.Client.Photon.StructWrapping;

namespace DWHITE.Audio
{
    /// <summary>
    /// Player音效管理器 - 全局音效系统管理
    /// 基于单例模式，管理所有Player音效相关的全局设置
    /// 支持音效池化、全局音量控制、音效统计等功能
    /// </summary>
    public class PlayerAudioManager : Singleton<PlayerAudioManager>
    {
        #region 配置
        
        [Header("全局音效设置")]
        [SerializeField] private float _globalVolumeMultiplier = 1f;
        [SerializeField] private bool _enableAudio = true;
        [SerializeField] private bool _enable3DAudio = true;
        
        [Header("音效池化")]
        [SerializeField] private int _audioSourcePoolSize = 10;
        [SerializeField] private bool _enablePooling = true;
        
        [Header("调试")]
        [SerializeField] private bool _showDebugInfo = false;
        [SerializeField] private bool _showAudioStats = false;
        
        #endregion
        
        #region 状态变量
        
        // 注册的音效控制器
        private List<PlayerAudioController> _registeredControllers = new List<PlayerAudioController>();
        
        // AudioSource对象池
        private Queue<AudioSource> _audioSourcePool = new Queue<AudioSource>();
        private List<AudioSource> _activeAudioSources = new List<AudioSource>();
        
        // 音效统计
        private int _totalAudioClipsPlayed = 0;
        private int _audioClipsPlayedThisFrame = 0;
        
        #endregion
        
        #region 属性
        
        /// <summary>全局音量倍率</summary>
        public float GlobalVolumeMultiplier 
        { 
            get => _globalVolumeMultiplier; 
            set => SetGlobalVolumeMultiplier(value); 
        }
        
        /// <summary>是否启用音效</summary>
        public bool EnableAudio 
        { 
            get => _enableAudio; 
            set => SetEnableAudio(value); 
        }
        
        /// <summary>是否启用3D音效</summary>
        public bool Enable3DAudio 
        { 
            get => _enable3DAudio; 
            set => SetEnable3DAudio(value); 
        }
        
        /// <summary>注册的音效控制器数量</summary>
        public int RegisteredControllersCount => _registeredControllers.Count;
        
        /// <summary>音效播放统计</summary>
        public int TotalAudioClipsPlayed => _totalAudioClipsPlayed;
        
        #endregion
        
        #region Unity生命周期
        
        protected override void Awake()
        {
            base.Awake();
            
            // 初始化音效池
            if (_enablePooling)
                InitializeAudioPool();
        }
        
        private void Update()
        {
            // 重置每帧统计
            _audioClipsPlayedThisFrame = 0;
            
            // 清理已完成的音效
            CleanupFinishedAudioSources();
        }
        
        private void LateUpdate()
        {
            // 显示音效统计
            if (_showAudioStats)
                UpdateAudioStats();
        }
        
        #endregion
        
        #region 音效控制器管理
        
        /// <summary>
        /// 注册音效控制器
        /// </summary>
        public void RegisterAudioController(PlayerAudioController controller)
        {
            if (controller == null || _registeredControllers.Contains(controller))
                return;
                
            _registeredControllers.Add(controller);
            
            // 应用全局设置
            ApplyGlobalSettingsToController(controller);
            
            if (_showDebugInfo)
                Debug.Log($"[PlayerAudioManager] 注册音效控制器: {controller.name}");
        }
        
        /// <summary>
        /// 取消注册音效控制器
        /// </summary>
        public void UnregisterAudioController(PlayerAudioController controller)
        {
            if (controller == null)
                return;
                
            _registeredControllers.Remove(controller);
            
            if (_showDebugInfo)
                Debug.Log($"[PlayerAudioManager] 取消注册音效控制器: {controller.name}");
        }
        
        /// <summary>
        /// 应用全局设置到指定控制器
        /// </summary>
        private void ApplyGlobalSettingsToController(PlayerAudioController controller)
        {
            if (controller == null) return;
            
            controller.SetGlobalVolumeMultiplier(_globalVolumeMultiplier);
            
            if (!_enableAudio)
                controller.StopAllAudio();
        }
        
        #endregion
        
        #region 全局音效控制
        
        /// <summary>
        /// 设置全局音量倍率
        /// </summary>
        public void SetGlobalVolumeMultiplier(float multiplier)
        {
            _globalVolumeMultiplier = Mathf.Clamp01(multiplier);
            
            // 应用到所有注册的控制器
            foreach (var controller in _registeredControllers)
            {
                if (controller != null)
                    controller.SetGlobalVolumeMultiplier(_globalVolumeMultiplier);
            }
            
            if (_showDebugInfo)
                Debug.Log($"[PlayerAudioManager] 设置全局音量倍率: {_globalVolumeMultiplier:F2}");
        }
        
        /// <summary>
        /// 设置是否启用音效
        /// </summary>
        public void SetEnableAudio(bool enable)
        {
            _enableAudio = enable;
            
            if (!enable)
            {
                // 停止所有音效
                foreach (var controller in _registeredControllers)
                {
                    if (controller != null)
                        controller.StopAllAudio();
                }
            }
            
            if (_showDebugInfo)
                Debug.Log($"[PlayerAudioManager] 音效启用状态: {_enableAudio}");
        }
        
        /// <summary>
        /// 设置是否启用3D音效
        /// </summary>
        public void SetEnable3DAudio(bool enable)
        {
            _enable3DAudio = enable;
            
            // 这里可以添加切换2D/3D音效的逻辑
            // 例如调整所有AudioSource的spatialBlend参数
            
            if (_showDebugInfo)
                Debug.Log($"[PlayerAudioManager] 3D音效启用状态: {_enable3DAudio}");
        }
        
        /// <summary>
        /// 停止所有Player音效
        /// </summary>
        public void StopAllPlayerAudio()
        {
            foreach (var controller in _registeredControllers)
            {
                if (controller != null)
                    controller.StopAllAudio();
            }
            
            // 停止池化的音效
            foreach (var audioSource in _activeAudioSources)
            {
                if (audioSource != null && audioSource.isPlaying)
                    audioSource.Stop();
            }
            
            if (_showDebugInfo)
                Debug.Log("[PlayerAudioManager] 停止所有Player音效");
        }
        
        #endregion
        
        #region 音效池化系统
        
        /// <summary>
        /// 初始化音效池
        /// </summary>
        private void InitializeAudioPool()
        {
            for (int i = 0; i < _audioSourcePoolSize; i++)
            {
                CreatePooledAudioSource();
            }
            
            if (_showDebugInfo)
                Debug.Log($"[PlayerAudioManager] 初始化音效池，大小: {_audioSourcePoolSize}");
        }
        
        /// <summary>
        /// 创建池化的AudioSource
        /// </summary>
        private void CreatePooledAudioSource()
        {
            GameObject audioObj = new GameObject("PooledAudioSource");
            audioObj.transform.SetParent(transform);
            
            AudioSource audioSource = audioObj.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            
            _audioSourcePool.Enqueue(audioSource);
        }
        
        /// <summary>
        /// 从池中获取AudioSource
        /// </summary>
        public AudioSource GetPooledAudioSource()
        {
            if (!_enablePooling) return null;
            
            AudioSource audioSource;
            
            if (_audioSourcePool.Count > 0)
            {
                audioSource = _audioSourcePool.Dequeue();
            }
            else
            {
                // 池已空，创建新的AudioSource
                CreatePooledAudioSource();
                audioSource = _audioSourcePool.Dequeue();
                
                if (_showDebugInfo)
                    Debug.Log("[PlayerAudioManager] 音效池已空，创建新的AudioSource");
            }
            
            _activeAudioSources.Add(audioSource);
            return audioSource;
        }
        
        /// <summary>
        /// 归还AudioSource到池中
        /// </summary>
        public void ReturnAudioSourceToPool(AudioSource audioSource)
        {
            if (!_enablePooling || audioSource == null) return;
            
            // 停止播放并重置
            audioSource.Stop();
            audioSource.clip = null;
            audioSource.volume = 1f;
            audioSource.pitch = 1f;
            
            // 从活跃列表移除
            _activeAudioSources.Remove(audioSource);
            
            // 归还到池中
            _audioSourcePool.Enqueue(audioSource);
        }
        
        /// <summary>
        /// 清理已完成播放的AudioSource
        /// </summary>
        private void CleanupFinishedAudioSources()
        {
            for (int i = _activeAudioSources.Count - 1; i >= 0; i--)
            {
                var audioSource = _activeAudioSources[i];
                
                if (audioSource == null || !audioSource.isPlaying)
                {
                    ReturnAudioSourceToPool(audioSource);
                }
            }
        }
        
        #endregion
        
        #region 便捷播放方法
        
        /// <summary>
        /// 播放一次性音效（使用池化）
        /// </summary>
        public void PlayOneShot(AudioClip clip, Vector3 position, float volume = 1f, float pitch = 1f)
        {
            if (!_enableAudio || clip == null) return;
            
            AudioSource audioSource = GetPooledAudioSource();
            if (audioSource == null)
            {
                // 如果池化不可用，使用静态方法
                AudioSource.PlayClipAtPoint(clip, position, volume * _globalVolumeMultiplier);
                _totalAudioClipsPlayed++;
                _audioClipsPlayedThisFrame++;
                return;
            }
            
            // 配置AudioSource
            audioSource.transform.position = position;
            audioSource.clip = clip;
            audioSource.volume = volume * _globalVolumeMultiplier;
            audioSource.pitch = pitch;
            audioSource.spatialBlend = _enable3DAudio ? 1f : 0f;
            audioSource.Play();
            
            _totalAudioClipsPlayed++;
            _audioClipsPlayedThisFrame++;
            
            if (_showDebugInfo)
                Debug.Log($"[PlayerAudioManager] 播放一次性音效: {clip.name}");
        }
        
        /// <summary>
        /// 播放随机音效
        /// </summary>
        public void PlayRandomOneShot(AudioClip[] clips, Vector3 position, float volume = 1f, float pitchVariation = 0.1f)
        {
            if (clips == null || clips.Length == 0) return;
            
            AudioClip randomClip = clips[Random.Range(0, clips.Length)];
            float randomPitch = 1f + Random.Range(-pitchVariation, pitchVariation);
            
            PlayOneShot(randomClip, position, volume, randomPitch);
        }
        
        #endregion
        
        #region 音效统计和调试
        
        /// <summary>
        /// 更新音效统计
        /// </summary>
        private void UpdateAudioStats()
        {
            // 这里可以添加更多统计信息的收集
        }
        
        /// <summary>
        /// 获取音效统计信息
        /// </summary>
        public string GetAudioStats()
        {
            return $"注册控制器: {_registeredControllers.Count}\n" +
                   $"总播放次数: {_totalAudioClipsPlayed}\n" +
                   $"本帧播放: {_audioClipsPlayedThisFrame}\n" +
                   $"活跃音源: {_activeAudioSources.Count}\n" +
                   $"池中音源: {_audioSourcePool.Count}";
        }
        
        #endregion
        
        #region 配置保存/加载
        
        /// <summary>
        /// 保存音效设置
        /// </summary>
        public void SaveAudioSettings()
        {
            PlayerPrefs.SetFloat("PlayerAudio_GlobalVolume", _globalVolumeMultiplier);
            PlayerPrefs.SetInt("PlayerAudio_EnableAudio", _enableAudio ? 1 : 0);
            PlayerPrefs.SetInt("PlayerAudio_Enable3DAudio", _enable3DAudio ? 1 : 0);
            PlayerPrefs.Save();
            
            if (_showDebugInfo)
                Debug.Log("[PlayerAudioManager] 音效设置已保存");
        }
        
        /// <summary>
        /// 加载音效设置
        /// </summary>
        public void LoadAudioSettings()
        {
            _globalVolumeMultiplier = PlayerPrefs.GetFloat("PlayerAudio_GlobalVolume", 1f);
            _enableAudio = PlayerPrefs.GetInt("PlayerAudio_EnableAudio", 1) == 1;
            _enable3DAudio = PlayerPrefs.GetInt("PlayerAudio_Enable3DAudio", 1) == 1;
            
            // 应用设置
            SetGlobalVolumeMultiplier(_globalVolumeMultiplier);
            SetEnableAudio(_enableAudio);
            SetEnable3DAudio(_enable3DAudio);
            
            if (_showDebugInfo)
                Debug.Log("[PlayerAudioManager] 音效设置已加载");
        }
        
        #endregion
        
        #region Inspector GUI
        
#if UNITY_EDITOR
        private void OnGUI()
        {
            if (!_showAudioStats || !Application.isPlaying) return;
            
            GUILayout.BeginArea(new Rect(10, 120, 300, 200));
            GUILayout.BeginVertical("box");
            
            GUILayout.Label("Player Audio Manager Stats", GUI.skin.label);
            GUILayout.Space(5);
            
            GUILayout.Label(GetAudioStats());
            
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }
#endif
        
        #endregion
    }
}
