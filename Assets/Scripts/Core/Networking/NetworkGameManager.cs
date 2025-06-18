using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using Hashtable = ExitGames.Client.Photon.Hashtable;

namespace DWHITE
{
    /// <summary>
    /// 网络游戏管理器 - 处理游戏状态同步和匹配逻辑
    /// 专为重力射击游戏设计，包含物理权威和状态同步
    /// </summary>
    public class NetworkGameManager : MonoBehaviourPun, IPunObservable
    {
        #region Game State Enums
        
        public enum GamePhase
        {
            Lobby,          // 大厅阶段 - 玩家自由探索
            Matching,       // 匹配阶段 - 等待玩家
            PreGame,        // 游戏准备阶段
            InGame,         // 游戏进行中
            PostGame,       // 游戏结束
            Paused          // 游戏暂停
        }
        
        public enum MatchType
        {
            FreePlay,       // 自由模式
            TeamDeathmatch, // 团队死斗
            Elimination,    // 淘汰赛
            KingOfTheHill,  // 山丘之王
            CaptureFlag     // 夺旗模式
        }
        
        #endregion
        
        #region Configuration & Events
        
        [Header("游戏配置")]
        [SerializeField] private GamePhase _initialPhase = GamePhase.Lobby;
        [SerializeField] private MatchType _matchType = MatchType.FreePlay;
        [SerializeField] private int _maxPlayersPerMatch = 8;
        [SerializeField] private float _matchDuration = 300f; // 5分钟
        
        [Header("物理权威设置")]
        [SerializeField] private bool _useMasterClientAuthority = true;
        [SerializeField] private float _physicsUpdateRate = 60f;
        [SerializeField] private float _syncThreshold = 0.1f;
        
        [Header("游戏平衡")]
        [SerializeField] private float _respawnDelay = 3f;
        [SerializeField] private int _maxLives = 3;
        [SerializeField] private float _warmupDuration = 10f;
        
        [Header("调试")]
        [SerializeField] private bool _showGameDebugInfo = false;
        
        // Events
        public System.Action<GamePhase> OnGamePhaseChanged;
        public System.Action<Player> OnPlayerJoinedMatch;
        public System.Action<Player> OnPlayerLeftMatch;
        public System.Action<float> OnMatchTimeUpdated;
        public System.Action<Player, int> OnPlayerScoreChanged;
        public System.Action OnMatchStarted;
        public System.Action OnMatchEnded;
        
        #endregion
        
        #region Private Fields
        
        private GamePhase _currentPhase;
        private float _matchStartTime;
        private float _remainingTime;
        private Dictionary<int, PlayerMatchData> _playerData = new Dictionary<int, PlayerMatchData>();
        private bool _isMatchActive = false;
        private Coroutine _matchTimerCoroutine;
        
        // 网络同步状态
        private struct NetworkGameState
        {
            public GamePhase phase;
            public float matchTime;
            public int playersReady;
            public bool isMatchActive;
        }
        
        private NetworkGameState _networkState;
        
        #endregion
        
        #region Properties
        
        public GamePhase CurrentPhase => _currentPhase;
        public MatchType CurrentMatchType => _matchType;
        public float RemainingTime => _remainingTime;
        public bool IsMatchActive => _isMatchActive;
        public bool IsMasterClientAuthority => _useMasterClientAuthority;
        public int PlayersInMatch => _playerData.Count;
        public int PlayersReady => GetReadyPlayerCount();
        
        #endregion
        
        #region Unity Lifecycle
        
        private void Awake()
        {
            // 确保只有一个GameManager实例
            if (FindObjectsOfType<NetworkGameManager>().Length > 1)
            {
                Destroy(gameObject);
                return;
            }
            
            _currentPhase = _initialPhase;
            _networkState.phase = _currentPhase;
        }
        
        private void Start()
        {
            // 只有主客户端初始化游戏状态
            if (PhotonNetwork.IsMasterClient)
            {
                InitializeGameState();
            }
            
            // 注册网络管理器事件
            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.OnPlayerEnteredRoomEvent += OnPlayerEnteredRoom;
                NetworkManager.Instance.OnPlayerLeftRoomEvent += OnPlayerLeftRoom;
                NetworkManager.Instance.OnMasterClientSwitchedEvent += OnMasterClientSwitched;
            }
        }
        
