using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DWHITE.Weapons
{
    /// <summary>
    /// 双管霰弹枪实现
    /// 发射霰弹的武器
    /// </summary>
    public class ShotGun : ProjectileWeapon
    {
        protected override void UpdateWeapon()
        {
            base.UpdateWeapon();
        }

        protected override void OnEquip()
        {
            base.OnEquip();

            if (_showDebugInfo)
                Debug.Log("[双管霰弹枪] 双管霰弹枪已装备！准备发射霰弹！");
        }

        protected override void OnUnequip()
        {
            base.OnUnequip();

            if (_showDebugInfo)
                Debug.Log("[双管霰弹枪] 双管霰弹枪已收起");
        }
    }
}
