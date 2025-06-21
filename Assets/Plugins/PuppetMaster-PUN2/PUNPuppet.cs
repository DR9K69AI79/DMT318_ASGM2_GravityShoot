using UnityEngine;
using System.Collections;
using Photon.Pun;

namespace RootMotion.Dynamics
{

    /// <summary>
    /// Synchronizes a puppet over the network. The server instance of the puppet is fully authoritative over switching BehaviourPuppet states. 
    /// Other than that, the clients are free to collide with objects and solve their own physics. Ragoll syncing is done only while the puppet is unpinned.
    /// </summary>
    public class PUNPuppet : MonoBehaviourPun
    {

        [Tooltip("The frequency of syncing Rigidbodies.")]
        public float rigidbodySyncInterval = 0.1f;

        [Tooltip("Normally rigidbodies ase synced via velocity and angular velocity only (for better smoothness). However if a rigidbody drifts more than this value from it's synced position, it's position and rotation will be snapped to the synced state.")]
        public float rigidbodyPositionTolerance = Mathf.Infinity;

        private Rigidbody[] syncRigidbodies = new Rigidbody[0];
        private PuppetMaster puppetMaster;
        private BehaviourPuppet puppet;
        private float nextSyncTime;
        private float syncBlend;
        private Vector3[] velocities = new Vector3[0];
        private Vector3[] angularVelocities = new Vector3[0];
        private Vector3[] positions = new Vector3[0];
        private Vector3[] rotations = new Vector3[0];
        private bool syncFlag;        void Start()
        {
            // Find all required components
            puppetMaster = GetComponentInChildren<PuppetMaster>();
            puppet = GetComponentInChildren<BehaviourPuppet>();
            
            // Check if required components are found
            if (puppetMaster == null)
            {
                Debug.LogError("PUNPuppet: PuppetMaster component not found in children of " + gameObject.name);
                enabled = false;
                return;
            }
            
            if (puppet == null)
            {
                Debug.LogError("PUNPuppet: BehaviourPuppet component not found in children of " + gameObject.name);
                enabled = false;
                return;
            }
            
            syncRigidbodies = puppetMaster.GetComponentsInChildren<Rigidbody>();

            velocities = new Vector3[syncRigidbodies.Length];
            angularVelocities = new Vector3[syncRigidbodies.Length];
            positions = new Vector3[syncRigidbodies.Length];
            rotations = new Vector3[syncRigidbodies.Length];

            // Only the server instance will get event calls
            if (photonView.IsMine)
            {
                puppet.onLoseBalance.unityEvent.AddListener(OnLoseBalance);
                puppet.onGetUpProne.unityEvent.AddListener(OnGetUp);
                puppet.onGetUpSupine.unityEvent.AddListener(OnGetUp);
                name = "PUN Puppet " + "Local";
            }
            else
            {
                // Make sure the remote puppet never loses balance on it's own, only the server has authority here
                puppet.knockOutDistance = Mathf.Infinity;
                puppet.canGetUp = false;
                puppet.canMoveTarget = false;
                name = "PUN Puppet " + "Remote";
            }

            puppetMaster.transform.parent = null;
        }

        // Force instances on the client machines to lose balance
        void OnLoseBalance()
        {
            photonView.RPC("RpcLoseBalance", RpcTarget.Others);
        }

        // Force instances on the client machines to get up
        void OnGetUp()
        {
            photonView.RPC("RpcGetUp", RpcTarget.Others);
        }        // Force instances on the client machines to lose balance
        [PunRPC]
        void RpcLoseBalance()
        {
            if (puppet != null)
            {
                puppet.SetState(BehaviourPuppet.State.Unpinned);
            }
        }

        // Force instances on the client machines to get up
        [PunRPC]
        void RpcGetUp()
        {
            if (puppet != null)
            {
                puppet.SetState(BehaviourPuppet.State.GetUp);
            }
        }

        // We have unparented PuppetMaster so make sure it doesn't remain when this character is destroyed.
        void OnDestroy()
        {
            if (puppetMaster != null) Destroy(puppetMaster.gameObject);
        }        // Returns true if puppet is fully pinned and no rigidbody syncing should be required
        private bool PuppetIsPinned()
        {
            if (puppet == null || puppetMaster == null) return true; // If components are missing, assume pinned to avoid errors
            
            if (puppet.state == BehaviourPuppet.State.Unpinned) return false;
            if (!puppetMaster.isActive) return true;

            foreach (Muscle m in puppetMaster.muscles)
            {
                if (m.state.pinWeightMlp < 1f) return false;
            }
            return true;
        }

