using UnityEngine;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using System.IO;
using System.Text.RegularExpressions;

namespace DWHITE.Editor
{
    /// <summary>
    /// 构建验证器 - 检查潜在的构建问题
    /// </summary>
    public class BuildValidation : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            Debug.Log("[BuildValidation] 开始构建前验证...");
            
            // 检查UnityEditor引用泄露
            CheckUnityEditorReferences();
            
            // 检查调试代码
            CheckDebugCode();
            
            Debug.Log("[BuildValidation] 构建前验证完成");
        }

        [MenuItem("Tools/Build Validation/Check Unity Editor References")]
        public static void CheckUnityEditorReferences()
        {
            string[] scriptPaths = Directory.GetFiles(Application.dataPath, "*.cs", SearchOption.AllDirectories);
            int issueCount = 0;
            
            foreach (string path in scriptPaths)
            {
                // 跳过Editor文件夹
                if (path.Contains("\\Editor\\") || path.Contains("/Editor/"))
                    continue;
                    
                string content = File.ReadAllText(path);
                string relativePath = path.Replace(Application.dataPath, "Assets");
                
                // 检查未保护的UnityEditor using语句
                if (Regex.IsMatch(content, @"^\s*using\s+UnityEditor\s*;", RegexOptions.Multiline))
                {
                    if (!content.Contains("#if UNITY_EDITOR"))
                    {
                        Debug.LogError($"[BuildValidation] 发现未保护的UnityEditor引用: {relativePath}");
                        issueCount++;
                    }
                }
                
                // 检查直接使用UnityEditor API
                if (Regex.IsMatch(content, @"UnityEditor\.", RegexOptions.Multiline))
                {
                    if (!IsProperlyProtected(content, "UnityEditor."))
                    {
                        Debug.LogWarning($"[BuildValidation] 发现可能未保护的UnityEditor API使用: {relativePath}");
                        issueCount++;
                    }
                }
            }
            
            if (issueCount == 0)
            {
                Debug.Log("[BuildValidation] ✅ 未发现UnityEditor引用问题");
            }
            else
            {
                Debug.LogError($"[BuildValidation] ❌ 发现 {issueCount} 个潜在问题");
            }
        }
        
        [MenuItem("Tools/Build Validation/Check Debug Code")]
        public static void CheckDebugCode()
        {
            string[] scriptPaths = Directory.GetFiles(Application.dataPath, "*.cs", SearchOption.AllDirectories);
            int warningCount = 0;
            
            foreach (string path in scriptPaths)
            {
                string content = File.ReadAllText(path);
                string relativePath = path.Replace(Application.dataPath, "Assets");
                
                // 检查Debug.Log调用
                if (content.Contains("Debug.Log") && !content.Contains("// Debug allowed"))
                {
                    Debug.LogWarning($"[BuildValidation] 发现Debug.Log调用: {relativePath}");
                    warningCount++;
                }
                
                // 检查Console.WriteLine
                if (content.Contains("Console.WriteLine"))
                {
                    Debug.LogWarning($"[BuildValidation] 发现Console.WriteLine调用: {relativePath}");
                    warningCount++;
                }
            }
            
            if (warningCount == 0)
            {
                Debug.Log("[BuildValidation] ✅ 未发现调试代码问题");
            }
            else
            {
                Debug.LogWarning($"[BuildValidation] ⚠️ 发现 {warningCount} 个调试代码警告");
            }
        }
        
        private static bool IsProperlyProtected(string content, string apiCall)
        {
            string[] lines = content.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Contains(apiCall))
                {
                    // 向上查找最近的#if UNITY_EDITOR
                    for (int j = i; j >= 0; j--)
                    {
                        if (lines[j].Trim().StartsWith("#if UNITY_EDITOR"))
                        {
                            return true;
                        }
                        if (lines[j].Trim().StartsWith("#endif"))
                        {
                            break;
                        }
                    }
                    return false;
                }
            }
            return true;
        }
    }
}
