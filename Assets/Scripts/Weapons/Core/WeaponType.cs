namespace DWHITE.Weapons
{
    /// <summary>
    /// 武器类型枚举
    /// 用于区分不同类型的武器实现
    /// </summary>
    public enum WeaponType
    {
        /// <summary>
        /// 投射物武器 - 发射物理投射物
        /// </summary>
        Projectile,
        
        /// <summary>
        /// 射线武器 - 即时命中的射线检测
        /// </summary>
        Hitscan,
        
        /// <summary>
        /// 近战武器 - 近距离碰撞检测
        /// </summary>
        Melee,
        
        /// <summary>
        /// 激光武器 - 持续性射线伤害
        /// </summary>
        Laser,
        
        /// <summary>
        /// 霰弹武器 - 发射多个投射物或射线
        /// </summary>
        Shotgun,
        
        /// <summary>
        /// 爆炸武器 - 范围伤害武器
        /// </summary>
        Explosive,
        
        /// <summary>
        /// 特殊武器 - 具有独特机制的武器
        /// </summary>
        Special
    }
}
