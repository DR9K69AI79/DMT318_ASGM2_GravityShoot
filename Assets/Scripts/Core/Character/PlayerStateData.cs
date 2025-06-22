using UnityEngine;
using System;

namespace DWHITE
{
    /// <summary>
    /// 玩家状态数据结构 - 包含所有可订阅的状态信息
    /// 设计为不可变数据结构，确保状态一致性
    /// </summary>
    [Serializable]
    public struct PlayerStateData
    {
        #region Movement State
        [Header("运动状态")]
        public bool isGrounded;
        public bool isOnSteep;
        public bool isSprinting;
        public Vector3 velocity;
        public float speed;
        public Vector2 moveInput;
        public float currentSpeedMultiplier;
        #endregion

        #region Jump State
        [Header("跳跃状态")]
        public bool isJumping;
        public int jumpPhase;
        public bool canJump;
        #endregion

        #region Environment State
        [Header("环境状态")]
        public Vector3 gravityDirection;
        public Vector3 upAxis;
        public Vector3 forwardAxis;
        public Vector3 rightAxis;
        public Vector3 contactNormal;
        #endregion

        #region Input State
        [Header("输入状态")]
        public Vector2 lookInput;
        public bool firePressed;
        public bool jumpPressed;
        public bool sprintPressed;
        #endregion

        #region Health State
        [Header("生命值状态")]
        public float currentHealth;
        public float maxHealth;
        public bool isAlive;
        #endregion

        #region Weapon State  
        [Header("武器状态")]
        public bool hasWeapon;
        public int currentWeaponIndex;
        public int currentAmmo;
        public int maxAmmo;
        public bool isReloading;
        public string weaponName;
        #endregion

        /// <summary>
        /// 创建一个空的状态数据
        /// </summary>
        public static PlayerStateData Empty => new PlayerStateData
        {
            upAxis = Vector3.up,
            forwardAxis = Vector3.forward,
            rightAxis = Vector3.right,
            maxHealth = 100f,
            currentHealth = 100f,
            isAlive = true,
            hasWeapon = false,
            currentWeaponIndex = -1,
            weaponName = ""
        };

        /// <summary>
        /// 检查两个状态是否相等（用于优化事件分发）
        /// </summary>
        public bool Equals(PlayerStateData other)
        {
            return isGrounded == other.isGrounded &&
                   isOnSteep == other.isOnSteep &&
                   isSprinting == other.isSprinting &&
                   isJumping == other.isJumping &&
                   jumpPhase == other.jumpPhase &&
                   Vector3.Equals(velocity, other.velocity) &&
                   Vector2.Equals(moveInput, other.moveInput) &&
                   currentHealth == other.currentHealth &&
                   isAlive == other.isAlive &&
                   currentWeaponIndex == other.currentWeaponIndex &&
                   currentAmmo == other.currentAmmo &&
                   isReloading == other.isReloading &&
                   hasWeapon == other.hasWeapon;
        }
    }

    /// <summary>
    /// 状态变化事件参数
    /// </summary>
    public class PlayerStateChangedEventArgs : EventArgs
    {
        public PlayerStateData PreviousState { get; }
        public PlayerStateData CurrentState { get; }
        public float DeltaTime { get; }

        public PlayerStateChangedEventArgs(PlayerStateData previousState, PlayerStateData currentState, float deltaTime)
        {
            PreviousState = previousState;
            CurrentState = currentState;
            DeltaTime = deltaTime;
        }
    }

    /// <summary>
    /// 特定状态变化事件的类型
    /// </summary>
    public enum StateChangeType
    {
        MovementChanged,
        JumpStateChanged,
        GroundStateChanged,
        SprintStateChanged,
        EnvironmentChanged,
        HealthChanged,
        WeaponChanged,
        AmmoChanged
    }
}
