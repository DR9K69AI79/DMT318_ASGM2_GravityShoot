using UnityEngine;
using System.Collections.Generic;

namespace DWHITE.Weapons
{
    /// <summary>
    /// 近战武器基类
    /// 处理近距离攻击，包括范围检测和连击
    /// </summary>
    public class MeleeWeapon : WeaponBase
    {
        [Header("近战设置")]
        [SerializeField] protected float _attackRange = 2f;
        [SerializeField] protected float _attackAngle = 90f; // 攻击扇形角度
        [SerializeField] protected LayerMask _hitLayers = -1;
        [SerializeField] protected int _maxTargetsPerSwing = 5; // 每次挥击最大目标数
        
        [Header("攻击效果")]
        [SerializeField] protected GameObject _slashEffect;
        [SerializeField] protected Transform _effectSpawnPoint;        [SerializeField] protected AudioClip _swingSound;
        [SerializeField] protected AudioClip _hitSound;
        
        private AudioSource _audioSource; // 音效播放组件
        
        [Header("连击系统")]
        [SerializeField] protected bool _enableCombo = false;
        [SerializeField] protected int _maxComboCount = 3;
        [SerializeField] protected float _comboTimeWindow = 1f; // 连击时间窗口
        [SerializeField] protected float[] _comboDamageMultipliers = { 1f, 1.2f, 1.5f };
        
        protected int _currentComboCount = 0;
        protected float _lastComboTime = 0f;
          protected override void Awake()
        {
            base.Awake();
            
            // 获取或添加AudioSource组件
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
            }
            
            // 近战武器通常不需要弹药系统
            if (_weaponData != null && _weaponData.HasInfiniteAmmo)
            {
                _currentAmmo = 1; // 设置为1以满足HasAmmo检查
            }
        }
        
        protected override void Update()
        {
            base.Update();
            UpdateComboSystem();
        }
        
        /// <summary>
        /// 更新连击系统
        /// </summary>
        protected virtual void UpdateComboSystem()
        {
            if (!_enableCombo) return;
            
            // 检查连击是否超时
            if (Time.time - _lastComboTime > _comboTimeWindow)
            {
                if (_currentComboCount > 0)
                {
                    _currentComboCount = 0;
                    if (_showDebugInfo)
                        Debug.Log("[近战武器] 连击重置");
                }
            }
        }
        
        /// <summary>
        /// 具体的攻击实现
        /// </summary>
        /// <param name="direction">攻击方向</param>
        /// <returns>是否成功攻击</returns>
        protected override bool FireImplementation(Vector3 direction)
        {
            if (_weaponData == null || _muzzlePoint == null)
            {
                if (_showDebugInfo)
                    Debug.LogError($"[近战武器] {gameObject.name} 缺少必要组件");
                return false;
            }
            
            Vector3 attackOrigin = _muzzlePoint.position;
            Vector3 attackDirection = direction.normalized;
            
            if (_showDebugInfo)
            {
                Debug.Log($"[近战武器] 攻击: 起点={attackOrigin}, 方向={attackDirection}, 范围={_attackRange}");
            }
            
            // 执行范围攻击
            List<Collider> hitTargets = PerformMeleeAttack(attackOrigin, attackDirection);
            
            // 播放攻击音效
            PlaySwingSound();
            
            // 生成攻击特效
            CreateSlashEffect(attackOrigin, attackDirection);
            
            // 更新连击
            if (_enableCombo)
            {
                UpdateCombo();
            }
            
            return hitTargets.Count > 0;
        }
        
        /// <summary>
        /// 执行近战攻击检测
        /// </summary>
        protected virtual List<Collider> PerformMeleeAttack(Vector3 origin, Vector3 direction)
        {
            List<Collider> hitTargets = new List<Collider>();
            
            // 获取攻击范围内的所有碰撞体
            Collider[] potentialTargets = Physics.OverlapSphere(origin, _attackRange, _hitLayers);
            
            foreach (Collider target in potentialTargets)
            {
                // 跳过自己
                if (target.transform.root == transform.root)
                    continue;
                
                // 检查是否在攻击角度内
                Vector3 targetDirection = (target.transform.position - origin).normalized;
                float angle = Vector3.Angle(direction, targetDirection);
                
                if (angle <= _attackAngle * 0.5f)
                {
                    hitTargets.Add(target);
                    
                    // 处理伤害
                    ProcessMeleeHit(target, origin, targetDirection);
                    
                    // 限制同时攻击的目标数量
                    if (hitTargets.Count >= _maxTargetsPerSwing)
                        break;
                }
            }
            
            if (_showDebugInfo)
            {
                Debug.Log($"[近战武器] 命中 {hitTargets.Count} 个目标");
            }
            
            return hitTargets;
        }
          /// <summary>
        /// 处理近战命中
        /// </summary>
        protected virtual void ProcessMeleeHit(Collider target, Vector3 attackOrigin, Vector3 targetDirection)
        {
            // 计算伤害（包括连击加成）
            float damage = CalculateMeleeDamage();
            
            // 使用DamageableAdapter统一处理伤害
            Vector3 hitPoint = target.ClosestPoint(attackOrigin);
            bool damageApplied = DamageableAdapter.ApplyDamage(
                target,
                damage,
                hitPoint,
                targetDirection,
                gameObject, // 使用武器GameObject作为来源
                this,
                null // 近战武器没有投射物
            );
            
            if (damageApplied)
            {
                // 播放命中音效
                PlayHitSound();
                
                if (_showDebugInfo)
                {
                    Debug.Log($"[近战武器] 对 {target.name} 造成 {damage} 伤害 (连击: {_currentComboCount})");
                }
            }            else if (_showDebugInfo)
            {
                Debug.Log($"[近战武器] {target.name} 不是可伤害目标");
            }
        }
        
