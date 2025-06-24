using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DWHITE.Weapons
{
    /// <summary>
    /// WeaponData配置迁移工具
    /// 帮助将传统配置迁移到ProjectileSettings结构
    /// </summary>
    public static class WeaponDataMigrationTool
    {        /// <summary>
        /// 创建基于传统配置的ProjectileSettings
        /// 注意：由于传统字段已在第十阶段清理中移除，此方法现在创建默认配置
        /// </summary>
        public static ProjectileSettings CreateProjectileSettingsFromLegacy(WeaponData weaponData)
        {
            if (weaponData == null) return null;
            
            // 由于传统配置字段已被移除，创建标准预设配置
            var settings = new ProjectileSettings();
            
            // 应用标准子弹预设
            // settings = WeaponPresets.CreateStandardBulletSettings(); // 如果有这个方法的话
            
            Debug.Log($"[配置迁移] 为武器 {weaponData.WeaponName} 创建默认ProjectileSettings（传统字段已移除）");
            
            return settings;
        }
          /// <summary>
        /// 批量迁移所有WeaponData资产
        /// 确保所有武器都启用ProjectileSettings并拥有配置对象
        /// </summary>
        public static void MigrateAllWeaponData()
        {
#if UNITY_EDITOR
            string[] guids = AssetDatabase.FindAssets("t:WeaponData");
            int migratedCount = 0;
            int configuredCount = 0;
            
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                WeaponData weaponData = AssetDatabase.LoadAssetAtPath<WeaponData>(path);
                
                if (weaponData != null)
                {
                    bool needsMigration = false;
                    
                    // 检查是否启用了ProjectileSettings
                    if (!weaponData.UseProjectileSettings)
                    {
                        weaponData.EnableProjectileSettings();
                        needsMigration = true;
                        migratedCount++;
                        Debug.Log($"[配置迁移] 已为武器 {weaponData.WeaponName} 启用ProjectileSettings");
                    }
                    
                    // 检查是否有ProjectileSettings配置对象
                    if (weaponData.ProjectileSettings == null)
                    {
                        var newSettings = weaponData.GetOrCreateProjectileSettings();
                        needsMigration = true;
                        configuredCount++;
                        Debug.Log($"[配置迁移] 已为武器 {weaponData.WeaponName} 创建ProjectileSettings配置");
                    }
                    
                    // 标记为已修改
                    if (needsMigration)
                    {
                        EditorUtility.SetDirty(weaponData);
                    }
                }
            }
            
            if (migratedCount > 0 || configuredCount > 0)
            {
                AssetDatabase.SaveAssets();
                Debug.Log($"[配置迁移] 迁移完成 - 启用ProjectileSettings: {migratedCount}个，创建配置对象: {configuredCount}个");
            }
            else
            {
                Debug.Log("[配置迁移] 所有武器配置都已是最新状态");
            }
#endif
        }
          /// <summary>
        /// 验证所有武器配置的一致性
        /// </summary>
        public static void ValidateAllWeaponData()
        {
#if UNITY_EDITOR
            string[] guids = AssetDatabase.FindAssets("t:WeaponData");
            int validatedCount = 0;
            int inconsistentCount = 0;
            
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                WeaponData weaponData = AssetDatabase.LoadAssetAtPath<WeaponData>(path);
                
                if (weaponData != null)
                {
                    validatedCount++;
                    
                    // 检查是否使用ProjectileSettings
                    if (!weaponData.UseProjectileSettings)
                    {
                        inconsistentCount++;
                        Debug.LogWarning($"[配置验证] 武器 {weaponData.WeaponName} 仍在使用传统配置，建议迁移到ProjectileSettings");
                    }
                    else if (weaponData.ProjectileSettings == null)
                    {
                        inconsistentCount++;
                        Debug.LogWarning($"[配置验证] 武器 {weaponData.WeaponName} 启用了ProjectileSettings但未设置配置对象");
                    }
                }
            }
            
            Debug.Log($"[配置验证] 验证完成: {validatedCount} 个武器，{inconsistentCount} 个需要注意");
#endif
        }
        
        /// <summary>
        /// 报告当前配置状态
        /// </summary>
        public static void ReportConfigurationStatus()
        {
#if UNITY_EDITOR
            string[] guids = AssetDatabase.FindAssets("t:WeaponData");
            int totalCount = 0;
            int usingProjectileSettings = 0;
            int usingLegacySettings = 0;
            
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                WeaponData weaponData = AssetDatabase.LoadAssetAtPath<WeaponData>(path);
                
                if (weaponData != null)
                {
                    totalCount++;
                    
                    if (weaponData.UseProjectileSettings)
                    {
                        usingProjectileSettings++;
                    }
                    else
                    {
                        usingLegacySettings++;
                    }
                }
            }
            
            Debug.Log("=== 武器配置状态报告 ===");
            Debug.Log($"总武器数量: {totalCount}");
            Debug.Log($"使用ProjectileSettings: {usingProjectileSettings}");
            Debug.Log($"使用传统配置: {usingLegacySettings}");
            Debug.Log($"迁移进度: {(totalCount > 0 ? (float)usingProjectileSettings / totalCount * 100f : 0f):F1}%");
#endif
        }
    }
}

#if UNITY_EDITOR
namespace DWHITE.Weapons.Editor
{
    /// <summary>
    /// WeaponData配置迁移编辑器工具
    /// </summary>
    public class WeaponDataMigrationEditor : EditorWindow
    {
        [MenuItem("GravityShoot/武器配置迁移工具")]
        public static void ShowWindow()
        {
            GetWindow<WeaponDataMigrationEditor>("武器配置迁移");
        }
        
        private void OnGUI()
        {
            GUILayout.Label("武器配置迁移工具", EditorStyles.boldLabel);
            GUILayout.Space(10);
            
            if (GUILayout.Button("报告配置状态"))
            {
                WeaponDataMigrationTool.ReportConfigurationStatus();
            }
            
            GUILayout.Space(5);
              if (GUILayout.Button("验证配置状态"))
            {
                WeaponDataMigrationTool.ValidateAllWeaponData();
            }
            
            GUILayout.Space(5);
            
            if (GUILayout.Button("确保所有武器使用ProjectileSettings"))
            {
                if (EditorUtility.DisplayDialog("确认迁移", 
                    "这将确保所有武器都启用ProjectileSettings并拥有配置对象。是否继续？", 
                    "确认", "取消"))
                {
                    WeaponDataMigrationTool.MigrateAllWeaponData();
                }
            }
            
            GUILayout.Space(10);
            
            GUILayout.Label("说明:", EditorStyles.boldLabel);
            GUILayout.Label("• 配置状态报告：显示当前项目中武器配置的使用情况");
            GUILayout.Label("• 验证配置状态：检查武器是否正确使用ProjectileSettings");
            GUILayout.Label("• 确保ProjectileSettings：为所有武器启用并创建ProjectileSettings");
            GUILayout.Label("• 注意：传统配置字段已在第十阶段清理中移除");
        }
    }
}
#endif
