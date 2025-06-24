using UnityEngine;
using DWHITE.Weapons;

namespace DWHITE.Debugger
{
    /// <summary>
    /// 投射物调试器 - 用于诊断投射物不移动的问题
    /// 将此脚本添加到投射物预制体上以获取详细的调试信息
    /// </summary>
    public class ProjectileDebugger : MonoBehaviour
    {
        private Rigidbody rb;
        private ProjectileBase projectileBase;
        private float debugInterval = 1f;
        private float lastDebugTime;

        void Start()
        {
            rb = GetComponent<Rigidbody>();
            projectileBase = GetComponent<ProjectileBase>();
            
            Debug.Log("=== 投射物调试器启动 ===");
            Debug.Log($"GameObject: {gameObject.name}");
            Debug.Log($"Rigidbody: {(rb != null ? "存在" : "缺失")}");
            Debug.Log($"ProjectileBase: {(projectileBase != null ? "存在" : "缺失")}");
            
            if (rb != null)
            {
                Debug.Log($"初始速度: {rb.velocity}");
                Debug.Log($"运动学模式: {rb.isKinematic}");
                Debug.Log($"使用重力: {rb.useGravity}");
                Debug.Log($"质量: {rb.mass}");
                Debug.Log($"阻力: {rb.drag}");
            }
            
            // 检查是否有CustomGravityRigidbody组件
            var customGravity = GetComponent<CustomGravityRigidbody>();
            Debug.Log($"CustomGravityRigidbody: {(customGravity != null ? "存在" : "缺失")}");
        }

        void Update()
        {
            if (Time.time - lastDebugTime >= debugInterval)
            {
                lastDebugTime = Time.time;
                
                if (rb != null)
                {
                    Debug.Log($"[{Time.time:F1}s] 位置: {transform.position}, 速度: {rb.velocity} (大小: {rb.velocity.magnitude:F2})");
                    
                    if (rb.velocity.magnitude < 0.1f)
                    {
                        Debug.LogWarning($"[{Time.time:F1}s] 投射物几乎静止! 可能的问题:");
                        Debug.LogWarning($"  - 运动学模式: {rb.isKinematic}");
                        Debug.LogWarning($"  - 速度为零: {rb.velocity}");
                        Debug.LogWarning($"  - 阻力过大: {rb.drag}");
                        Debug.LogWarning($"  - 质量设置: {rb.mass}");
                    }
                }
            }
        }

        /// <summary>
        /// 手动设置投射物速度 - 用于调试
        /// </summary>
        [ContextMenu("设置测试速度")]
        public void SetTestVelocity()
        {
            if (rb != null)
            {
                Vector3 testVelocity = transform.forward * 20f;
                rb.velocity = testVelocity;
                Debug.Log($"手动设置测试速度: {testVelocity}");
            }
        }

        /// <summary>
        /// 重置物理状态 - 用于调试
        /// </summary>
        [ContextMenu("重置物理状态")]
        public void ResetPhysics()
        {
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.useGravity = true;
                rb.drag = 0f;
                rb.mass = 1f;
                Debug.Log("物理状态已重置");
            }
        }

        /// <summary>
        /// 显示详细状态信息
        /// </summary>
        [ContextMenu("显示详细状态")]
        public void ShowDetailedStatus()
        {
            Debug.Log("=== 投射物详细状态 ===");
            Debug.Log($"GameObject: {gameObject.name}");
            Debug.Log($"位置: {transform.position}");
            Debug.Log($"旋转: {transform.rotation.eulerAngles}");
            Debug.Log($"缩放: {transform.localScale}");
            
            if (rb != null)
            {
                Debug.Log($"Rigidbody状态:");
                Debug.Log($"  速度: {rb.velocity} (大小: {rb.velocity.magnitude:F2})");
                Debug.Log($"  角速度: {rb.angularVelocity}");
                Debug.Log($"  运动学模式: {rb.isKinematic}");
                Debug.Log($"  使用重力: {rb.useGravity}");
                Debug.Log($"  质量: {rb.mass}");
                Debug.Log($"  阻力: {rb.drag}");
                Debug.Log($"  角阻力: {rb.angularDrag}");
                Debug.Log($"  睡眠状态: {rb.IsSleeping()}");
                Debug.Log($"  约束: {rb.constraints}");
            }
            
            if (projectileBase != null)
            {
                Debug.Log($"ProjectileBase状态:");
                Debug.Log($"  速度: {projectileBase.Speed}");
                Debug.Log($"  伤害: {projectileBase.Damage}");
                Debug.Log($"  生命周期: {projectileBase.Lifetime}");
                Debug.Log($"  已命中: {projectileBase.HasHit}");
                Debug.Log($"  已销毁: {projectileBase.IsDestroyed}");
            }
            
            // 检查其他组件
            var components = GetComponents<Component>();
            Debug.Log($"所有组件:");
            foreach (var comp in components)
            {
                Debug.Log($"  - {comp.GetType().Name}");
            }
        }
    }
}
