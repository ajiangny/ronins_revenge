using System;
using System.Collections.Generic;
using UnityEngine;

// Every clip in Audio/Voice by file name, so a conversation's lines find their voice by id. Built by
// Ninja/Apply Voice Lines; a line without a clip is shown as a subtitle only.
[CreateAssetMenu(menuName = "Ninja/Voice Library")]
public class VoiceLibrary : ScriptableObject
{
    [Serializable]
    public class Clip
    {
        public string id;
        public AudioClip clip;
    }

    public Clip[] clips;

    [NonSerialized] Dictionary<string, AudioClip> byId;
    [NonSerialized] Clip[] indexed;

    public AudioClip Find(string id)
    {
        if (clips == null || id == null) return null;
        if (byId == null || indexed != clips)
        {
            byId = new Dictionary<string, AudioClip>();
            foreach (Clip entry in clips)
            {
                if (entry.id != null && entry.clip != null) byId[entry.id] = entry.clip;
            }
            indexed = clips;
        }
        return byId.TryGetValue(id, out AudioClip found) ? found : null;
    }
}
