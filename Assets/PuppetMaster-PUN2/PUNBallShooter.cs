using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

namespace RootMotion.Demos
{

    // Just shooting objects from the camera towards the mouse position.
    public class PUNBallShooter : MonoBehaviourPun
    {

        public KeyCode keyCode = KeyCode.Mouse0;
        public GameObject ball;
        public Vector3 spawnOffset = new Vector3(0f, 0.5f, 0f);
        public Vector3 force = new Vector3(0f, 0f, 7f);
        public float mass = 3f;

        void Update()
        {
            if (!photonView.IsMine) return;

            if (Input.GetKeyDown(keyCode))
            {
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                Vector3 position = Camera.main.transform.position + Camera.main.transform.rotation * spawnOffset;
                Vector3 velocity = Quaternion.LookRotation(ray.direction) * force;

                object[] data = new object[1] { velocity };
                PhotonNetwork.Instantiate(ball.name, position, Quaternion.identity, 0, data);
            }

        }
    }
}

