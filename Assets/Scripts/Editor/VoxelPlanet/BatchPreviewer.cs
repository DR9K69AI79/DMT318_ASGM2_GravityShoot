using UnityEngine;
using UnityEditor;

namespace DWHITE.Editor.VoxelPlanet
{
    /// <summary>
    /// 切换批量渲染与实例化预览
    /// </summary>
    public class BatchPreviewer : EditorWindow
    {
        [MenuItem("Tools/Voxel Planet/Batch Previewer")]
        public static void ShowWindow()
        {
            GetWindow<BatchPreviewer>("Batch Previewer");
        }

        private bool instanced = false;

        private void OnGUI()
        {
            instanced = EditorGUILayout.Toggle("Use Instancing", instanced);
            if (GUILayout.Button("Toggle Render Mode"))
            {
                ToggleRenderMode();
            }
        }

        public void ToggleRenderMode()
        {
            // 此处仅示例切换标志位，真实项目应根据情况选择渲染方式
            instanced = !instanced;
            Debug.Log($"Render mode: {(instanced ? "Instanced" : "Instantiate")}");
        }
    }
}
