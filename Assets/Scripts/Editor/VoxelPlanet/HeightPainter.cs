using UnityEngine;
using UnityEditor;
using static DWHITE.VoxelPlanet.VoxelPlanetData;

namespace DWHITE.Editor.VoxelPlanet
{
    /// <summary>
    /// 生成六面高度场的编辑器工具
    /// </summary>
    public class HeightPainter : EditorWindow
    {
        [MenuItem("Tools/Voxel Planet/Height Painter")]
        public static void ShowWindow()
        {
            GetWindow<HeightPainter>("Height Painter");
        }

        private float freq = 0.2f;
        private int maxLayers = 4;
        private int seed = 0;

        private void OnGUI()
        {
            freq = EditorGUILayout.Slider("Noise Freq", freq, 0.05f, 0.5f);
            maxLayers = EditorGUILayout.IntSlider("Max Layers", maxLayers, 1, 8);
            seed = EditorGUILayout.IntField("Seed", seed);

            if (GUILayout.Button("Generate Heights"))
            {
                GenerateHeights();
            }
        }

        /// <summary>
        /// 根据噪声生成六面统一高度
        /// </summary>
        public void GenerateHeights()
        {
            Random.InitState(seed);
            vertH.Clear();
            var faces = new Vector3Int[]
            {
                Vector3Int.right, Vector3Int.left,
                Vector3Int.up, Vector3Int.down,
                Vector3Int.forward, Vector3Int.back
            };

            foreach (var face in faces)
            {
                for (int u = 0; u <= N; u++)
                {
                    for (int v = 0; v <= N; v++)
                    {
                        Vector3Int vWorld = FaceToWorld(face, u, v);
                        if (!vertH.ContainsKey(vWorld))
                        {
                            float n = Mathf.PerlinNoise(
                                (vWorld.x + seed) * freq,
                                (vWorld.y + seed) * freq);
                            vertH[vWorld] = n;
                        }
                    }
                }
            }

            foreach (var kvp in vertH)
            {
                int layers = Mathf.RoundToInt(kvp.Value * maxLayers);
                Vector3Int cell = kvp.Key;
                for (int i = 0; i < layers; i++)
                {
                    Vector3Int below = cell - new Vector3Int(0, i, 0);
                    if (InBounds(below))
                    {
                        map[below.x, below.y, below.z].id = BlockID.C;
                    }
                }
            }

            Debug.Log($"Generated {vertH.Count} vertices");
        }

        private static Vector3Int FaceToWorld(Vector3Int face, int u, int v)
        {
            // 简化版映射，仅演示作用
            if (face == Vector3Int.up || face == Vector3Int.down)
                return new Vector3Int(u, face.y == 1 ? N : 0, v);
            if (face == Vector3Int.right || face == Vector3Int.left)
                return new Vector3Int(face.x == 1 ? N : 0, u, v);
            return new Vector3Int(u, v, face.z == 1 ? N : 0);
        }

        private static bool InBounds(Vector3Int c)
        {
            return c.x >= 0 && c.x < N && c.y >= 0 && c.y < N && c.z >= 0 && c.z < N;
        }
    }
}
