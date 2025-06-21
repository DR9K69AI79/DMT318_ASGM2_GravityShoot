using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

namespace RootMotion.Demos
{
    public class PUNBall : MonoBehaviourPun, IPunInstantiateMagicCallback
    {
        public void OnPhotonInstantiate(PhotonMessageInfo info)
        {
            object[] data = photonView.InstantiationData;
            Vector3 velocity = (Vector3)data[0];

            var r = GetComponent<Rigidbody>();
            r.AddForce(velocity - r.velocity, ForceMode.VelocityChange);
        }
    }
}