        private void OnDestroy()
        {
            if (_matchTimerCoroutine != null)
            {
                StopCoroutine(_matchTimerCoroutine);
            }
            
            // 取消注册事件
            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.OnPlayerEnteredRoomEvent -= OnPlayerEnteredRoom;
                NetworkManager.Instance.OnPlayerLeftRoomEvent -= OnPlayerLeftRoom;
                NetworkManager.Instance.OnMasterClientSwitchedEvent -= OnMasterClientSwitched;
            }
        }
        
        #endregion
        
        #region Game State Management
        
        /// <summary>
        /// 初始化游戏状态（仅主客户端）
        /// </summary>
        private void InitializeGameState()
        {
            if (!PhotonNetwork.IsMasterClient) return;
            
            LogGameDebug("初始化游戏状态");
            
            _remainingTime = _matchDuration;
            _isMatchActive = false;
            
            // 同步到所有客户端
            photonView.RPC("SyncGameState", RpcTarget.Others, (int)_currentPhase, _remainingTime, false);
        }
        
        /// <summary>
        /// 切换游戏阶段
        /// </summary>
        /// <param name="newPhase">新的游戏阶段</param>
        public void ChangeGamePhase(GamePhase newPhase)
        {
            if (!PhotonNetwork.IsMasterClient)
            {
                LogGameDebug("只有主客户端可以切换游戏阶段");
                return;
            }
            
            if (_currentPhase == newPhase) return;
            
            GamePhase oldPhase = _currentPhase;
            _currentPhase = newPhase;
            
            LogGameDebug($"游戏阶段从 {oldPhase} 切换到 {newPhase}");
            
            // 同步到所有客户端
            photonView.RPC("OnGamePhaseChangedRPC", RpcTarget.All, (int)newPhase);
            
            // 根据新阶段执行相应逻辑
            HandlePhaseTransition(newPhase);
        }
        
        /// <summary>
        /// 处理阶段转换逻辑
        /// </summary>
        private void HandlePhaseTransition(GamePhase newPhase)
        {
            switch (newPhase)
            {
                case GamePhase.Lobby:
                    ResetMatchData();
                    break;
                    
                case GamePhase.Matching:
                    PrepareForMatching();
                    break;
                    
                case GamePhase.PreGame:
                    StartWarmup();
                    break;
                    
                case GamePhase.InGame:
                    StartMatch();
                    break;
                    
                case GamePhase.PostGame:
                    EndMatch();
                    break;
                    
                case GamePhase.Paused:
                    PauseMatch();
                    break;
            }
        }
        
        #endregion
        
        #region Match Management
        
        /// <summary>
        /// 准备匹配阶段
        /// </summary>
        private void PrepareForMatching()
        {
            LogGameDebug("进入匹配准备阶段");
            
            // 重置玩家数据
            _playerData.Clear();
            
            // 初始化所有在房间中的玩家
            foreach (var player in PhotonNetwork.PlayerList)
            {
                InitializePlayerData(player);
            }
        }
        
        /// <summary>
        /// 开始热身阶段
        /// </summary>
        private void StartWarmup()
        {
            if (!PhotonNetwork.IsMasterClient) return;
            
            LogGameDebug($"开始热身阶段 ({_warmupDuration}秒)");
            
            photonView.RPC("OnWarmupStartedRPC", RpcTarget.All, _warmupDuration);
            
            // 热身倒计时
            StartCoroutine(WarmupCountdown());
        }
        
        /// <summary>
        /// 热身倒计时协程
        /// </summary>
        private IEnumerator WarmupCountdown()
        {
            float countdown = _warmupDuration;
            
            while (countdown > 0)
            {
                photonView.RPC("UpdateWarmupTimer", RpcTarget.All, countdown);
                yield return new WaitForSeconds(1f);
                countdown -= 1f;
            }
            
            // 自动开始比赛
            ChangeGamePhase(GamePhase.InGame);
        }
        
        /// <summary>
        /// 开始比赛
        /// </summary>
        private void StartMatch()
        {
            if (!PhotonNetwork.IsMasterClient) return;
            
            LogGameDebug("比赛开始！");
            
            _isMatchActive = true;
            _matchStartTime = (float)PhotonNetwork.Time;
            _remainingTime = _matchDuration;
            
            photonView.RPC("OnMatchStartedRPC", RpcTarget.All, _matchStartTime);
            
            // 开始比赛计时器
            if (_matchTimerCoroutine != null)
            {
                StopCoroutine(_matchTimerCoroutine);
            }
            _matchTimerCoroutine = StartCoroutine(MatchTimer());
            
            OnMatchStarted?.Invoke();
        }
        
        /// <summary>
        /// 比赛计时器协程
        /// </summary>
        private IEnumerator MatchTimer()
        {
            while (_remainingTime > 0 && _isMatchActive)
            {
                yield return new WaitForSeconds(1f);
                _remainingTime -= 1f;
                OnMatchTimeUpdated?.Invoke(_remainingTime);
            }
            
            if (_remainingTime <= 0)
            {
                ChangeGamePhase(GamePhase.PostGame);
            }
        }
        
        /// <summary>
        /// 结束比赛
        /// </summary>
        private void EndMatch()
        {
            if (!PhotonNetwork.IsMasterClient) return;
            
            LogGameDebug("比赛结束");
            
            _isMatchActive = false;
            
            if (_matchTimerCoroutine != null)
            {
                StopCoroutine(_matchTimerCoroutine);
                _matchTimerCoroutine = null;
            }
            
            photonView.RPC("OnMatchEndedRPC", RpcTarget.All);
            OnMatchEnded?.Invoke();
            
            // 延迟返回大厅
            StartCoroutine(ReturnToLobbyAfterDelay(10f));
        }
        
        /// <summary>
        /// 暂停比赛
        /// </summary>
        private void PauseMatch()
        {
            if (!PhotonNetwork.IsMasterClient) return;
            
            LogGameDebug("比赛暂停");
            
            if (_matchTimerCoroutine != null)
            {
                StopCoroutine(_matchTimerCoroutine);
            }
            
            photonView.RPC("OnMatchPausedRPC", RpcTarget.All);
        }
        
        /// <summary>
        /// 延迟返回大厅
        /// </summary>
        private IEnumerator ReturnToLobbyAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            ChangeGamePhase(GamePhase.Lobby);
        }
        
        #endregion
        
        #region Player Management
        
        /// <summary>
        /// 初始化玩家数据
        /// </summary>
        private void InitializePlayerData(Player player)
        {
            if (!_playerData.ContainsKey(player.ActorNumber))
            {
                _playerData[player.ActorNumber] = new PlayerMatchData
                {
                    player = player,
                    score = 0,
                    kills = 0,
                    deaths = 0,
                    isReady = false,
                    lives = _maxLives,
                    lastRespawnTime = 0f
                };
                
                LogGameDebug($"初始化玩家数据: {player.NickName}");
            }
        }
        
        /// <summary>
        /// 设置玩家准备状态
        /// </summary>
        /// <param name="isReady">是否准备</param>
        public void SetPlayerReady(bool isReady)
        {
            if (_playerData.ContainsKey(PhotonNetwork.LocalPlayer.ActorNumber))
            {
                photonView.RPC("UpdatePlayerReady", RpcTarget.All, PhotonNetwork.LocalPlayer.ActorNumber, isReady);
            }
        }
        
        /// <summary>
        /// 获取准备的玩家数量
        /// </summary>
        private int GetReadyPlayerCount()
        {
            int count = 0;
            foreach (var data in _playerData.Values)
            {
                if (data.isReady) count++;
            }
            return count;
        }
        
        /// <summary>
        /// 检查是否所有玩家都已准备
        /// </summary>
        private bool AllPlayersReady()
        {
            if (_playerData.Count < 2) return false; // 至少需要2个玩家
            
            foreach (var data in _playerData.Values)
            {
                if (!data.isReady) return false;
            }
            return true;
        }
        
        /// <summary>
        /// 重置比赛数据
        /// </summary>
        private void ResetMatchData()
        {
            _playerData.Clear();
            _isMatchActive = false;
            _remainingTime = _matchDuration;
            
            if (_matchTimerCoroutine != null)
            {
                StopCoroutine(_matchTimerCoroutine);
                _matchTimerCoroutine = null;
            }
        }
        
        #endregion
        
        #region RPC Methods
        
        [PunRPC]
        private void SyncGameState(int phase, float remainingTime, bool isActive)
        {
            _currentPhase = (GamePhase)phase;
            _remainingTime = remainingTime;
            _isMatchActive = isActive;
            
            LogGameDebug($"同步游戏状态: {_currentPhase}, 剩余时间: {_remainingTime}");
        }
        
        [PunRPC]
        private void OnGamePhaseChangedRPC(int newPhase)
        {
            GamePhase phase = (GamePhase)newPhase;
            _currentPhase = phase;
            
            LogGameDebug($"游戏阶段变更: {phase}");
            OnGamePhaseChanged?.Invoke(phase);
        }
        
        [PunRPC]
        private void OnWarmupStartedRPC(float duration)
        {
            LogGameDebug($"热身开始，持续时间: {duration}秒");
        }
        
        [PunRPC]
        private void UpdateWarmupTimer(float countdown)
        {
            LogGameDebug($"热身倒计时: {countdown:F1}秒");
        }
        
        [PunRPC]
        private void OnMatchStartedRPC(float startTime)
        {
            _isMatchActive = true;
            _matchStartTime = startTime;
            
            LogGameDebug("比赛开始RPC接收");
            OnMatchStarted?.Invoke();
        }
        
        [PunRPC]
        private void OnMatchEndedRPC()
        {
            _isMatchActive = false;
            
            LogGameDebug("比赛结束RPC接收");
            OnMatchEnded?.Invoke();
        }
        
        [PunRPC]
        private void OnMatchPausedRPC()
        {
            LogGameDebug("比赛暂停RPC接收");
        }
        
        [PunRPC]
        private void UpdatePlayerReady(int actorNumber, bool isReady)
        {
            if (_playerData.ContainsKey(actorNumber))
            {
                //_playerData[actorNumber].isReady = isReady;
                
                Player player = PhotonNetwork.CurrentRoom.GetPlayer(actorNumber);
                LogGameDebug($"玩家 {player?.NickName ?? "未知"} 准备状态: {isReady}");
                
                // 检查是否所有玩家都已准备
                if (PhotonNetwork.IsMasterClient && AllPlayersReady() && _currentPhase == GamePhase.Matching)
                {
                    ChangeGamePhase(GamePhase.PreGame);
                }
            }
        }
        
        [PunRPC]
        private void UpdatePlayerScore(int actorNumber, int newScore)
        {
            if (_playerData.ContainsKey(actorNumber))
            {
                //_playerData[actorNumber].score = newScore;
                
                Player player = PhotonNetwork.CurrentRoom.GetPlayer(actorNumber);
                OnPlayerScoreChanged?.Invoke(player, newScore);
                
                LogGameDebug($"玩家 {player?.NickName ?? "未知"} 得分更新: {newScore}");
            }
        }
        
        #endregion
        
        #region Network Callbacks
        
        private void OnPlayerEnteredRoom(Player newPlayer)
        {
            LogGameDebug($"玩家 {newPlayer.NickName} 加入游戏");
            
            if (PhotonNetwork.IsMasterClient)
            {
                InitializePlayerData(newPlayer);
                
                // 同步当前游戏状态给新玩家
                photonView.RPC("SyncGameState", newPlayer, (int)_currentPhase, _remainingTime, _isMatchActive);
            }
            
            OnPlayerJoinedMatch?.Invoke(newPlayer);
        }
        
        private void OnPlayerLeftRoom(Player otherPlayer)
        {
            LogGameDebug($"玩家 {otherPlayer.NickName} 离开游戏");
            
            if (_playerData.ContainsKey(otherPlayer.ActorNumber))
            {
                _playerData.Remove(otherPlayer.ActorNumber);
            }
            
            OnPlayerLeftMatch?.Invoke(otherPlayer);
            
            // 如果在比赛中且玩家不足，考虑结束比赛
            if (PhotonNetwork.IsMasterClient && _isMatchActive && PlayersInMatch < 2)
            {
                LogGameDebug("玩家不足，结束比赛");
                ChangeGamePhase(GamePhase.PostGame);
            }
        }
        
        private void OnMasterClientSwitched(Player newMasterClient)
        {
            LogGameDebug($"主客户端切换到: {newMasterClient.NickName}");
            
            // 新的主客户端需要接管游戏状态管理
            if (PhotonNetwork.IsMasterClient)
            {
                LogGameDebug("成为新的主客户端，接管游戏管理");
                
                // 重新启动计时器（如果比赛进行中）
                if (_isMatchActive && _matchTimerCoroutine == null)
                {
                    _matchTimerCoroutine = StartCoroutine(MatchTimer());
                }
            }
        }
        
        #endregion
        
        #region IPunObservable Implementation
        
        public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
        {
            if (stream.IsWriting)
            {
                // 发送游戏状态数据
                stream.SendNext((int)_currentPhase);
                stream.SendNext(_remainingTime);
                stream.SendNext(_isMatchActive);
                stream.SendNext(PlayersReady);
            }
            else
            {
                // 接收游戏状态数据
                _networkState.phase = (GamePhase)stream.ReceiveNext();
                _networkState.matchTime = (float)stream.ReceiveNext();
                _networkState.isMatchActive = (bool)stream.ReceiveNext();
                _networkState.playersReady = (int)stream.ReceiveNext();
                
                // 同步本地状态（非主客户端）
                if (!PhotonNetwork.IsMasterClient)
                {
                    _currentPhase = _networkState.phase;
                    _remainingTime = _networkState.matchTime;
                    _isMatchActive = _networkState.isMatchActive;
                }
            }
        }
        
        #endregion
        
        #region Public API
        
        /// <summary>
        /// 手动开始比赛（主客户端调用）
        /// </summary>
        public void StartMatchManually()
        {
            if (PhotonNetwork.IsMasterClient && _currentPhase == GamePhase.Matching)
            {
                ChangeGamePhase(GamePhase.PreGame);
            }
        }
        
        /// <summary>
        /// 结束当前比赛（主客户端调用）
        /// </summary>
        public void EndMatchManually()
        {
            if (PhotonNetwork.IsMasterClient && _isMatchActive)
            {
                ChangeGamePhase(GamePhase.PostGame);
            }
        }
        
        /// <summary>
        /// 暂停/恢复比赛
        /// </summary>
        public void TogglePause()
        {
            if (!PhotonNetwork.IsMasterClient) return;
            
            if (_currentPhase == GamePhase.InGame)
            {
                ChangeGamePhase(GamePhase.Paused);
            }
            else if (_currentPhase == GamePhase.Paused)
            {
                ChangeGamePhase(GamePhase.InGame);
            }
        }
        
        /// <summary>
        /// 更新玩家得分
        /// </summary>
        /// <param name="player">玩家</param>
        /// <param name="scoreChange">得分变化</param>
        public void UpdatePlayerScore(Player player, int scoreChange)
        {
            if (_playerData.ContainsKey(player.ActorNumber))
            {
                int newScore = _playerData[player.ActorNumber].score + scoreChange;
                photonView.RPC("UpdatePlayerScore", RpcTarget.All, player.ActorNumber, newScore);
            }
        }
        
        /// <summary>
        /// 获取玩家数据
        /// </summary>
        /// <param name="player">玩家</param>
        /// <returns>玩家匹配数据</returns>
        public PlayerMatchData? GetPlayerData(Player player)
        {
            return _playerData.TryGetValue(player.ActorNumber, out PlayerMatchData data) ? data : null;
        }
        
        /// <summary>
        /// 获取排行榜数据
        /// </summary>
        /// <returns>按得分排序的玩家列表</returns>
        public List<PlayerMatchData> GetLeaderboard()
        {
            var leaderboard = new List<PlayerMatchData>(_playerData.Values);
            leaderboard.Sort((a, b) => b.score.CompareTo(a.score));
            return leaderboard;
        }
        
        /// <summary>
        /// 检查是否可以开始比赛
        /// </summary>
        /// <returns>是否可以开始</returns>
        public bool CanStartMatch()
        {
            return PhotonNetwork.IsMasterClient && 
                   _currentPhase == GamePhase.Matching && 
                   PlayersInMatch >= 2 && 
                   AllPlayersReady();
        }
        
        #endregion
        
        #region Utility Methods
        
        private void LogGameDebug(string message)
        {
            if (_showGameDebugInfo)
            {
                Debug.Log($"[NetworkGameManager] {message}");
            }
        }
        
        #endregion
    }
    
    /// <summary>
    /// 玩家匹配数据结构
    /// </summary>
    [System.Serializable]
    public struct PlayerMatchData
    {
        public Player player;
        public int score;
        public int kills;
        public int deaths;
        public bool isReady;
        public int lives;
        public float lastRespawnTime;
        
        public float KDRatio => deaths > 0 ? (float)kills / deaths : kills;
    }
}
