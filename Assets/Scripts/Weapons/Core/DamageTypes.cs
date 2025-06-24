using UnityEngine;

namespace DWHITE.Weapons
{
    /// <summary>
    /// 伤害类型枚举
    /// </summary>
    public enum DamageType
    {
        Projectile,    // 投射物伤害
        Explosion,     // 爆炸伤害
        Hitscan,       // 即时命中伤害
        Environmental, // 环境伤害
        Melee         // 近战伤害
    }
    
    /// <summary>
    /// 伤害信息结构
    /// </summary>
    [System.Serializable]
    public struct DamageInfo
    {
        public float damage;
        public DamageType damageType;
        public GameObject source;
        public WeaponBase weapon;
        public Vector3 hitPoint;
        public Vector3 hitNormal;
        public Vector3 hitDirection;
        public bool isHeadshot;
        public ProjectileBase projectile;
        public float distance;
    }
}
