using UnityEngine;

namespace DWHITE.Weapons
{
    /// <summary>
    /// 武器预设配置工具
    /// 提供常用武器类型的快速配置模板
    /// </summary>
    public static class WeaponPresets
    {
        #region 基础武器预设

        /// <summary>
        /// 标准突击步枪预设
        /// </summary>
        public static void ApplyAssaultRiflePreset(WeaponData weaponData)
        {
            if (weaponData == null) return;

            // 通过反射设置私有字段，仅在编辑器模式下使用
            #if UNITY_EDITOR
            SetPrivateField(weaponData, "_fireRate", 6f);
            SetPrivateField(weaponData, "_automatic", true);
            SetPrivateField(weaponData, "_projectilesPerShot", 1);
            SetPrivateField(weaponData, "_spreadAngle", 2f);
            SetPrivateField(weaponData, "_accuracy", 0.85f);
            SetPrivateField(weaponData, "_magazineSize", 30);
            SetPrivateField(weaponData, "_reloadTime", 2.5f);
            SetPrivateField(weaponData, "_recoilPattern", new Vector2(1.5f, 3f));
            SetPrivateField(weaponData, "_recoilRecoveryTime", 0.8f);
            
            Debug.Log("已应用突击步枪预设配置");
            #endif
        }

        /// <summary>
        /// 狙击步枪预设
        /// </summary>
        public static void ApplySniperRiflePreset(WeaponData weaponData)
        {
            if (weaponData == null) return;

            #if UNITY_EDITOR
            SetPrivateField(weaponData, "_fireRate", 0.8f);
            SetPrivateField(weaponData, "_automatic", false);
            SetPrivateField(weaponData, "_projectilesPerShot", 1);
            SetPrivateField(weaponData, "_spreadAngle", 0f);
            SetPrivateField(weaponData, "_accuracy", 1f);
            SetPrivateField(weaponData, "_magazineSize", 5);
            SetPrivateField(weaponData, "_reloadTime", 3.5f);
            SetPrivateField(weaponData, "_recoilPattern", new Vector2(0.5f, 8f));
            SetPrivateField(weaponData, "_recoilRecoveryTime", 1.5f);
            
            Debug.Log("已应用狙击步枪预设配置");
            #endif
        }

        /// <summary>
        /// 霰弹枪预设
        /// </summary>
        public static void ApplyShotgunPreset(WeaponData weaponData)
        {
            if (weaponData == null) return;

            #if UNITY_EDITOR
            SetPrivateField(weaponData, "_fireRate", 1.2f);
            SetPrivateField(weaponData, "_automatic", false);
            SetPrivateField(weaponData, "_projectilesPerShot", 8);
            SetPrivateField(weaponData, "_spreadAngle", 15f);
            SetPrivateField(weaponData, "_accuracy", 0.6f);
            SetPrivateField(weaponData, "_magazineSize", 6);
            SetPrivateField(weaponData, "_reloadTime", 4f);
            SetPrivateField(weaponData, "_recoilPattern", new Vector2(2f, 6f));
            SetPrivateField(weaponData, "_recoilRecoveryTime", 1.2f);
            
            Debug.Log("已应用霰弹枪预设配置");
            #endif
        }

        /// <summary>
        /// 手枪预设
        /// </summary>
        public static void ApplyPistolPreset(WeaponData weaponData)
        {
            if (weaponData == null) return;

            #if UNITY_EDITOR
            SetPrivateField(weaponData, "_fireRate", 3f);
            SetPrivateField(weaponData, "_automatic", false);
            SetPrivateField(weaponData, "_projectilesPerShot", 1);
            SetPrivateField(weaponData, "_spreadAngle", 1f);
            SetPrivateField(weaponData, "_accuracy", 0.9f);
            SetPrivateField(weaponData, "_magazineSize", 12);
            SetPrivateField(weaponData, "_reloadTime", 1.8f);
            SetPrivateField(weaponData, "_recoilPattern", new Vector2(1f, 2.5f));
            SetPrivateField(weaponData, "_recoilRecoveryTime", 0.4f);
            
            Debug.Log("已应用手枪预设配置");
            #endif
        }

        #endregion

        #region 投射物预设

        /// <summary>
        /// 标准子弹预设
        /// </summary>
        public static ProjectileSettings CreateStandardBulletPreset()
        {
            var settings = new ProjectileSettings();
            
            #if UNITY_EDITOR
            SetPrivateField(settings, "_speed", 100f);
            SetPrivateField(settings, "_damage", 25f);
            SetPrivateField(settings, "_maxRange", 200f);
            SetPrivateField(settings, "_lifetime", 5f);
            SetPrivateField(settings, "_mass", 0.5f);
            SetPrivateField(settings, "_drag", 0.1f);
            SetPrivateField(settings, "_useGravity", false);
            #endif
            
            return settings;
        }

        /// <summary>
        /// 爆炸弹预设
        /// </summary>
        public static ProjectileSettings CreateExplosiveBulletPreset()
        {
            var settings = CreateStandardBulletPreset();
            
            #if UNITY_EDITOR
            SetPrivateField(settings, "_speed", 80f);
            SetPrivateField(settings, "_damage", 20f);
            SetPrivateField(settings, "_explosionRadius", 5f);
            SetPrivateField(settings, "_explosionDamage", 60f);
            SetPrivateField(settings, "_friendlyFire", false);
            #endif
            
            return settings;
        }

