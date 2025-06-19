using UnityEngine;
using UnityEditor;

namespace DWHITE.Editor
{
    /// <summary>
    /// 构建修复工具 - 自动修复常见的构建问题
    /// </summary>
    public class BuildFixer
    {
        [MenuItem("Tools/Build Fixer/Fix Common Build Issues")]
        public static void FixCommonBuildIssues()
        {
            Debug.Log("[BuildFixer] 开始修复常见构建问题...");
            
            // 1. 设置正确的脚本编译设置
            SetScriptingDefineSymbols();
            
            // 2. 检查并设置正确的API兼容性级别
            SetApiCompatibilityLevel();
            
            // 3. 确保正确的.NET版本
            SetDotNetVersion();
            
            // 4. 设置构建选项
            SetBuildOptions();
            
            Debug.Log("[BuildFixer] 构建问题修复完成！");
            
            // 保存项目设置
            AssetDatabase.SaveAssets();
        }
        
        [MenuItem("Tools/Build Fixer/Remove Debug Defines")]
        public static void RemoveDebugDefines()
        {
            BuildTargetGroup targetGroup = EditorUserBuildSettings.selectedBuildTargetGroup;
            string defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(targetGroup);
            
            // 移除调试相关的宏定义
            defines = defines.Replace("ENABLE_DEBUG", "");
            defines = defines.Replace("DEBUG", "");
            defines = defines.Replace("DEVELOPMENT_BUILD", "");
            
            // 清理多余的分号
            defines = defines.Replace(";;", ";");
            defines = defines.Trim(';');
            
            PlayerSettings.SetScriptingDefineSymbolsForGroup(targetGroup, defines);
            Debug.Log($"[BuildFixer] 已移除调试宏定义，当前定义: {defines}");
        }
        
        private static void SetScriptingDefineSymbols()
        {
            BuildTargetGroup targetGroup = EditorUserBuildSettings.selectedBuildTargetGroup;
            string currentDefines = PlayerSettings.GetScriptingDefineSymbolsForGroup(targetGroup);
            
            // 确保必要的宏定义存在
            if (!currentDefines.Contains("PUN_2_OR_NEWER"))
            {
                if (!string.IsNullOrEmpty(currentDefines))
                    currentDefines += ";";
                currentDefines += "PUN_2_OR_NEWER";
            }
            
            PlayerSettings.SetScriptingDefineSymbolsForGroup(targetGroup, currentDefines);
            Debug.Log($"[BuildFixer] 脚本定义符号已设置: {currentDefines}");
        }
        
        private static void SetApiCompatibilityLevel()
        {
            // 设置为.NET Standard 2.1（推荐用于现代Unity项目）
            PlayerSettings.SetApiCompatibilityLevel(EditorUserBuildSettings.selectedBuildTargetGroup, ApiCompatibilityLevel.NET_Standard_2_0);
            Debug.Log("[BuildFixer] API兼容性级别已设置为.NET Standard 2.0");
        }
        
        private static void SetDotNetVersion()
        {
            // 确保使用正确的.NET版本
            PlayerSettings.SetScriptingBackend(EditorUserBuildSettings.selectedBuildTargetGroup, ScriptingImplementation.Mono2x);
            Debug.Log("[BuildFixer] 脚本后端已设置为Mono");
        }
        
        private static void SetBuildOptions()
        {
            // 设置开发版本选项（用于调试）
            EditorUserBuildSettings.development = false;
            EditorUserBuildSettings.allowDebugging = false;
            EditorUserBuildSettings.connectProfiler = false;
            
            Debug.Log("[BuildFixer] 构建选项已设置为发布模式");
        }
        
        [MenuItem("Tools/Build Fixer/Show Current Build Settings")]
        public static void ShowCurrentBuildSettings()
        {
            BuildTargetGroup targetGroup = EditorUserBuildSettings.selectedBuildTargetGroup;
            string defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(targetGroup);
            
            Debug.Log("=== 当前构建设置 ===");
            Debug.Log($"目标平台: {EditorUserBuildSettings.activeBuildTarget}");
            Debug.Log($"目标组: {targetGroup}");
            Debug.Log($"脚本定义符号: {defines}");
            Debug.Log($"API兼容性: {PlayerSettings.GetApiCompatibilityLevel(targetGroup)}");
            Debug.Log($"脚本后端: {PlayerSettings.GetScriptingBackend(targetGroup)}");
            Debug.Log($"开发构建: {EditorUserBuildSettings.development}");
            Debug.Log($"允许调试: {EditorUserBuildSettings.allowDebugging}");
            Debug.Log($"连接Profiler: {EditorUserBuildSettings.connectProfiler}");
        }
    }
}
