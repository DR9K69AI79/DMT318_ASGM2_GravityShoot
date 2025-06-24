using UnityEngine;

namespace DWHITE.Weapons
{
    /// <summary>
    /// 武器接口，定义所有武器必须实现的基本功能
    /// </summary>
    public interface IWeapon
    {
        #region 基本属性
        
        /// <summary>
        /// 武器配置数据
        /// </summary>
        WeaponData WeaponData { get; }
        
        /// <summary>
        /// 是否已装备
        /// </summary>
        bool IsEquipped { get; }
        
        /// <summary>
        /// 是否正在装弹
        /// </summary>
        bool IsReloading { get; }
        
        /// <summary>
        /// 当前弹药数
        /// </summary>
        int CurrentAmmo { get; }
        
        /// <summary>
        /// 最大弹药数
        /// </summary>
        int MaxAmmo { get; }
        
        /// <summary>
        /// 是否有弹药
        /// </summary>
        bool HasAmmo { get; }
        
        /// <summary>
        /// 是否可以开火
        /// </summary>
        bool CanFire { get; }
        
        /// <summary>
        /// 枪口位置
        /// </summary>
        Transform MuzzlePoint { get; }
        
        #endregion
        
        #region 核心功能
        
        /// <summary>
        /// 装备武器
        /// </summary>
        void Equip();
        
        /// <summary>
        /// 卸载武器
        /// </summary>
        void Unequip();
        
        /// <summary>
        /// 尝试开火
        /// </summary>
        /// <param name="targetDirection">射击方向</param>
        /// <returns>是否成功开火</returns>
        bool TryFire(Vector3 targetDirection);
        
        /// <summary>
        /// 尝试装弹
        /// </summary>
        /// <returns>是否成功开始装弹</returns>
        bool TryReload();
        
        #endregion
    }
}
