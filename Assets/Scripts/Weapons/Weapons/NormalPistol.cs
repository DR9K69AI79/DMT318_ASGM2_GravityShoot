using UnityEngine;

namespace DWHITE.Weapons
{
    /// <summary>
    /// 普通手枪实现
    /// 发射普通子弹的武器
    /// </summary>
    public class NormalPistol : ProjectileWeapon
    {
        protected override void UpdateWeapon()
        {
            base.UpdateWeapon();
            
        }
        
        protected override void OnEquip()
        {
            base.OnEquip();
            
            if (_showDebugInfo)
                Debug.Log("[普通手枪] 普通手枪已装备！准备发射子弹！");
        }
        
        protected override void OnUnequip()
        {
            base.OnUnequip();
            
            if (_showDebugInfo)
                Debug.Log("[普通手枪] 普通手枪已收起");
        }
    }
}
