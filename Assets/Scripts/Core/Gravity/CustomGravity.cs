using System.Collections.Generic;
using UnityEngine;

namespace DWHITE {	
	/// <summary>
	/// 自定义重力系统的静态管理器
	/// 负责注册重力源并计算累积重力
	/// </summary>
	public static class CustomGravity
	{
	    private static List<GravitySource> _sources = new List<GravitySource>();
	    
	    /// <summary>
	    /// 注册重力源
	    /// </summary>
	    /// <param name="source">要注册的重力源</param>
	    public static void Register(GravitySource source)
	    {
	        if (source != null && !_sources.Contains(source))
	        {
	            _sources.Add(source);
	        }
	    }
	    
	    /// <summary>
	    /// 注销重力源
	    /// </summary>
	    /// <param name="source">要注销的重力源</param>
	    public static void Unregister(GravitySource source)
	    {
	        if (source != null)
	        {
	            _sources.Remove(source);
	        }
	    }
	    
	    /// <summary>
	    /// 获取指定位置的累积重力
	    /// </summary>
	    /// <param name="position">世界坐标位置</param>
	    /// <returns>累积的重力加速度向量</returns>
	    public static Vector3 GetGravity(Vector3 position)
	    {
	        Vector3 gravity = Vector3.zero;
	        
	        for (int i = 0; i < _sources.Count; i++)
	        {
	            if (_sources[i] != null && _sources[i].gameObject.activeInHierarchy)
	            {
	                gravity += _sources[i].GetGravity(position);
	            }
	        }
	        
	        return gravity;
	    }
	    
	    /// <summary>
	    /// 获取指定位置的累积重力和上轴方向
	    /// </summary>
	    /// <param name="position">世界坐标位置</param>
	    /// <param name="upAxis">输出的上轴方向</param>
	    /// <returns>累积的重力加速度向量</returns>
	    public static Vector3 GetGravity(Vector3 position, out Vector3 upAxis)
	    {
	        Vector3 gravity = GetGravity(position);
	        
	        if (gravity.sqrMagnitude > 0f)
	        {
	            upAxis = -gravity.normalized;
	        }
	        else
	        {
	            upAxis = Vector3.up; // 默认向上
	        }
	        
	        return gravity;
	    }
	    
	    /// <summary>
	    /// 获取指定位置最强重力源的上轴方向
	    /// </summary>
	    /// <param name="position">世界坐标位置</param>
	    /// <returns>标准化的上轴向量</returns>
	    public static Vector3 GetUpAxis(Vector3 position)
	    {
	        float strongestGravity = 0f;
	        Vector3 upAxis = Vector3.up;
	        
	        for (int i = 0; i < _sources.Count; i++)
	        {
	            if (_sources[i] != null && _sources[i].gameObject.activeInHierarchy)
	            {
	                Vector3 gravity = _sources[i].GetGravity(position);
	                float gravityMagnitude = gravity.magnitude;
	                
	                if (gravityMagnitude > strongestGravity)
	                {
	                    strongestGravity = gravityMagnitude;
	                    upAxis = _sources[i].GetUpAxis(position);
	                }
	            }
	        }
	        
	        return upAxis;
	    }
	    
	    /// <summary>
	    /// 清理无效的重力源引用
	    /// </summary>
	    public static void CleanUp()
	    {
	        for (int i = _sources.Count - 1; i >= 0; i--)
	        {
	            if (_sources[i] == null)
	            {
	                _sources.RemoveAt(i);
	            }
	        }
	    }
	    
	    /// <summary>
	    /// 获取当前注册的重力源数量
	    /// </summary>
	    public static int SourceCount => _sources.Count;
	    
	    /// <summary>
	    /// 获取所有注册的重力源（只读）
	    /// </summary>
	    public static IReadOnlyList<GravitySource> Sources => _sources.AsReadOnly();
	}
}
