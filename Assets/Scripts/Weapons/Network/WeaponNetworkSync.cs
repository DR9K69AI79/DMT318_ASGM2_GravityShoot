using UnityEngine;
using Photon.Pun;
using System.Collections.Generic;

namespace DWHITE.Weapons.Network
{
    /// <summary>
    /// 武器网络同步管理器
    /// 处理武器切换、射击和特效的网络同步
    /// </summary>
    public class WeaponNetworkSync : MonoBehaviourPun, IPunObservable
    {
        [Header("同步设置")]
        [SerializeField] private bool _syncWeaponIndex = true;
        [SerializeField] private bool _syncAmmoCount = true;
        [SerializeField] private bool _syncReloadState = true;
        [SerializeField] private float _sendRate = 10f; // 每秒发送次数
        
        [Header("射击同步")]
        [SerializeField] private bool _useRPCForFiring = true;
        [SerializeField] private bool _syncMuzzleFlash = true;
        [SerializeField] private bool _syncSoundEffects = true;
        
        [Header("调试")]
        [SerializeField] private bool _showDebugInfo = false;
        
        private PlayerWeaponController _weaponController;
        private int _networkWeaponIndex = -1;
        private int _networkAmmoCount = 0;
        private bool _networkIsReloading = false;
        private float _lastSendTime = 0f;
        
        // 本地状态缓存
        private int _lastWeaponIndex = -1;
        private int _lastAmmoCount = -1;
        private bool _lastReloadState = false;
        
        #region Unity 生命周期
        
        private void Awake()
        {
            _weaponController = GetComponent<PlayerWeaponController>();
            if (_weaponController == null)
            {
                Debug.LogError("[武器网络同步] 未找到 PlayerWeaponController 组件！");
                enabled = false;
                return;
            }
        }
        
        private void Start()
        {
            // 只有网络对象才需要同步
            if (photonView == null)
            {
                Debug.LogWarning("[武器网络同步] 没有 PhotonView 组件，禁用网络同步");
                enabled = false;
                return;
            }
            
            // 订阅武器事件
            if (photonView.IsMine)
            {
                SubscribeToWeaponEvents();
            }
        }
        
        private void Update()
        {
            // 非本地玩家应用网络状态
            if (!photonView.IsMine && _weaponController != null)
            {
                ApplyNetworkState();
            }
        }
        
        private void OnDestroy()
        {
            if (photonView != null && photonView.IsMine)
            {
                UnsubscribeFromWeaponEvents();
            }
        }
        
        #endregion
        
        #region 事件订阅
        
        private void SubscribeToWeaponEvents()
        {
            // 订阅武器系统事件
            PlayerWeaponController.OnWeaponSwitched += OnWeaponSwitched;
            WeaponBase.OnWeaponFired += OnWeaponFired;
            WeaponBase.OnReloadStarted += OnReloadStarted;
            WeaponBase.OnReloadCompleted += OnReloadCompleted;
        }
        
        private void UnsubscribeFromWeaponEvents()
        {
            // 取消订阅武器系统事件
            PlayerWeaponController.OnWeaponSwitched -= OnWeaponSwitched;
            WeaponBase.OnWeaponFired -= OnWeaponFired;
            WeaponBase.OnReloadStarted -= OnReloadStarted;
            WeaponBase.OnReloadCompleted -= OnReloadCompleted;
        }
        
        #endregion
        
        #region 事件处理
        
        private void OnWeaponSwitched(PlayerWeaponController controller, WeaponBase weapon)
        {
            if (controller != _weaponController) return;
            
            // 发送武器切换RPC
            photonView.RPC("OnWeaponSwitchedRPC", RpcTarget.Others, controller.CurrentWeaponIndex);
            
            if (_showDebugInfo)
                Debug.Log($"[武器网络同步] 发送武器切换: {controller.CurrentWeaponIndex}");
        }
        
        private void OnWeaponFired(WeaponBase weapon)
        {
            if (!_useRPCForFiring || weapon.transform.root != transform) return;
            
            // 发送射击RPC
            Vector3 muzzlePos = weapon.MuzzlePoint ? weapon.MuzzlePoint.position : weapon.transform.position;
            Vector3 aimDir = _weaponController.CurrentAimDirection;
            
            photonView.RPC("OnWeaponFiredRPC", RpcTarget.Others, 
                muzzlePos.x, muzzlePos.y, muzzlePos.z,
                aimDir.x, aimDir.y, aimDir.z,
                weapon.WeaponData.WeaponName);
            
            if (_showDebugInfo)
                Debug.Log($"[武器网络同步] 发送射击事件: {weapon.WeaponData.WeaponName}");
        }
        
        private void OnReloadStarted(WeaponBase weapon)
        {
            if (weapon.transform.root != transform) return;
            
            photonView.RPC("OnReloadStartedRPC", RpcTarget.Others);
            
            if (_showDebugInfo)
                Debug.Log("[武器网络同步] 发送开始装弹事件");
        }
        
        private void OnReloadCompleted(WeaponBase weapon)
        {
            if (weapon.transform.root != transform) return;
            
            photonView.RPC("OnReloadCompletedRPC", RpcTarget.Others);
            
            if (_showDebugInfo)
                Debug.Log("[武器网络同步] 发送装弹完成事件");
        }
        
        #endregion
        
        #region RPC 方法
        