        /// <summary>
        /// 弹跳弹预设
        /// </summary>
        public static ProjectileSettings CreateBouncingBulletPreset()
        {
            var settings = CreateStandardBulletPreset();
            
            #if UNITY_EDITOR
            SetPrivateField(settings, "_speed", 60f);
            SetPrivateField(settings, "_damage", 15f);
            SetPrivateField(settings, "_maxBounceCount", 3);
            SetPrivateField(settings, "_bounceEnergyLoss", 0.2f);
            SetPrivateField(settings, "_lifetime", 8f);
            #endif
            
            return settings;
        }

        /// <summary>
        /// 重力弹预设
        /// </summary>
        public static ProjectileSettings CreateGravityBulletPreset()
        {
            var settings = CreateStandardBulletPreset();
            
            #if UNITY_EDITOR
            SetPrivateField(settings, "_speed", 40f);
            SetPrivateField(settings, "_damage", 10f);
            SetPrivateField(settings, "_gravityForce", 50f);
            SetPrivateField(settings, "_gravityRadius", 8f);
            SetPrivateField(settings, "_affectOtherProjectiles", true);
            SetPrivateField(settings, "_lifetime", 12f);
            #endif
            
            return settings;
        }

        /// <summary>
        /// 穿透弹预设
        /// </summary>
        public static ProjectileSettings CreatePiercingBulletPreset()
        {
            var settings = CreateStandardBulletPreset();
            
            #if UNITY_EDITOR
            SetPrivateField(settings, "_speed", 120f);
            SetPrivateField(settings, "_damage", 18f);
            SetPrivateField(settings, "_penetrationCount", 5);
            SetPrivateField(settings, "_penetrationDamageReduction", 0.15f);
            #endif
            
            return settings;
        }

        #endregion

        #region 实用工具

        /// <summary>
        /// 配置验证 - 检查不合理的数值组合
        /// </summary>
        public static string ValidateWeaponConfiguration(WeaponData weaponData)
        {
            if (weaponData == null) return "武器配置为空";

            var warnings = new System.Text.StringBuilder();

            // 检查基础配置
            if (weaponData.FireRate > 15f)
                warnings.AppendLine("⚠️ 射速过高，可能影响性能");
            
            if (weaponData.MagazineSize > 100)
                warnings.AppendLine("⚠️ 弹夹容量过大，可能影响游戏平衡");

            if (weaponData.ReloadTime < 0.5f)
                warnings.AppendLine("⚠️ 装弹时间过短，玩家可能感觉不到装弹过程");

            // 检查投射物配置
            if (weaponData.UseProjectileSettings && weaponData.ProjectileSettings != null)
            {
                var projSettings = weaponData.ProjectileSettings;
                
                if (projSettings.Speed > 300f)
                    warnings.AppendLine("⚠️ 投射物速度过高，可能难以命中");
                
                if (projSettings.Damage > 200f)
                    warnings.AppendLine("⚠️ 伤害值过高，可能破坏游戏平衡");
                
                if (projSettings.ExplosionRadius > 20f)
                    warnings.AppendLine("⚠️ 爆炸半径过大，注意性能和平衡性");
                
                if (projSettings.MaxBounceCount > 10)
                    warnings.AppendLine("⚠️ 弹跳次数过多，可能造成无限弹跳");
            }

            return warnings.Length > 0 ? warnings.ToString() : "✓ 配置看起来合理";
        }

        /// <summary>
        /// 使用反射设置私有字段（仅编辑器模式）
        /// </summary>
        private static void SetPrivateField(object target, string fieldName, object value)
        {
            #if UNITY_EDITOR
            var field = target.GetType().GetField(fieldName, 
                System.Reflection.BindingFlags.NonPublic | 
                System.Reflection.BindingFlags.Instance);
            
            if (field != null)
            {
                field.SetValue(target, value);
            }
            else
            {
                Debug.LogWarning($"找不到字段: {fieldName}");
            }
            #endif
        }

        #endregion

        #region 推荐数值范围

        /// <summary>
        /// 获取推荐的数值范围说明
        /// </summary>
        public static string GetRecommendedRanges()
        {
            return @"
━━━ 推荐数值范围 ━━━

🔫 基础武器参数：
• 射速: 0.5-10 次/秒
• 弹夹容量: 5-50 发
• 装弹时间: 0.8-5.0 秒
• 精度: 0.6-1.0
• 散射角度: 0-30 度

💥 投射物参数：
• 速度: 20-200 m/s
• 伤害: 5-100 点
• 射程: 50-500 米
• 生命周期: 2-15 秒

🎯 特殊效果：
• 弹跳次数: 0-5 次
• 爆炸半径: 0-15 米
• 穿透次数: 0-8 次
• 引力强度: 0-100

⚡ 性能建议：
• 同时投射物数量 < 200
• 网络同步间隔 > 0.05秒
• 复杂效果适度使用
";
        }

        #endregion
    }
}