        /// <summary>
        /// 计算近战伤害（包括连击加成）
        /// </summary>
        protected virtual float CalculateMeleeDamage()
        {
            float baseDamage = _weaponData.Damage;
            
            if (!_enableCombo || _currentComboCount == 0)
                return baseDamage;
            
            // 应用连击伤害倍数
            int comboIndex = Mathf.Min(_currentComboCount - 1, _comboDamageMultipliers.Length - 1);
            float multiplier = _comboDamageMultipliers[comboIndex];
            
            return baseDamage * multiplier;
        }
        
        /// <summary>
        /// 更新连击计数
        /// </summary>
        protected virtual void UpdateCombo()
        {
            _currentComboCount = Mathf.Min(_currentComboCount + 1, _maxComboCount);
            _lastComboTime = Time.time;
            
            if (_showDebugInfo)
            {
                Debug.Log($"[近战武器] 连击 {_currentComboCount}!");
            }
        }
        
        /// <summary>
        /// 创建斩击特效
        /// </summary>
        protected virtual void CreateSlashEffect(Vector3 origin, Vector3 direction)
        {
            if (_slashEffect == null) return;
            
            Vector3 effectPosition = _effectSpawnPoint != null ? _effectSpawnPoint.position : origin;
            Quaternion effectRotation = Quaternion.LookRotation(direction);
            
            GameObject effect = Instantiate(_slashEffect, effectPosition, effectRotation);
            
            // 自动销毁特效
            Destroy(effect, 2f);
        }
        
        /// <summary>
        /// 播放挥击音效
        /// </summary>
        protected virtual void PlaySwingSound()
        {
            if (_swingSound != null && _audioSource != null)
            {
                _audioSource.PlayOneShot(_swingSound);
            }
        }
        
        /// <summary>
        /// 播放命中音效
        /// </summary>
        protected virtual void PlayHitSound()
        {
            if (_hitSound != null && _audioSource != null)
            {
                _audioSource.PlayOneShot(_hitSound);
            }
        }
        
        /// <summary>
        /// 近战武器通常不需要装弹
        /// </summary>
        public override bool TryReload()
        {
            // 近战武器不需要装弹
            return false;
        }
        
        /// <summary>
        /// 重写HasAmmo检查，近战武器总是有"弹药"
        /// </summary>
        public new bool HasAmmo
        {
            get
            {
                // 近战武器总是可以使用，除非武器数据明确限制
                return _weaponData == null || _weaponData.HasInfiniteAmmo || _currentAmmo > 0;
            }
        }        
        /// <summary>
        /// 网络同步的攻击效果
        /// </summary>
        public override void NetworkFire(Vector3 direction, float timestamp)
        {
            // 显示攻击效果但不造成伤害
            Vector3 attackOrigin = _muzzlePoint.position;
            CreateSlashEffect(attackOrigin, direction.normalized);
            PlaySwingSound();
            
            if (_showDebugInfo)
            {
                Debug.Log("[近战武器] 网络同步攻击效果");
            }        }

#if UNITY_EDITOR
        /// <summary>
        /// 绘制攻击范围用于调试
        /// </summary>
        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();

            if (_muzzlePoint == null) return;

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(_muzzlePoint.position, _attackRange);

            // 绘制攻击角度
            Vector3 forward = _muzzlePoint.forward;
            Vector3 right = Quaternion.AngleAxis(_attackAngle * 0.5f, _muzzlePoint.up) * forward;
            Vector3 left = Quaternion.AngleAxis(-_attackAngle * 0.5f, _muzzlePoint.up) * forward;

            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(_muzzlePoint.position, right * _attackRange);
            Gizmos.DrawRay(_muzzlePoint.position, left * _attackRange);
        }
#endif
    }
}
