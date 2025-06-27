using UnityEngine;

namespace DWHITE.Weapons
{
    /// <summary>
    /// 射线检测武器（即时命中武器，如激光枪）
    /// 不生成投射物，使用射线检测即时命中目标
    /// </summary>
    public class HitscanWeapon : WeaponBase
    {
        [Header("射线设置")]
        [SerializeField] protected LayerMask _hitLayers = -1;
        [SerializeField] protected float _maxRange = 100f;
        [SerializeField] protected LineRenderer _laserLine;
        [SerializeField] protected GameObject _impactEffect;
        [SerializeField] protected float _laserVisibleTime = 0.1f;
        
        [Header("伤害衰减")]
        [SerializeField] protected bool _useDamageDropoff = false;
        [SerializeField] protected AnimationCurve _damageDropoffCurve = AnimationCurve.Linear(0, 1, 1, 0.5f);
        
        protected override void Awake()
        {
            base.Awake();
            
            // 确保激光线组件存在
            if (_laserLine == null)
            {
                _laserLine = GetComponentInChildren<LineRenderer>();
            }
            
            // 初始时隐藏激光线
            if (_laserLine != null)
            {
                _laserLine.gameObject.SetActive(false);
            }
        }
        
        /// <summary>
        /// 具体的开火实现 - 射线检测
        /// </summary>
        /// <param name="direction">射击方向</param>
        /// <returns>是否成功开火</returns>
        protected override bool FireImplementation(Vector3 direction)
        {
            if (_weaponData == null || _muzzlePoint == null)
            {
                if (_showDebugInfo)
                    Debug.LogError($"[射线武器] {gameObject.name} 缺少必要组件");
                return false;
            }
            
            Vector3 origin = _muzzlePoint.position;
            Vector3 fireDirection = direction.normalized;
            float range = _maxRange > 0 ? _maxRange : _weaponData.MaxRange;
            
            if (_showDebugInfo)
            {
                Debug.Log($"[射线武器] 射线检测: 起点={origin}, 方向={fireDirection}, 距离={range}");
            }
            
            // 执行射线检测
            bool hitSomething = PerformRaycast(origin, fireDirection, range);
              // 显示激光效果
            ShowLaserEffect(origin, fireDirection, range);
            
            // TODO: 实现枪口闪光等效果
            // PlayMuzzleFlash();
            
            return true;
        }
        
        /// <summary>
        /// 执行射线检测和伤害处理
        /// </summary>
        protected virtual bool PerformRaycast(Vector3 origin, Vector3 direction, float maxDistance)
        {
            RaycastHit hit;
            Vector3 endPoint = origin + direction * maxDistance;
            
            if (Physics.Raycast(origin, direction, out hit, maxDistance, _hitLayers))
            {
                if (_showDebugInfo)
                {
                    Debug.Log($"[射线武器] 命中目标: {hit.collider.name} 距离: {hit.distance:F2}");
                }
                
                // 计算伤害（考虑距离衰减）
                float damage = CalculateDamage(hit.distance);
                  // TODO: 集成DamageSystem后，这里应该调用DamageSystem.ApplyDamage
                // 使用DamageableAdapter统一处理伤害
                ProcessHit(hit, damage);
                
                // 在命中点生成效果
                CreateImpactEffect(hit.point, hit.normal);
                
                return true;
            }
            else
            {
                if (_showDebugInfo)
                {
                    Debug.Log("[射线武器] 未命中任何目标");
                }
                return false;
            }
        }
        
        /// <summary>
        /// 计算考虑距离衰减的伤害
        /// </summary>
        protected virtual float CalculateDamage(float distance)
        {
            float baseDamage = _weaponData.Damage;
            
            if (!_useDamageDropoff)
                return baseDamage;
            
            float normalizedDistance = distance / _maxRange;
            float damageMultiplier = _damageDropoffCurve.Evaluate(normalizedDistance);
            
            return baseDamage * damageMultiplier;
        }        /// <summary>
        /// 处理命中逻辑
        /// </summary>
        protected virtual void ProcessHit(RaycastHit hit, float damage)
        {
            // 计算爆头伤害
            bool isHeadshot = IsHeadshot(hit);
            float finalDamage = damage;
            
            if (isHeadshot && _weaponData.CanHeadshot)
            {
                finalDamage *= _weaponData.HeadshotMultiplier;
            }
            // 直接调用IDamageable接口处理伤害
            DWHITE.IDamageable damageable = hit.collider.GetComponent<DWHITE.IDamageable>();
            if (damageable == null)
            {
                damageable = hit.collider.GetComponentInParent<DWHITE.IDamageable>();
            }
            
            if (damageable != null && damageable.IsAlive)
            {
                damageable.TakeDamage(finalDamage, hit.point, -hit.normal);
                
                if (_showDebugInfo)
                {
                    Debug.Log($"[射线武器] 造成伤害: {finalDamage} (爆头: {isHeadshot})");
                }
            }
            else if (_showDebugInfo)
            {
                Debug.Log($"[射线武器] {hit.collider.name} 不是可伤害目标");
            }
        }
        
        /// <summary>
        /// 检查是否为爆头
        /// </summary>
        protected virtual bool IsHeadshot(RaycastHit hit)
        {
            // 简单的爆头检测：检查碰撞体名称或标签
            return hit.collider.name.ToLower().Contains("head") || 
                   hit.collider.CompareTag("Head");
        }
        
        /// <summary>
        /// 显示激光效果
        /// </summary>
        protected virtual void ShowLaserEffect(Vector3 origin, Vector3 direction, float maxDistance)
        {
            if (_laserLine == null) return;
            
            Vector3 endPoint = origin + direction * maxDistance;
            
            // 检查是否有遮挡
            RaycastHit hit;
            if (Physics.Raycast(origin, direction, out hit, maxDistance, _hitLayers))
            {
                endPoint = hit.point;
            }
            
            // 设置激光线
            _laserLine.gameObject.SetActive(true);
            _laserLine.positionCount = 2;
            _laserLine.SetPosition(0, origin);
            _laserLine.SetPosition(1, endPoint);
            
            // 延迟隐藏激光线
            if (_laserVisibleTime > 0)
            {
                Invoke(nameof(HideLaserEffect), _laserVisibleTime);
            }
        }
        
        /// <summary>
        /// 隐藏激光效果
        /// </summary>
        protected virtual void HideLaserEffect()
        {
            if (_laserLine != null)
            {
                _laserLine.gameObject.SetActive(false);
            }
        }
        
        /// <summary>
        /// 创建命中特效
        /// </summary>
        protected virtual void CreateImpactEffect(Vector3 position, Vector3 normal)
        {
            if (_impactEffect != null)
            {
                GameObject effect = Instantiate(_impactEffect, position, Quaternion.LookRotation(normal));
                
                // 自动销毁特效
                Destroy(effect, 3f);
            }
        }
          /// <summary>
        /// 网络同步的开火效果
        /// </summary>
        public override void NetworkFire(Vector3 direction, float timestamp)
        {
            // 显示射击效果但不造成伤害
            Vector3 origin = _muzzlePoint.position;
            ShowLaserEffect(origin, direction.normalized, _maxRange);
            // TODO: 实现枪口闪光效果
            
            if (_showDebugInfo)
            {
                Debug.Log("[射线武器] 网络同步射击效果");
            }
        }
    }
}
