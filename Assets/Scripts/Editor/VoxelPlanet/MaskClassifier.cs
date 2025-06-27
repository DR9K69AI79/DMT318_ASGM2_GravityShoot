using UnityEngine;
using UnityEditor;
using static DWHITE.VoxelPlanet.VoxelPlanetData;

namespace DWHITE.Editor.VoxelPlanet
{
    /// <summary>
    /// 6 bit 掩码分类工具
    /// </summary>
    public class MaskClassifier : EditorWindow
    {
        [MenuItem("Tools/Voxel Planet/Mask Classifier")]
        public static void ShowWindow()
        {
            GetWindow<MaskClassifier>("Mask Classifier");
        }

        private void OnGUI()
        {
            if (GUILayout.Button("Classify Masks"))
            {
                ClassifyMasks();
            }
        }

        public void ClassifyMasks()
        {
            for (int x = 0; x < N; x++)
            {
                for (int y = 0; y < N; y++)
                {
                    for (int z = 0; z < N; z++)
                    {
                        if (map[x, y, z].id == BlockID.Empty) continue;
                        byte m = 0;
                        if (IsEmpty(x + 1, y, z)) m |= 1;
                        if (IsEmpty(x - 1, y, z)) m |= 2;
                        if (IsEmpty(x, y + 1, z)) m |= 4;
                        if (IsEmpty(x, y - 1, z)) m |= 8;
                        if (IsEmpty(x, y, z + 1)) m |= 16;
                        if (IsEmpty(x, y, z - 1)) m |= 32;
                        map[x, y, z].mask = m;
                    }
                }
            }
            Debug.Log("Mask classification complete");
        }

        private static bool IsEmpty(int x, int y, int z)
        {
            if (x < 0 || x >= N || y < 0 || y >= N || z < 0 || z >= N) return true;
            return map[x, y, z].id == BlockID.Empty;
        }
    }
}
