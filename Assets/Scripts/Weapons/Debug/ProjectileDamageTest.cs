using UnityEngine;
using DWHITE.Weapons;

namespace DWHITE.Weapons.Debugger
{
    /// <summary>
    /// 投射物伤害测试组件
    /// 帮助验证投射物伤害系统是否正常工作
    /// </summary>
    public class ProjectileDamageTest : MonoBehaviour
    {
        [Header("测试设置")]
        [SerializeField] private bool _enableDebugLog = true;
        [SerializeField] private bool _trackDamageEvents = true;
        
        [Header("统计信息")]
        [SerializeField] private int _totalHits = 0;
        [SerializeField] private float _totalDamageDealt = 0f;
        [SerializeField] private int _headshotCount = 0;
        
        private void Start()
        {
            if (_trackDamageEvents)
            {
                // 订阅投射物事件
                ProjectileBase.OnProjectileHit += OnProjectileHit;
                
                // 订阅伤害系统事件
                if (DamageSystem.Instance != null)
                {
                    DamageSystem.OnDamageApplied += OnDamageApplied;
                }
            }
        }
        
        private void OnDestroy()
        {
            // 取消订阅事件
            ProjectileBase.OnProjectileHit -= OnProjectileHit;
            
            if (DamageSystem.Instance != null)
            {
                DamageSystem.OnDamageApplied -= OnDamageApplied;
            }
        }
        
        private void OnProjectileHit(ProjectileBase projectile, RaycastHit hit)
        {
            _totalHits++;
            
            if (_enableDebugLog)
            {
                UnityEngine.Debug.Log($"[伤害测试] 投射物命中: {hit.collider?.name}, " +
                         $"投射物类型: {projectile.GetType().Name}, " +
                         $"伤害: {projectile.Damage}, " +
                         $"总命中次数: {_totalHits}");
            }
        }
        
        private void OnDamageApplied(DamageInfo damageInfo, DWHITE.IDamageable target)
        {
            _totalDamageDealt += damageInfo.damage;
            
            if (damageInfo.isHeadshot)
            {
                _headshotCount++;
            }
            
            if (_enableDebugLog)
            {
                MonoBehaviour targetComponent = target as MonoBehaviour;
                string targetName = targetComponent != null ? targetComponent.name : "Unknown";
                
                UnityEngine.Debug.Log($"[伤害测试] 伤害已应用: 目标={targetName}, " +
                         $"伤害={damageInfo.damage:F1}, " +
                         $"类型={damageInfo.damageType}, " +
                         $"爆头={damageInfo.isHeadshot}, " +
                         $"来源={damageInfo.source?.name ?? "Unknown"}, " +
                         $"累计伤害: {_totalDamageDealt:F1}");
            }
        }
        
        /// <summary>
        /// 重置统计信息
        /// </summary>
        [ContextMenu("重置统计")]
        public void ResetStats()
        {
            _totalHits = 0;
            _totalDamageDealt = 0f;
            _headshotCount = 0;
            
            UnityEngine.Debug.Log("[伤害测试] 统计信息已重置");
        }
        
        /// <summary>
        /// 打印当前统计信息
        /// </summary>
        [ContextMenu("打印统计")]
        public void PrintStats()
        {
            UnityEngine.Debug.Log($"[伤害测试] === 当前统计 ===\n" +
                     $"总命中次数: {_totalHits}\n" +
                     $"累计伤害: {_totalDamageDealt:F1}\n" +
                     $"爆头次数: {_headshotCount}\n" +
                     $"平均伤害: {(_totalHits > 0 ? _totalDamageDealt / _totalHits : 0):F1}\n" +
                     $"爆头率: {(_totalHits > 0 ? (float)_headshotCount / _totalHits * 100 : 0):F1}%");
        }
        
        private void OnGUI()
        {
            if (!_enableDebugLog) return;
            
            GUILayout.BeginArea(new Rect(10, 10, 300, 150));
            GUILayout.Label("=== 投射物伤害测试 ===");
            GUILayout.Label($"总命中: {_totalHits}");
            GUILayout.Label($"累计伤害: {_totalDamageDealt:F1}");
            GUILayout.Label($"爆头: {_headshotCount}");
            GUILayout.Label($"平均伤害: {(_totalHits > 0 ? _totalDamageDealt / _totalHits : 0):F1}");
            
            if (GUILayout.Button("重置统计"))
            {
                ResetStats();
            }
            GUILayout.EndArea();
        }
    }
}
