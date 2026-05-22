using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "VectoArena/Audio Library", fileName = "VectoAudioLibrary")]
public class VectoAudioLibrary : ScriptableObject
{
    public List<VectoAudioEntry> entries = new List<VectoAudioEntry>();

    public AudioClip GetRandomClip(VectoAudioId id)
    {
        VectoAudioEntry entry = entries.Find(item => item.id == id);
        if (entry == null || entry.clips == null || entry.clips.Count == 0)
        {
            return null;
        }

        return entry.clips[UnityEngine.Random.Range(0, entry.clips.Count)];
    }
}

[Serializable]
public class VectoAudioEntry
{
    public VectoAudioId id;
    public List<AudioClip> clips = new List<AudioClip>();
    [Range(0f, 1f)] public float volume = 1f;
    public bool loop;
}
