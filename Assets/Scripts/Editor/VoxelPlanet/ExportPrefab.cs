using UnityEngine;
using UnityEditor;

namespace DWHITE.Editor.VoxelPlanet
{
    /// <summary>
    /// 将生成的星球烘焙为单个Prefab
    /// </summary>
    public class ExportPrefab : EditorWindow
    {
        [MenuItem("Tools/Voxel Planet/Export Prefab")]
        public static void ShowWindow()
        {
            GetWindow<ExportPrefab>("Export Prefab");
        }

        private void OnGUI()
        {
            if (GUILayout.Button("Bake"))
            {
                Bake();
            }
        }

        public void Bake()
        {
            // 示例：创建空物体并保存为Prefab
            GameObject container = new GameObject("VoxelPlanet");
            string path = "Assets/VoxelPlanet.prefab";
            PrefabUtility.SaveAsPrefabAsset(container, path);
            DestroyImmediate(container);
            Debug.Log($"Prefab saved to {path}");
        }
    }
}
