using UnityEngine;
using Photon.Pun;

namespace DWHITE
{
    /// <summary>
    /// Base class for Photon network synchronization.
    /// Provides unified serialization methods for derived classes.
    /// </summary>
    public abstract class NetworkSyncBase : MonoBehaviourPun, IPunObservable
    {
        public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
        {
            if (stream.IsWriting)
            {
                WriteData(stream);
            }
            else
            {
                ReadData(stream, info);
            }
        }

        /// <summary>
        /// Write network data to the stream.
        /// </summary>
        protected abstract void WriteData(PhotonStream stream);

        /// <summary>
        /// Read network data from the stream.
        /// </summary>
        protected abstract void ReadData(PhotonStream stream, PhotonMessageInfo info);
    }
}
