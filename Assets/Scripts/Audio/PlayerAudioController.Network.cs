using UnityEngine;
using Photon.Pun;

namespace DWHITE.Audio
{
    /// <summary>
    /// Player音效控制器网络扩展
    /// 为PlayerAudioController添加网络同步支持
    /// 确保多人游戏中音效的正确播放和同步
    /// </summary>
    public partial class PlayerAudioController
    {
        #region 网络同步设置
        
        [Header("网络同步")]
        [SerializeField] private PhotonView photonView; 
        [SerializeField] private bool _enableNetworkSync = true;
        [SerializeField] private bool _syncFootsteps = false; // 脚步声通常不需要同步
        [SerializeField] private bool _syncJumpLanding = true;
        [SerializeField] private bool _syncEnvironmentAudio = false;
        [SerializeField] private float _networkAudioRange = 20f; // 网络音效听觉范围
        
        #endregion
        
        #region 网络音效播放
        
        /// <summary>
        /// 网络同步播放跳跃音效
        /// </summary>
        public void NetworkPlayJumpSound()
        {
            if (!_enableNetworkSync || !_syncJumpLanding) return;
            
            // 如果是网络玩家，只播放音效不发送RPC
            if (_isNetworkPlayer)
            {
                PlayJumpSound();
                return;
            }
            
            // 本地玩家发送RPC
            if (photonView != null && photonView.IsMine)
            {
                photonView.RPC("OnNetworkJumpSound", RpcTarget.Others);
                PlayJumpSound(); // 本地也播放
            }
        }
        
        /// <summary>
        /// 网络同步播放着地音效
        /// </summary>
        public void NetworkPlayLandingSound(float landingSpeed)
        {
            if (!_enableNetworkSync || !_syncJumpLanding) return;
            
            // 如果是网络玩家，只播放音效不发送RPC
            if (_isNetworkPlayer)
            {
                PlayLandingSound(landingSpeed);
                return;
            }
            
            // 本地玩家发送RPC
            if (photonView != null && photonView.IsMine)
            {
                photonView.RPC("OnNetworkLandingSound", RpcTarget.Others, landingSpeed);
                PlayLandingSound(landingSpeed); // 本地也播放
            }
        }
        
        #endregion
        
        #region 网络RPC处理
        
        /// <summary>
        /// 接收网络跳跃音效RPC
        /// </summary>
        [PunRPC]
        private void OnNetworkJumpSound()
        {
            // 检查距离是否在听觉范围内
            if (IsWithinAudioRange())
            {
                PlayJumpSound();
            }
            
            if (_showDebugInfo)
                Debug.Log("[PlayerAudioController] 接收到网络跳跃音效");
        }
        
        /// <summary>
        /// 接收网络着地音效RPC
        /// </summary>
        [PunRPC]
        private void OnNetworkLandingSound(float landingSpeed)
        {
            // 检查距离是否在听觉范围内
            if (IsWithinAudioRange())
            {
                PlayLandingSound(landingSpeed);
            }
            
            if (_showDebugInfo)
                Debug.Log($"[PlayerAudioController] 接收到网络着地音效，速度: {landingSpeed:F2}");
        }
        
        #endregion
        
        #region 网络距离检测
        
        /// <summary>
        /// 检查是否在音效听觉范围内
        /// </summary>
        private bool IsWithinAudioRange()
        {
            // 获取本地玩家位置
            var localPlayer = FindLocalPlayer();
            if (localPlayer == null) return true; // 如果找不到本地玩家，默认播放
            
            float distance = Vector3.Distance(transform.position, localPlayer.position);
            return distance <= _networkAudioRange;
        }
        
        /// <summary>
        /// 查找本地玩家Transform
        /// </summary>
        private Transform FindLocalPlayer()
        {
            // 这里需要根据您的项目结构来实现
            // 例如通过PlayerManager或其他方式获取本地玩家
            var localPlayerGO = GameObject.FindGameObjectWithTag("Player");
            if (localPlayerGO != null)
            {
                var networkPlayer = localPlayerGO.GetComponent<NetworkPlayerController>();
                if (networkPlayer != null && networkPlayer.photonView.IsMine)
                    return localPlayerGO.transform;
            }
            
            return null;
        }
        
        #endregion
        
        #region 网络优化
        
        /// <summary>
        /// 设置网络音效范围
        /// </summary>
        public void SetNetworkAudioRange(float range)
        {
            _networkAudioRange = Mathf.Max(0f, range);
        }
        
        /// <summary>
        /// 设置网络同步选项
        /// </summary>
        public void SetNetworkSyncOptions(bool enableSync, bool syncJumpLanding, bool syncFootsteps, bool syncEnvironment)
        {
            _enableNetworkSync = enableSync;
            _syncJumpLanding = syncJumpLanding;
            _syncFootsteps = syncFootsteps;
            _syncEnvironmentAudio = syncEnvironment;
        }
        
        #endregion
    }
}
