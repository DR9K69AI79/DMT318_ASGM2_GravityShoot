using UnityEngine;
using UnityEditor;
using DWHITE.Weapons;

namespace DWHITE.Weapons.Editor
{
    /// <summary>
    /// WeaponData的自定义编辑器
    /// 提供快速配置按钮和验证功能
    /// </summary>
    [CustomEditor(typeof(WeaponData))]
    public class WeaponDataEditor : UnityEditor.Editor
    {
        private WeaponData weaponData;
        private bool showPresets = true;
        private bool showValidation = true;
        private bool showRecommendations = false;

        private void OnEnable()
        {
            weaponData = (WeaponData)target;
        }

        public override void OnInspectorGUI()
        {
            // 绘制默认Inspector
            DrawDefaultInspector();

            EditorGUILayout.Space(15);

            // 快速配置区域
            DrawQuickConfigSection();

            // 验证区域
            DrawValidationSection();

            // 推荐数值区域
            DrawRecommendationsSection();

            // 应用修改
            if (GUI.changed)
            {
                EditorUtility.SetDirty(weaponData);
            }
        }

        private void DrawQuickConfigSection()
        {
            EditorGUILayout.BeginVertical("box");
            
            showPresets = EditorGUILayout.Foldout(showPresets, "🔧 快速配置预设", true);
            
            if (showPresets)
            {
                EditorGUILayout.HelpBox("使用预设可以快速配置常见武器类型的参数", MessageType.Info);
                
                EditorGUILayout.BeginHorizontal();
                
                if (GUILayout.Button("🎯 突击步枪", GUILayout.Height(30)))
                {
                    WeaponPresets.ApplyAssaultRiflePreset(weaponData);
                    EditorUtility.SetDirty(weaponData);
                }
                
                if (GUILayout.Button("🔍 狙击步枪", GUILayout.Height(30)))
                {
                    WeaponPresets.ApplySniperRiflePreset(weaponData);
                    EditorUtility.SetDirty(weaponData);
                }
                
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.BeginHorizontal();
                
                if (GUILayout.Button("💥 霰弹枪", GUILayout.Height(30)))
                {
                    WeaponPresets.ApplyShotgunPreset(weaponData);
                    EditorUtility.SetDirty(weaponData);
                }
                
                if (GUILayout.Button("🔫 手枪", GUILayout.Height(30)))
                {
                    WeaponPresets.ApplyPistolPreset(weaponData);
                    EditorUtility.SetDirty(weaponData);
                }
                
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(10);

                // 投射物预设区域
                EditorGUILayout.LabelField("投射物预设:", EditorStyles.boldLabel);
                
                EditorGUILayout.BeginHorizontal();
                
                if (GUILayout.Button("标准弹", GUILayout.Height(25)))
                {
                    CreateAndAssignProjectileSettings(WeaponPresets.CreateStandardBulletPreset());
                }
                
                if (GUILayout.Button("爆炸弹", GUILayout.Height(25)))
                {
                    CreateAndAssignProjectileSettings(WeaponPresets.CreateExplosiveBulletPreset());
                }
                
                if (GUILayout.Button("弹跳弹", GUILayout.Height(25)))
                {
                    CreateAndAssignProjectileSettings(WeaponPresets.CreateBouncingBulletPreset());
                }
                
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.BeginHorizontal();
                
                if (GUILayout.Button("重力弹", GUILayout.Height(25)))
                {
                    CreateAndAssignProjectileSettings(WeaponPresets.CreateGravityBulletPreset());
                }
                
                if (GUILayout.Button("穿透弹", GUILayout.Height(25)))
                {
                    CreateAndAssignProjectileSettings(WeaponPresets.CreatePiercingBulletPreset());
                }
                
                EditorGUILayout.EndHorizontal();
            }
            
            EditorGUILayout.EndVertical();
        }

        private void DrawValidationSection()
        {
            EditorGUILayout.BeginVertical("box");
            
            showValidation = EditorGUILayout.Foldout(showValidation, "✅ 配置验证", true);
            
            if (showValidation)
            {
                EditorGUILayout.BeginHorizontal();
                
                if (GUILayout.Button("🔍 验证配置", GUILayout.Height(25)))
                {
                    string validation = WeaponPresets.ValidateWeaponConfiguration(weaponData);
                    Debug.Log($"武器配置验证结果:\n{validation}");
                }
                
                EditorGUILayout.EndHorizontal();
                
                // 显示一些关键指标
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("配置概览:", EditorStyles.boldLabel);
                
                float fireInterval = weaponData.FireRate > 0 ? 1f / weaponData.FireRate : 0f;
                EditorGUILayout.LabelField($"• 射击间隔: {fireInterval:F2}秒");
                EditorGUILayout.LabelField($"• DPS估算: {(weaponData.Damage * weaponData.FireRate):F1}");
                EditorGUILayout.LabelField($"• 连射时长: {(weaponData.MagazineSize / weaponData.FireRate):F1}秒");
                
                if (weaponData.UseProjectileSettings && weaponData.ProjectileSettings != null)
                {
                    var settings = weaponData.ProjectileSettings;
                    float timeToMaxRange = settings.MaxRange / settings.Speed;
                    EditorGUILayout.LabelField($"• 到达最远距离: {timeToMaxRange:F1}秒");
                }
            }
            
            EditorGUILayout.EndVertical();
        }

        private void DrawRecommendationsSection()
        {
            EditorGUILayout.BeginVertical("box");
            
            showRecommendations = EditorGUILayout.Foldout(showRecommendations, "📚 推荐数值范围", true);
            
            if (showRecommendations)
            {
                EditorGUILayout.HelpBox(WeaponPresets.GetRecommendedRanges(), MessageType.Info);
                
                if (GUILayout.Button("复制到剪贴板"))
                {
                    EditorGUIUtility.systemCopyBuffer = WeaponPresets.GetRecommendedRanges();
                    Debug.Log("推荐数值范围已复制到剪贴板");
                }
            }
            
            EditorGUILayout.EndVertical();
        }

        private void CreateAndAssignProjectileSettings(ProjectileSettings preset)
        {
            // 获取当前ProjectileSettings的字段并复制到武器数据中
            // 由于ProjectileSettings是嵌入类，直接赋值
            var currentSettings = weaponData.ProjectileSettings;
            if (currentSettings == null)
            {
                Debug.LogWarning("当前武器没有ProjectileSettings，请先创建一个");
                return;
            }

            // 使用反射复制所有字段
            var sourceFields = preset.GetType().GetFields(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var targetFields = currentSettings.GetType().GetFields(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            foreach (var sourceField in sourceFields)
            {
                var targetField = System.Array.Find(targetFields, f => f.Name == sourceField.Name);
                if (targetField != null)
                {
                    targetField.SetValue(currentSettings, sourceField.GetValue(preset));
                }
            }

            EditorUtility.SetDirty(weaponData);
            Debug.Log("已应用投射物预设配置");
        }
    }
}
