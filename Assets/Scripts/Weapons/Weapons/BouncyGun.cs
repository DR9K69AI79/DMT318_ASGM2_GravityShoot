using UnityEngine;

namespace DWHITE.Weapons
{
    /// <summary>
    /// 弹力枪实现
    /// 发射高弹跳投射物的武器
    /// </summary>
    public class BouncyGun : ProjectileWeapon
    {
        [Header("弹力枪设置")]
        [SerializeField] private bool _chargeable = false;
        [SerializeField] private float _maxChargeTime = 2f;
        [SerializeField] private float _chargeSpeedMultiplier = 2f;
        [SerializeField] private AnimationCurve _chargeCurve = AnimationCurve.Linear(0, 1, 1, 2);
        
        [Header("弹力枪效果")]
        [SerializeField] private Color _bounceTrailColor = Color.cyan;
        [SerializeField] private ParticleSystem _chargeEffect;
        
        private float _chargeStartTime;
        private bool _isCharging;
        
        protected override void UpdateWeapon()
        {
            base.UpdateWeapon();
            
            // 处理蓄力逻辑
            if (_chargeable && _isEquipped)
            {
                UpdateCharging();
            }
        }
        
        /// <summary>
        /// 更新蓄力状态
        /// </summary>
        private void UpdateCharging()
        {
            // 这里需要检查玩家输入来启动蓄力
            // 简化实现，实际应该通过 PlayerWeaponController 的输入系统
        }
        
        /// <summary>
        /// 开始蓄力
        /// </summary>
        public void StartCharging()
        {
            if (!_chargeable || _isCharging) return;
            
            _isCharging = true;
            _chargeStartTime = Time.time;
            
            if (_chargeEffect != null)
            {
                _chargeEffect.Play();
            }
            
            if (_showDebugInfo)
                Debug.Log("[弹力枪] 开始蓄力");
        }
        
        /// <summary>
        /// 停止蓄力并射击
        /// </summary>
        public void ReleaseCharge(Vector3 direction)
        {
            if (!_isCharging) return;
            
            float chargeTime = Time.time - _chargeStartTime;
            float chargeRatio = Mathf.Clamp01(chargeTime / _maxChargeTime);
            
            _isCharging = false;
            
            if (_chargeEffect != null)
            {
                _chargeEffect.Stop();
            }
            
            // 执行蓄力射击
            FireCharged(direction, chargeRatio);
            
            if (_showDebugInfo)
                Debug.Log($"[弹力枪] 蓄力射击，蓄力比例: {chargeRatio:F2}");
        }
        
        /// <summary>
        /// 执行蓄力射击
        /// </summary>
        private void FireCharged(Vector3 direction, float chargeRatio)
        {
            // 计算蓄力后的速度倍数
            float speedMultiplier = _chargeCurve.Evaluate(chargeRatio) * _chargeSpeedMultiplier;
            
            // 临时修改武器数据
            float originalSpeed = _weaponData.ProjectileSpeed;
            // 注意：这里直接修改 ScriptableObject 是不好的做法
            // 在实际项目中应该通过其他方式传递蓄力参数
            
            // 执行射击
            Fire(direction);
            
            // 恢复原始速度
            // _weaponData.ProjectileSpeed = originalSpeed; // 不应该直接修改SO
        }
        
        protected override void ConfigureProjectile(ProjectileBase projectile, Vector3 velocity, Vector3 direction)
        {
            base.ConfigureProjectile(projectile, velocity, direction);
            
            // 为弹力投射物添加特殊配置
            BouncyProjectile bouncyProjectile = projectile as BouncyProjectile;
            if (bouncyProjectile != null)
            {
                bouncyProjectile.SetTrailColor(_bounceTrailColor);
                
                // 根据武器数据设置弹跳参数
                if (_weaponData != null)
                {
                    bouncyProjectile.SetBounceParameters(
                        _weaponData.MaxBounceCount,
                        _weaponData.BounceEnergyLoss
                    );
                }
            }
        }
        
        protected override void OnEquip()
        {
            base.OnEquip();
            
            if (_showDebugInfo)
                Debug.Log("[弹力枪] 弹力枪已装备！准备发射弹跳子弹！");
        }
        
        protected override void OnUnequip()
        {
            // 取消蓄力
            if (_isCharging)
            {
                _isCharging = false;
                if (_chargeEffect != null)
                {
                    _chargeEffect.Stop();
                }
            }
            
            base.OnUnequip();
            
            if (_showDebugInfo)
                Debug.Log("[弹力枪] 弹力枪已收起");
        }
    }
}
