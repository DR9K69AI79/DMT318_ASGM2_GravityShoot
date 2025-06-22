using System.Collections.Generic;
using UnityEngine;

namespace DWHITE.VoxelPlanet
{
    /// <summary>
    /// 基础体素数据结构与枚举
    /// </summary>
    public enum BlockID : byte { Empty, C, S, SO, CR, TC, TCR }

    public struct Voxel
    {
        public BlockID id;
        public byte mask; // 6 bit 暴露面掩码
    }

    /// <summary>
    /// 存储体素地图及顶点高度的简单容器
    /// </summary>
    public static class VoxelPlanetData
    {
        public const int N = 20; // 体素边长
        public static Voxel[,,] map = new Voxel[N, N, N];
        public static Dictionary<Vector3Int, float> vertH = new();
    }
}
