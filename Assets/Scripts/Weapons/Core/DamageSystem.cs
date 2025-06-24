using UnityEngine;
using System.Collections.Generic;

namespace DWHITE.Weapons
{
    /// <summary>
    /// 统一伤害处理系统
    /// 负责处理所有类型的伤害计算、应用和事件分发
    /// </summary>
    public class DamageSystem : MonoBehaviour
    {
        #region 单例模式
        
        private static DamageSystem _instance;
        public static DamageSystem Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<DamageSystem>();
                    if (_instance == null)
                    {
                        GameObject damageSystemObj = new GameObject("DamageSystem");
                        _instance = damageSystemObj.AddComponent<DamageSystem>();
                        DontDestroyOnLoad(damageSystemObj);
                    }
                }
                return _instance;
            }
        }
        
        #endregion
        
        #region 事件定义
        
        /// <summary>
        /// 伤害应用前事件 - 可用于修改伤害值
        /// </summary>
        public static System.Action<DamageInfo> OnDamagePreApply;
        
        /// <summary>
        /// 伤害应用后事件 - 用于UI更新、特效等
        /// </summary>
        public static System.Action<DamageInfo, DWHITE.IDamageable> OnDamageApplied;
        
        /// <summary>
        /// 目标死亡事件
        /// </summary>
        public static System.Action<DWHITE.IDamageable, DamageInfo> OnTargetKilled;
        
        #endregion
        
        #region 配置
        
        [Header("调试设置")]
        [SerializeField] private bool _enableDebugLog = true;
        [SerializeField] private bool _showDamageNumbers = true;
        
        [Header("伤害数字显示")]
        [SerializeField] private GameObject _damageNumberPrefab;
        [SerializeField] private Transform _damageNumberParent;
        
        #endregion
        
        #region Unity生命周期
        
        private void Awake()
        {
            // 确保单例
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
                return;
            }
            
            InitializeDamageSystem();
        }
        
        #endregion
        
        #region 初始化
        
        /// <summary>
        /// 初始化伤害系统
        /// </summary>
        private void InitializeDamageSystem()
        {
            if (_enableDebugLog)
            {
                Debug.Log("[伤害系统] DamageSystem 初始化完成");
            }
            
            // 创建伤害数字父对象
            if (_damageNumberParent == null)
            {
                GameObject uiParent = GameObject.Find("DamageNumbers");
                if (uiParent == null)
                {
                    uiParent = new GameObject("DamageNumbers");
                    _damageNumberParent = uiParent.transform;
                }
                else
                {
                    _damageNumberParent = uiParent.transform;
                }
            }
        }
        
        #endregion
        
        #region 核心功能
        
        /// <summary>
        /// 应用伤害到目标
        /// </summary>
        /// <param name="target">目标对象</param>
        /// <param name="damageInfo">伤害信息</param>
        public static void ApplyDamage(DWHITE.IDamageable target, DamageInfo damageInfo)
        {
            if (target == null)
            {
                if (Instance._enableDebugLog)
                    Debug.LogWarning("[伤害系统] 目标为空，无法应用伤害");
                return;
            }
            
            if (!target.IsAlive)
            {
                if (Instance._enableDebugLog)
                    Debug.Log("[伤害系统] 目标已死亡，跳过伤害");
                return;
            }
            
            // 触发伤害前处理事件
            OnDamagePreApply?.Invoke(damageInfo);
            
            // 记录目标应用伤害前的生命值
            float healthBefore = target.GetCurrentHealth();
            
            // 应用伤害
            target.TakeDamage(damageInfo.damage, damageInfo.hitPoint, damageInfo.hitDirection);
            
            // 记录实际造成的伤害
            float actualDamage = healthBefore - target.GetCurrentHealth();
            damageInfo.damage = actualDamage; // 更新为实际伤害值
            
            if (Instance._enableDebugLog)
            {
                Debug.Log($"[伤害系统] 对 {target} 造成 {actualDamage:F1} 伤害 " +
                         $"(类型: {damageInfo.damageType}, 来源: {damageInfo.source?.name ?? "Unknown"})");
            }
            
            // 显示伤害数字
            if (Instance._showDamageNumbers)
            {
                Instance.ShowDamageNumber(damageInfo);
            }
            
            // 触发伤害后处理事件
            OnDamageApplied?.Invoke(damageInfo, target);
            
            // 检查目标是否死亡
            if (!target.IsAlive)
            {
                OnTargetKilled?.Invoke(target, damageInfo);
                
                if (Instance._enableDebugLog)
                {
                    Debug.Log($"[伤害系统] 目标 {target} 已死亡");
                }
            }
        }
        
        /// <summary>
        /// 应用爆炸伤害到范围内的目标
        /// </summary>
        /// <param name="center">爆炸中心点</param>
        /// <param name="radius">爆炸半径</param>
        /// <param name="baseDamage">基础伤害</param>
        /// <param name="damageSource">伤害来源</param>
        /// <param name="layerMask">检测层级</param>
        /// <param name="weapon">武器引用</param>
        public static void ApplyExplosionDamage(Vector3 center, float radius, float baseDamage, 
            GameObject damageSource, LayerMask layerMask, WeaponBase weapon = null)
        {
            if (radius <= 0 || baseDamage <= 0)
                return;
            
            // 获取爆炸范围内的所有碰撞体
            Collider[] hitColliders = Physics.OverlapSphere(center, radius, layerMask);
            
            foreach (Collider hitCollider in hitColliders)
            {
                DWHITE.IDamageable damageable = hitCollider.GetComponent<DWHITE.IDamageable>();
                if (damageable == null || !damageable.IsAlive)
                    continue;
                
                // 计算距离和伤害衰减
                float distance = Vector3.Distance(center, hitCollider.transform.position);
                float damageMultiplier = Mathf.Clamp01(1f - (distance / radius)); // 线性衰减
                float finalDamage = baseDamage * damageMultiplier;
                
                // 创建伤害信息
                DamageInfo explosionDamage = new DamageInfo
                {
                    damage = finalDamage,
                    damageType = DamageType.Explosion,
                    source = damageSource,
                    weapon = weapon,
                    hitPoint = hitCollider.ClosestPoint(center),
                    hitDirection = (hitCollider.transform.position - center).normalized,
                    hitNormal = -(hitCollider.transform.position - center).normalized,
                    distance = distance,
                    isHeadshot = false // 爆炸伤害通常不计算爆头
                };
                
                // 应用伤害
                ApplyDamage(damageable, explosionDamage);
            }
            
            if (Instance._enableDebugLog)
            {
                Debug.Log($"[伤害系统] 爆炸伤害影响了 {hitColliders.Length} 个目标");
            }
        }
        
        /// <summary>
        /// 应用持续伤害（DOT - Damage Over Time）
        /// </summary>
        /// <param name="target">目标</param>
        /// <param name="damagePerSecond">每秒伤害</param>
        /// <param name="duration">持续时间</param>
        /// <param name="damageSource">伤害来源</param>
        /// <param name="weapon">武器引用</param>
        public static void ApplyDamageOverTime(DWHITE.IDamageable target, float damagePerSecond, 
            float duration, GameObject damageSource, WeaponBase weapon = null)
        {
            if (target == null || !target.IsAlive)
                return;
            
            Instance.StartCoroutine(Instance.DamageOverTimeCoroutine(target, damagePerSecond, duration, damageSource, weapon));
        }
        
        #endregion
        
        #region 辅助功能
        
        /// <summary>
        /// 显示伤害数字
        /// </summary>
        private void ShowDamageNumber(DamageInfo damageInfo)
        {
            if (_damageNumberPrefab == null)
                return;
            
            GameObject damageNumber = Instantiate(_damageNumberPrefab, damageInfo.hitPoint, Quaternion.identity, _damageNumberParent);
            
            // 设置伤害数字的值和颜色
            DamageNumberDisplay display = damageNumber.GetComponent<DamageNumberDisplay>();
            if (display != null)
            {
                Color damageColor = GetDamageColor(damageInfo.damageType, damageInfo.isHeadshot);
                display.Initialize(damageInfo.damage, damageColor, damageInfo.isHeadshot);
            }
        }
        
        /// <summary>
        /// 根据伤害类型获取颜色
        /// </summary>
        private Color GetDamageColor(DamageType damageType, bool isHeadshot)
        {
            if (isHeadshot)
                return Color.red; // 爆头伤害用红色
            
            switch (damageType)
            {
                case DamageType.Projectile:
                    return Color.white;
                case DamageType.Explosion:
                    return new Color(1f, 0.5f, 0f); // 橙色
                case DamageType.Hitscan:
                    return Color.yellow;
                case DamageType.Melee:
                    return Color.cyan;
                case DamageType.Environmental:
                    return Color.green;
                default:
                    return Color.gray;
            }
        }
        
        /// <summary>
        /// 持续伤害协程
        /// </summary>
        private System.Collections.IEnumerator DamageOverTimeCoroutine(DWHITE.IDamageable target, 
            float damagePerSecond, float duration, GameObject damageSource, WeaponBase weapon)
        {
            float elapsed = 0f;
            float tickInterval = 0.5f; // 每0.5秒造成一次伤害
            float damagePerTick = damagePerSecond * tickInterval;
            
            while (elapsed < duration && target != null && target.IsAlive)
            {
                DamageInfo dotDamage = new DamageInfo
                {
                    damage = damagePerTick,
                    damageType = DamageType.Environmental, // DOT通常归类为环境伤害
                    source = damageSource,
                    weapon = weapon,
                    hitPoint = ((MonoBehaviour)target).transform.position,
                    hitDirection = Vector3.zero,
                    hitNormal = Vector3.up,
                    isHeadshot = false
                };
                
                ApplyDamage(target, dotDamage);
                
                yield return new WaitForSeconds(tickInterval);
                elapsed += tickInterval;
            }
        }
        
        #endregion
        
        #region 静态辅助方法
        
        /// <summary>
        /// 创建标准伤害信息
        /// </summary>
        public static DamageInfo CreateDamageInfo(float damage, DamageType type, GameObject source, 
            Vector3 hitPoint, Vector3 hitDirection, WeaponBase weapon = null, bool isHeadshot = false)
        {
            return new DamageInfo
            {
                damage = damage,
                damageType = type,
                source = source,
                weapon = weapon,
                hitPoint = hitPoint,
                hitDirection = hitDirection,
                hitNormal = -hitDirection.normalized,
                isHeadshot = isHeadshot,
                distance = 0f,
                projectile = null
            };
        }
        
        #endregion
    }
    
    /// <summary>
    /// 伤害数字显示组件
    /// </summary>
    public class DamageNumberDisplay : MonoBehaviour
    {
        [SerializeField] private TMPro.TextMeshPro _text;
        [SerializeField] private float _lifetime = 2f;
        [SerializeField] private float _moveSpeed = 2f;
        [SerializeField] private AnimationCurve _fadeCurve = new AnimationCurve(new Keyframe(0, 1), new Keyframe(1, 0));
        
        public void Initialize(float damage, Color color, bool isHeadshot)
        {
            if (_text == null)
                _text = GetComponent<TMPro.TextMeshPro>();
            
            if (_text != null)
            {
                _text.text = damage.ToString("F0");
                _text.color = color;
                
                if (isHeadshot)
                {
                    _text.text += "!";
                    _text.fontSize *= 1.2f;
                }
            }
            
            StartCoroutine(AnimateAndDestroy());
        }
        
        private System.Collections.IEnumerator AnimateAndDestroy()
        {
            Vector3 startPos = transform.position;
            float elapsed = 0f;
            
            while (elapsed < _lifetime)
            {
                float t = elapsed / _lifetime;
                
                // 向上移动
                Vector3 newPos = startPos + Vector3.up * (_moveSpeed * elapsed);
                transform.position = newPos;
                
                // 淡出
                if (_text != null)
                {
                    Color color = _text.color;
                    color.a = _fadeCurve.Evaluate(t);
                    _text.color = color;
                }
                
                elapsed += Time.deltaTime;
                yield return null;
            }
            
            Destroy(gameObject);
        }
    }
}
