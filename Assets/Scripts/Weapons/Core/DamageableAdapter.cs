using UnityEngine;

namespace DWHITE.Weapons
{
    /// <summary>
    /// IDamageable接口适配器
    /// 统一处理Core命名空间中的IDamageable接口
    /// </summary>
    public static class DamageableAdapter
    {
        /// <summary>
        /// 对目标应用伤害，使用Core命名空间的IDamageable接口
        /// </summary>
        /// <param name="target">目标碰撞体</param>
        /// <param name="damage">伤害值</param>
        /// <param name="hitPoint">命中点</param>
        /// <param name="hitDirection">命中方向</param>
        /// <param name="source">伤害来源</param>
        /// <param name="weapon">武器引用</param>
        /// <param name="projectile">投射物引用</param>
        /// <returns>是否成功应用伤害</returns>
        public static bool ApplyDamage(Collider target, float damage, Vector3 hitPoint, Vector3 hitDirection,
            GameObject source = null, WeaponBase weapon = null, ProjectileBase projectile = null)
        {
            if (target == null) return false;
            
            // 使用Core命名空间的IDamageable接口
            DWHITE.IDamageable damageable = target.GetComponent<DWHITE.IDamageable>();
            if (damageable == null)
            {
                damageable = target.GetComponentInParent<DWHITE.IDamageable>();
            }
            
            if (damageable != null && damageable.IsAlive)
            {
                damageable.TakeDamage(damage, hitPoint, hitDirection);
                return true;
            }
            
            return false;
        }
        
        /// <summary>
        /// 检查目标是否存活
        /// </summary>
        /// <param name="target">目标碰撞体</param>
        /// <returns>是否存活</returns>
        public static bool IsTargetAlive(Collider target)
        {
            if (target == null) return false;
            
            DWHITE.IDamageable damageable = target.GetComponent<DWHITE.IDamageable>();
            if (damageable == null)
            {
                damageable = target.GetComponentInParent<DWHITE.IDamageable>();
            }
            
            return damageable != null && damageable.IsAlive;
        }
        
        /// <summary>
        /// 获取目标的当前生命值
        /// </summary>
        /// <param name="target">目标碰撞体</param>
        /// <returns>当前生命值，如果目标不可伤害则返回-1</returns>
        public static float GetTargetHealth(Collider target)
        {
            if (target == null) return -1f;
            
            DWHITE.IDamageable damageable = target.GetComponent<DWHITE.IDamageable>();
            if (damageable == null)
            {
                damageable = target.GetComponentInParent<DWHITE.IDamageable>();
            }
            
            return damageable?.GetCurrentHealth() ?? -1f;
        }
    }
}
