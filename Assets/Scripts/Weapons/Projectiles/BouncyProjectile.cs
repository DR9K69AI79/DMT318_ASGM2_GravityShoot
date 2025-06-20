using UnityEngine;
using Photon.Pun;

namespace DWHITE.Weapons
{
    /// <summary>
    /// 弹跳投射物
    /// 具有强化弹跳能力的投射物
    /// </summary>
    public class BouncyProjectile : StandardProjectile
    {
        [Header("弹跳特效")]
        [SerializeField] private float _bounceSpeedBoost = 1.1f; // 每次弹跳的速度增益
        [SerializeField] private float _maxBounceSpeed = 50f; // 最大弹跳速度
        [SerializeField] private Color _trailColor = Color.cyan;
        [SerializeField] private AnimationCurve _bounceIntensityCurve = AnimationCurve.Linear(0, 1, 1, 0.5f);
        
        [Header("弹跳音效")]
        [SerializeField] private AudioClip[] _bounceVariations;
        [SerializeField] private float _pitchVariation = 0.2f;
        
        [Header("弹跳视觉")]
        [SerializeField] private GameObject _bounceEffectPrefab;
        [SerializeField] private ParticleSystem _trailParticles;
        
        private TrailRenderer _bouncyTrail;
        private int _totalBounces = 0;
        
        protected override void Start()
        {
            base.Start();
            
            // 设置轨迹效果
            _bouncyTrail = GetComponent<TrailRenderer>();
            SetupTrailEffect();
            
            if (_showDebugInfo)
                Debug.Log("[弹跳投射物] 弹跳子弹已发射，准备弹跳！");
        }
        
        /// <summary>
        /// 设置轨迹效果
        /// </summary>
        private void SetupTrailEffect()
        {
            if (_bouncyTrail != null)
            {
                _bouncyTrail.startColor = _trailColor;
                _bouncyTrail.endColor = new Color(_trailColor.r, _trailColor.g, _trailColor.b, 0f);
            }
            
            if (_trailParticles != null)
            {
                var main = _trailParticles.main;
                main.startColor = _trailColor;
            }
        }
        
        protected override void OnBounce(Collision collision, Vector3 newVelocity)
        {
            base.OnBounce(collision, newVelocity);
            
            _totalBounces++;
            
            // 计算弹跳强度（随着弹跳次数递减）
            float bounceIntensity = _bounceIntensityCurve.Evaluate((float)_totalBounces / _maxBounces);
            
            // 应用速度增益
            if (_rigidbody != null && _bounceSpeedBoost > 1f)
            {
                Vector3 boostedVelocity = _rigidbody.velocity * (_bounceSpeedBoost * bounceIntensity);
                
                // 限制最大速度
                if (boostedVelocity.magnitude > _maxBounceSpeed)
                {
                    boostedVelocity = boostedVelocity.normalized * _maxBounceSpeed;
                }
                
                _rigidbody.velocity = boostedVelocity;
            }
            
            // 播放弹跳效果
            PlayBounceEffect(collision.contacts[0].point, collision.contacts[0].normal);
            PlayRandomBounceSound();
            
            // 更新轨迹颜色（随弹跳次数变化）
            UpdateTrailColor();
            
            if (_showDebugInfo)
                Debug.Log($"[弹跳投射物] 第 {_totalBounces} 次弹跳，强度: {bounceIntensity:F2}，速度: {_rigidbody.velocity.magnitude:F2}");
        }
        
        /// <summary>
        /// 播放弹跳效果
        /// </summary>
        private void PlayBounceEffect(Vector3 position, Vector3 normal)
        {
            // 创建弹跳视觉效果
            if (_bounceEffectPrefab != null)
            {
                GameObject effect = Instantiate(_bounceEffectPrefab, position, Quaternion.LookRotation(normal));
                Destroy(effect, 2f);
            }
            
            // 创建冲击波效果
            CreateShockwave(position, normal);
        }
        
        /// <summary>
        /// 创建冲击波效果
        /// </summary>
        private void CreateShockwave(Vector3 position, Vector3 normal)
        {
            // 这里可以创建圆形扩散的冲击波效果
            // 例如：使用粒子系统或shader效果
        }
        
