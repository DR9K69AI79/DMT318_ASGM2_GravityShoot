using UnityEngine;

namespace DWHITE.Weapons
{
    /// <summary>
    /// 遗留的IDamageable接口 - 用于向后兼容
    /// 将逐步迁移到Core命名空间的接口
    /// </summary>
    public interface ILegacyDamageable
    {
        void TakeDamage(DamageInfo damageInfo);
        float GetHealth();
        bool IsAlive();
    }
    
    /// <summary>
    /// 用于遗留接口的简化DamageInfo
    /// </summary>
    [System.Serializable]
    public struct LegacyDamageInfo
    {
        public float damage;
        public GameObject source;
        public WeaponBase weapon;
        public Vector3 hitPoint;
        public Vector3 hitDirection;
        public ProjectileBase projectile;
    }
}
