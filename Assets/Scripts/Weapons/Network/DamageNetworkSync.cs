using UnityEngine;
using Photon.Pun;
using System;

namespace DWHITE.Weapons.Network
{
    /// <summary>
    /// 伤害系统网络同步组件
    /// 处理伤害的网络同步和验证
    /// </summary>
    public class DamageNetworkSync : MonoBehaviourPun
    {
        [Header("同步设置")]
        [SerializeField] private bool _enableDamageSync = true;
        [SerializeField] private bool _serverSideValidation = true;
        [SerializeField] private float _damageValidationTolerance = 0.1f;
        
        [Header("反作弊设置")]
        [SerializeField] private bool _enableAntiCheat = true;
        [SerializeField] private float _maxDamagePerSecond = 1000f;
        [SerializeField] private float _validationTimeWindow = 5f;
        
        [Header("调试")]
        [SerializeField] private bool _showDebugInfo = false;
        
        // 组件引用
        private IDamageable _damageable;
        
        // 伤害统计
        private float _totalDamageThisSecond = 0f;
        private float _lastDamageResetTime = 0f;
        private float _lastValidationTime = 0f;
        
        // 伤害缓存
        private struct PendingDamage
        {
            public float amount;
            public Vector3 hitPoint;
            public Vector3 hitDirection;
            public string damageSource;
            public float timestamp;
        }
        
        #region Unity 生命周期
        
        private void Awake()
        {
            _damageable = GetComponent<IDamageable>();
            if (_damageable == null)
            {
                LogDamage("警告: 未找到IDamageable组件");
            }
            
            _lastDamageResetTime = Time.time;
            _lastValidationTime = Time.time;
        }
        
        private void Update()
        {
            UpdateDamageStatistics();
            ValidateDamageRate();
        }
        
        #endregion
        
        #region 伤害同步
        
        /// <summary>
        /// 应用伤害（网络同步）
        /// </summary>
        public void ApplyDamage(float damage, Vector3 hitPoint, Vector3 hitDirection, string damageSource, int shooterActorId)
        {
            if (!_enableDamageSync) return;
            
            // 防作弊检查
            if (_enableAntiCheat && !ValidateDamageRequest(damage, damageSource))
            {
                LogDamage($"伤害请求被拒绝 - 伤害: {damage}, 来源: {damageSource}");
                return;
            }
            
            if (photonView.IsMine)
            {
                // 本地玩家：直接应用伤害并广播
                ApplyLocalDamage(damage, hitPoint, hitDirection, damageSource);
                BroadcastDamage(damage, hitPoint, hitDirection, damageSource, shooterActorId);
            }
            else
            {
                // 远程玩家：只广播，等待所有者确认
                RequestDamage(damage, hitPoint, hitDirection, damageSource, shooterActorId);
            }
        }
        
        /// <summary>
        /// 应用本地伤害
        /// </summary>
        private void ApplyLocalDamage(float damage, Vector3 hitPoint, Vector3 hitDirection, string damageSource)
        {
            if (_damageable != null)
            {
                _damageable.TakeDamage(damage, hitPoint, hitDirection);
                LogDamage($"应用本地伤害: {damage} from {damageSource}");
            }
            
            // 更新统计
            _totalDamageThisSecond += damage;
        }
        
        /// <summary>
        /// 广播伤害事件
        /// </summary>
        private void BroadcastDamage(float damage, Vector3 hitPoint, Vector3 hitDirection, string damageSource, int shooterActorId)
        {
            photonView.RPC("OnDamageAppliedRPC", RpcTarget.Others,
                damage, 
                hitPoint.x, hitPoint.y, hitPoint.z,
                hitDirection.x, hitDirection.y, hitDirection.z,
                damageSource, shooterActorId, PhotonNetwork.Time);
        }
        
        /// <summary>
        /// 请求伤害
        /// </summary>
        private void RequestDamage(float damage, Vector3 hitPoint, Vector3 hitDirection, string damageSource, int shooterActorId)
        {
            photonView.RPC("OnDamageRequestRPC", photonView.Owner,
                damage, 
                hitPoint.x, hitPoint.y, hitPoint.z,
                hitDirection.x, hitDirection.y, hitDirection.z,
                damageSource, shooterActorId, PhotonNetwork.Time);
        }
        
        #endregion
        
        #region RPC方法
        
        [PunRPC]
        private void OnDamageAppliedRPC(float damage, 
                                      float hitX, float hitY, float hitZ,
                                      float dirX, float dirY, float dirZ,
                                      string damageSource, int shooterActorId, double timestamp)
        {
            Vector3 hitPoint = new Vector3(hitX, hitY, hitZ);
            Vector3 hitDirection = new Vector3(dirX, dirY, dirZ);
            
            // 验证时间戳
            double currentTime = PhotonNetwork.Time;
            double timeDifference = Math.Abs(currentTime - timestamp);
            
            if (timeDifference > _validationTimeWindow)
            {
                LogDamage($"伤害事件超时 - 时间差: {timeDifference:F2}s");
                return;
            }
            
            // 播放伤害特效（不实际扣血）
            PlayDamageEffects(hitPoint, hitDirection, damage);
            
            LogDamage($"接收到伤害事件: {damage} from {damageSource}");
        }
        
