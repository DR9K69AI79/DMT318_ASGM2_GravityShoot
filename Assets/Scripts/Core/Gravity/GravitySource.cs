using UnityEngine;

namespace DWHITE {	
	/// <summary>
	/// 所有引力体的抽象基类
	/// 提供统一的重力计算接口
	/// </summary>
	public abstract class GravitySource : MonoBehaviour
	{
	    //private bool _isEnabled = true;
	
	    /// <summary>
	    /// 计算指定位置的重力加速度
	    /// </summary>
	    /// <param name="position">世界坐标位置</param>
	    /// <returns>重力加速度向量 (m/s²)</returns>
	    public abstract Vector3 GetGravity(Vector3 position);
	
	    /// <summary>
	    /// 获取指定位置的"上"轴向量（通常与重力方向相反）
	    /// </summary>
	    /// <param name="position">世界坐标位置</param>
	    /// <returns>标准化的上轴向量</returns>
	    public virtual Vector3 GetUpAxis(Vector3 position)
	    {
	        return -GetGravity(position).normalized;
	    }
	
	    protected virtual void OnEnable()
	    {
	        CustomGravity.Register(this);
	    }
	
	    protected virtual void OnDisable()
	    {
	        CustomGravity.Unregister(this);
	    }
	
	    protected virtual void OnDrawGizmosSelected()
	    {
	        // 子类可以重写此方法来显示重力场可视化
	    }
	}
}