        /// <summary>
        /// 播放随机弹跳音效
        /// </summary>
        private void PlayRandomBounceSound()
        {
            if (_bounceVariations != null && _bounceVariations.Length > 0)
            {
                AudioClip randomClip = _bounceVariations[Random.Range(0, _bounceVariations.Length)];
                
                // 创建临时音源以控制音调
                GameObject tempAudioObj = new GameObject("TempBounceAudio");
                tempAudioObj.transform.position = transform.position;
                
                AudioSource tempSource = tempAudioObj.AddComponent<AudioSource>();
                tempSource.clip = randomClip;
                tempSource.pitch = 1f + Random.Range(-_pitchVariation, _pitchVariation);
                tempSource.volume = 0.8f;
                tempSource.spatialBlend = 1f; // 3D sound
                tempSource.Play();
                
                // 音频播放完毕后销毁
                Destroy(tempAudioObj, randomClip.length + 0.1f);
            }
        }
        
        /// <summary>
        /// 更新轨迹颜色
        /// </summary>
        private void UpdateTrailColor()
        {
            if (_bouncyTrail == null) return;
            
            // 根据弹跳次数改变颜色
            float colorShift = (float)_totalBounces / _maxBounces;
            Color currentColor = Color.Lerp(_trailColor, Color.red, colorShift);
            
            _bouncyTrail.startColor = currentColor;
            _bouncyTrail.endColor = new Color(currentColor.r, currentColor.g, currentColor.b, 0f);
        }
        
        /// <summary>
        /// 设置轨迹颜色
        /// </summary>
        public void SetTrailColor(Color color)
        {
            _trailColor = color;
            SetupTrailEffect();
        }
        
        /// <summary>
        /// 设置弹跳参数
        /// </summary>
        public void SetBounceParameters(int maxBounces, float energyLoss)
        {
            _maxBounces = maxBounces;
            _bounceEnergyLoss = energyLoss;
        }
        
        protected override bool ProcessHit(RaycastHit hit)
        {
            // 弹跳投射物有更强的穿透倾向
            bool shouldDestroy = base.ProcessHit(hit);
            
            // 如果还有弹跳次数，优先尝试弹跳而不是造成伤害
            if (CanBounce && !shouldDestroy)
            {
                if (_showDebugInfo)
                    Debug.Log("[弹跳投射物] 优先弹跳而非销毁");
                return false;
            }
            
            return shouldDestroy;
        }
        
        protected override void OnDestroy()
        {
            // 弹跳投射物销毁时的特殊效果
            if (!_isDestroyed)
            {
                CreateFinalExplosion();
            }
            
            base.OnDestroy();
            
            if (_showDebugInfo)
                Debug.Log($"[弹跳投射物] 弹跳子弹销毁，总共弹跳了 {_totalBounces} 次");
        }
        
        /// <summary>
        /// 创建最终爆炸效果
        /// </summary>
        private void CreateFinalExplosion()
        {
            // 根据弹跳次数创建不同强度的爆炸效果
            float explosionIntensity = Mathf.Clamp01((float)_totalBounces / _maxBounces);
            
            // 这里可以根据弹跳次数创建更炫酷的爆炸效果
            if (_bounceEffectPrefab != null)
            {
                GameObject finalEffect = Instantiate(_bounceEffectPrefab, transform.position, Quaternion.identity);
                
                // 根据弹跳次数调整效果大小
                finalEffect.transform.localScale *= (1f + explosionIntensity);
                
                Destroy(finalEffect, 3f);
            }
        }
        
        #region 调试
        
#if UNITY_EDITOR
        protected override void OnDrawGizmos()
        {
            base.OnDrawGizmos();
            
            // 绘制弹跳计数信息
            if (_showDebugInfo && _totalBounces > 0)
            {
                Vector3 labelPos = transform.position + Vector3.up * 0.5f;
                UnityEditor.Handles.Label(labelPos, $"弹跳: {_totalBounces}/{_maxBounces}");
            }
        }
#endif
        
        #endregion
    }
}