        [PunRPC]
        private void OnDamageRequestRPC(float damage, 
                                      float hitX, float hitY, float hitZ,
                                      float dirX, float dirY, float dirZ,
                                      string damageSource, int shooterActorId, double timestamp)
        {
            if (!photonView.IsMine) return;
            
            Vector3 hitPoint = new Vector3(hitX, hitY, hitZ);
            Vector3 hitDirection = new Vector3(dirX, dirY, dirZ);
            
            // 服务端验证伤害
            if (_serverSideValidation && !ValidateServerDamage(damage, damageSource, shooterActorId, timestamp))
            {
                LogDamage($"服务端验证失败 - 伤害: {damage}");
                return;
            }
            
            // 应用伤害并广播确认
            ApplyLocalDamage(damage, hitPoint, hitDirection, damageSource);
            photonView.RPC("OnDamageConfirmedRPC", RpcTarget.Others,
                damage, shooterActorId, PhotonNetwork.Time);
        }
        
        [PunRPC]
        private void OnDamageConfirmedRPC(float damage, int shooterActorId, double timestamp)
        {
            // 伤害确认回调
            LogDamage($"伤害确认: {damage} from actor {shooterActorId}");
        }
        
        #endregion
        
        #region 验证系统
        
        /// <summary>
        /// 验证伤害请求
        /// </summary>
        private bool ValidateDamageRequest(float damage, string damageSource)
        {
            // 检查伤害值是否合理
            if (damage <= 0 || damage > 10000f)
            {
                LogDamage($"伤害值异常: {damage}");
                return false;
            }
            
            // 检查伤害速率
            float currentTime = Time.time;
            if (currentTime - _lastDamageResetTime >= 1f)
            {
                _totalDamageThisSecond = 0f;
                _lastDamageResetTime = currentTime;
            }
            
            if (_totalDamageThisSecond + damage > _maxDamagePerSecond)
            {
                LogDamage($"伤害速率过高: {_totalDamageThisSecond + damage}/s");
                return false;
            }
            
            return true;
        }
        
        /// <summary>
        /// 服务端伤害验证
        /// </summary>
        private bool ValidateServerDamage(float damage, string damageSource, int shooterActorId, double timestamp)
        {
            // 检查射击者是否存在
            if (PhotonNetwork.CurrentRoom.GetPlayer(shooterActorId) == null)
            {
                LogDamage($"射击者不存在: {shooterActorId}");
                return false;
            }
            
            // 检查时间戳
            double currentTime = PhotonNetwork.Time;
            double timeDifference = Math.Abs(currentTime - timestamp);
            
            if (timeDifference > _validationTimeWindow)
            {
                LogDamage($"伤害请求超时: {timeDifference:F2}s");
                return false;
            }
            
            // 其他验证逻辑...
            
            return true;
        }
        
        /// <summary>
        /// 更新伤害统计
        /// </summary>
        private void UpdateDamageStatistics()
        {
            float currentTime = Time.time;
            
            // 重置每秒伤害统计
            if (currentTime - _lastDamageResetTime >= 1f)
            {
                _totalDamageThisSecond = 0f;
                _lastDamageResetTime = currentTime;
            }
        }
        
        /// <summary>
        /// 验证伤害速率
        /// </summary>
        private void ValidateDamageRate()
        {
            if (_totalDamageThisSecond > _maxDamagePerSecond * 1.5f)
            {
                LogDamage($"警告：异常高伤害速率检测: {_totalDamageThisSecond}/s");
                // 可以在这里采取反作弊措施
            }
        }
        
        #endregion
        
        #region 特效播放
        
        /// <summary>
        /// 播放伤害特效
        /// </summary>
        private void PlayDamageEffects(Vector3 hitPoint, Vector3 hitDirection, float damage)
        {
            // 播放命中特效
            // TODO: 根据伤害类型播放不同特效
            
            // 播放血量变化动画
            // TODO: 更新UI显示
            
            // 播放伤害数字
            ShowDamageNumber(damage, hitPoint);
        }
        
        /// <summary>
        /// 显示伤害数字
        /// </summary>
        private void ShowDamageNumber(float damage, Vector3 position)
        {
            // TODO: 创建浮动伤害数字
            LogDamage($"显示伤害数字: {damage} at {position}");
        }
        
        #endregion
        
        #region 公共接口
        
        /// <summary>
        /// 设置同步选项
        /// </summary>
        public void SetSyncOptions(bool enableSync = true, bool serverValidation = true)
        {
            _enableDamageSync = enableSync;
            _serverSideValidation = serverValidation;
        }
        
        /// <summary>
        /// 设置反作弊参数
        /// </summary>
        public void SetAntiCheatOptions(bool enabled, float maxDpsPerSecond = 1000f, float validationWindow = 5f)
        {
            _enableAntiCheat = enabled;
            _maxDamagePerSecond = maxDpsPerSecond;
            _validationTimeWindow = validationWindow;
        }
        
        /// <summary>
        /// 获取当前伤害统计
        /// </summary>
        public float GetCurrentDPS()
        {
            return _totalDamageThisSecond;
        }
        
        /// <summary>
        /// 重置伤害统计
        /// </summary>
        public void ResetDamageStatistics()
        {
            _totalDamageThisSecond = 0f;
            _lastDamageResetTime = Time.time;
            _lastValidationTime = Time.time;
        }
        
        #endregion
        
        #region 调试
        
        private void LogDamage(string message)
        {
            if (_showDebugInfo)
            {
                Debug.Log($"[伤害网络同步] {message}");
            }
        }
        
        #endregion
    }
    
    /// <summary>
    /// 可受伤害接口
    /// </summary>
    public interface IDamageable
    {
        void TakeDamage(float damage, Vector3 hitPoint, Vector3 hitDirection);
        float GetCurrentHealth();
        float GetMaxHealth();
        bool IsAlive();
    }
}
