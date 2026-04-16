using System;

namespace HoraExtra.Network.Models
{
    /// <summary>
    /// Payload para sincronização de movimento de NPC/Boss.
    /// </summary>
    [Serializable]
    public class NpcMovePayload
    {
        public string id;
        public float[] p; // [x, y, z]
        public float r;   // Rotação Y (Yaw)
    }

    /// <summary>
    /// Payload para registro inicial de NPC na sala.
    /// </summary>
    [Serializable]
    public class NpcRegisterPayload
    {
        public string id;
        public string type;
        public float[] p;
        public float r;
        public string name;
        public bool isMaster;
    }
}