        // Rigidbody syncing
        void FixedUpdate()
        {
            if (photonView.IsMine)
            {
                FixedUpdateLocal();
            }
            else
            {
                FixedUpdateRemote();
            }
        }

        // Rigidbody syncing, local
        private void FixedUpdateLocal()
        {
            bool puppetIsPinned = PuppetIsPinned();
            syncBlend = Mathf.MoveTowards(syncBlend, puppetIsPinned ? 0f : 1f, Time.deltaTime * 3f);

            // Time to sync
            if (Time.time >= nextSyncTime && syncBlend > 0)
            {
                bool velocitiesOnly = rigidbodyPositionTolerance == Mathf.Infinity;

                // Read the positions, rotations and velocities of the Rigidbodies
                for (int i = 0; i < syncRigidbodies.Length; i++)
                {
                    if (!velocitiesOnly)
                    {
                        positions[i] = syncRigidbodies[i].position;
                        rotations[i] = syncRigidbodies[i].rotation.eulerAngles;
                    }
                    velocities[i] = syncRigidbodies[i].velocity;
                    angularVelocities[i] = syncRigidbodies[i].angularVelocity;
                }

                // RPC to send the information over to the clients
                if (velocitiesOnly)
                {
                    photonView.RPC("RpcSyncRigidbodyVelocities", RpcTarget.Others, velocities, angularVelocities, syncBlend);
                }
                else
                {
                    photonView.RPC("RpcSyncRigidbodies", RpcTarget.Others, positions, rotations, velocities, angularVelocities, syncBlend);
                }

                // When to sync next?
                nextSyncTime = Time.time + rigidbodySyncInterval;
            }
        }

        // Rigidbody syncing, remote
        private void FixedUpdateRemote()
        {
            if (syncFlag) // Using syncFlag to make sure rigidbodies are moved/rotated in FixedUpdate, not whenever the RPC arrives
            {
                float toleranceSqr = rigidbodyPositionTolerance * rigidbodyPositionTolerance;
                bool velocitiesOnly = rigidbodyPositionTolerance == Mathf.Infinity;

                // Ragdoll rigidbodies
                for (int i = 0; i < syncRigidbodies.Length; i++)
                {
                    if (!velocitiesOnly)
                    {
                        float posOffsetSqr = Vector3.SqrMagnitude(syncRigidbodies[i].position - positions[i]);
                        if (posOffsetSqr > toleranceSqr)
                        {
                            syncRigidbodies[i].MovePosition(Vector3.Lerp(syncRigidbodies[i].position, positions[i], syncBlend));
                            syncRigidbodies[i].MoveRotation(Quaternion.Slerp(syncRigidbodies[i].rotation, Quaternion.Euler(rotations[i]), syncBlend));
                        }
                    }

                    syncRigidbodies[i].velocity = Vector3.Lerp(syncRigidbodies[i].velocity, velocities[i], syncBlend);
                    syncRigidbodies[i].angularVelocity = Vector3.Lerp(syncRigidbodies[i].angularVelocity, angularVelocities[i], syncBlend);
                }

                syncFlag = false;
            }
        }

        // Syncing the positions, rotations and velocities of the Rigidbodies from the server to the clients.
        [PunRPC]
        void RpcSyncRigidbodies(Vector3[] positions, Vector3[] rotations, Vector3[] velocities, Vector3[] angularVelocities, float syncBlend)
        {

            if (this.positions.Length == 0) return; // Not initiated yet

            for (int i = 0; i < positions.Length; i++)
            {
                this.positions[i] = positions[i];
                this.rotations[i] = rotations[i];
                this.velocities[i] = velocities[i];
                this.angularVelocities[i] = angularVelocities[i];
            }

            this.syncBlend = syncBlend;
            syncFlag = true; // Using this to make sure rigidbodies are moved/rotated in FixedUpdate, not whenever this RPC arrives
        }

        // Syncing only the velocities and angularVelocities of the Rigidbodies from the server to the clients.
        [PunRPC]
        void RpcSyncRigidbodyVelocities(Vector3[] velocities, Vector3[] angularVelocities, float syncBlend)
        {
            if (this.positions.Length == 0) return; // Not initiated yet

            for (int i = 0; i < positions.Length; i++)
            {
                this.velocities[i] = velocities[i];
                this.angularVelocities[i] = angularVelocities[i];
            }

            this.syncBlend = syncBlend;
            syncFlag = true; // Using this to make sure rigidbodies are moved/rotated in FixedUpdate, not whenever this RPC arrives
        }
    }
}