        [PunRPC]
        private void OnWeaponSwitchedRPC(int weaponIndex)
        {
            _networkWeaponIndex = weaponIndex;
            
            if (_showDebugInfo)
                Debug.Log($"[武器网络同步] 接收到武器切换: {weaponIndex}");
        }
        
        [PunRPC]
        private void OnWeaponFiredRPC(float posX, float posY, float posZ, 
                                    float dirX, float dirY, float dirZ, 
                                    string weaponName)
        {
            Vector3 muzzlePosition = new Vector3(posX, posY, posZ);
            Vector3 aimDirection = new Vector3(dirX, dirY, dirZ);
            
            // 播放射击特效
            PlayNetworkFireEffects(muzzlePosition, aimDirection, weaponName);
            
            if (_showDebugInfo)
                Debug.Log($"[武器网络同步] 接收到射击事件: {weaponName} at {muzzlePosition}");
        }
        
        [PunRPC]
        private void OnReloadStartedRPC()
        {
            _networkIsReloading = true;
            
            if (_showDebugInfo)
                Debug.Log("[武器网络同步] 接收到开始装弹事件");
        }
        
        [PunRPC]
        private void OnReloadCompletedRPC()
        {
            _networkIsReloading = false;
            
            if (_showDebugInfo)
                Debug.Log("[武器网络同步] 接收到装弹完成事件");
        }
        
        #endregion
        
        #region 网络状态应用
        
        private void ApplyNetworkState()
        {
            // 应用武器切换
            if (_syncWeaponIndex && _networkWeaponIndex != _lastWeaponIndex)
            {
                if (_networkWeaponIndex >= 0 && _networkWeaponIndex < _weaponController.WeaponCount)
                {
                    // 这里需要调用武器控制器的切换方法
                    // _weaponController.SwitchToWeapon(_networkWeaponIndex);
                }
                _lastWeaponIndex = _networkWeaponIndex;
            }
            
            // 应用弹药同步
            if (_syncAmmoCount && _networkAmmoCount != _lastAmmoCount)
            {
                // 同步弹药数量
                _lastAmmoCount = _networkAmmoCount;
            }
            
            // 应用装弹状态
            if (_syncReloadState && _networkIsReloading != _lastReloadState)
            {
                // 同步装弹状态
                _lastReloadState = _networkIsReloading;
            }
        }
        
        private void PlayNetworkFireEffects(Vector3 muzzlePosition, Vector3 aimDirection, string weaponName)
        {
            // 播放枪口闪光
            if (_syncMuzzleFlash)
            {
                // 创建枪口闪光效果
                // 这里需要根据武器类型播放对应的特效
            }
            
            // 播放射击音效
            if (_syncSoundEffects)
            {
                // 播放射击音效
                // AudioSource.PlayClipAtPoint(fireSound, muzzlePosition);
            }
        }
        
        #endregion
        
        #region IPunObservable 实现
        
        public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
        {
            if (stream.IsWriting)
            {
                // 发送数据
                WriteNetworkData(stream);
            }
            else
            {
                // 接收数据
                ReadNetworkData(stream);
            }
        }
        
        private void WriteNetworkData(PhotonStream stream)
        {
            // 检查是否需要发送数据
            if (Time.time - _lastSendTime < 1f / _sendRate)
                return;
                
            _lastSendTime = Time.time;
            
            if (_weaponController == null) return;
            
            // 发送武器索引
            if (_syncWeaponIndex)
            {
                stream.SendNext(_weaponController.CurrentWeaponIndex);
            }
            
            // 发送弹药数量
            if (_syncAmmoCount && _weaponController.HasWeapon)
            {
                stream.SendNext(_weaponController.CurrentWeapon.CurrentAmmo);
            }
            else
            {
                stream.SendNext(0);
            }
            
            // 发送装弹状态
            if (_syncReloadState && _weaponController.HasWeapon)
            {
                stream.SendNext(_weaponController.CurrentWeapon.IsReloading);
            }
            else
            {
                stream.SendNext(false);
            }
        }
        
        private void ReadNetworkData(PhotonStream stream)
        {
            // 接收武器索引
            if (_syncWeaponIndex)
            {
                _networkWeaponIndex = (int)stream.ReceiveNext();
            }
            
            // 接收弹药数量
            if (_syncAmmoCount)
            {
                _networkAmmoCount = (int)stream.ReceiveNext();
            }
            
            // 接收装弹状态
            if (_syncReloadState)
            {
                _networkIsReloading = (bool)stream.ReceiveNext();
            }
        }
        
        #endregion
        
        #region 公共接口
        
        /// <summary>
        /// 启用/禁用特定同步功能
        /// </summary>
        public void SetSyncOptions(bool weaponIndex = true, bool ammoCount = true, bool reloadState = true)
        {
            _syncWeaponIndex = weaponIndex;
            _syncAmmoCount = ammoCount;
            _syncReloadState = reloadState;
        }
        
        /// <summary>
        /// 设置网络发送频率
        /// </summary>
        public void SetSendRate(float rate)
        {
            _sendRate = Mathf.Max(1f, rate);
        }
        
        /// <summary>
        /// 强制同步当前状态
        /// </summary>
        public void ForceSyncState()
        {
            if (photonView != null && photonView.IsMine && _weaponController != null)
            {
                photonView.RPC("OnWeaponSwitchedRPC", RpcTarget.Others, _weaponController.CurrentWeaponIndex);
            }
        }
        
        #endregion
    }
}
