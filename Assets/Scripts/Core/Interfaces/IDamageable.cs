using UnityEngine;

namespace DWHITE
{
    /// <summary>
    /// 可受伤害接口
    /// 定义所有可以受到伤害的对象必须实现的方法
    /// </summary>
    public interface IDamageable
    {
        /// <summary>
        /// 受到伤害
        /// </summary>
        /// <param name="damage">伤害值</param>
        /// <param name="hitPoint">命中点</param>
        /// <param name="hitDirection">伤害方向</param>
        void TakeDamage(float damage, Vector3 hitPoint, Vector3 hitDirection);
        
        /// <summary>
        /// 获取当前生命值
        /// </summary>
        /// <returns>当前生命值</returns>
        float GetCurrentHealth();
        
        /// <summary>
        /// 获取最大生命值
        /// </summary>
        /// <returns>最大生命值</returns>
        float GetMaxHealth();
          /// <summary>
        /// 检查是否存活
        /// </summary>
        /// <returns>是否存活</returns>
        bool IsAlive { get; }
    }
}
