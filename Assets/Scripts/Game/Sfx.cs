using System.Collections.Generic;
using UnityEngine;

namespace LudoGame
{
    /// <summary>Plays the game's short sound effects (CC0 sounds by Kenney, in Resources/Audio).</summary>
    public static class Sfx
    {
        static AudioSource[] sources;
        static int next;
        static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();

        public static void Init(GameObject host)
        {
            sources = new AudioSource[4];
            for (int i = 0; i < sources.Length; i++)
            {
                sources[i] = host.AddComponent<AudioSource>();
                sources[i].playOnAwake = false;
                sources[i].spatialBlend = 0f;
            }
        }

        public static void Play(string name, float volume = 1f, float pitchJitter = 0f)
        {
            if (sources == null || !GameSettings.Sound) return;
            if (!clips.TryGetValue(name, out var clip))
            {
                clip = Resources.Load<AudioClip>("Audio/" + name);
                clips[name] = clip;
            }
            if (clip == null) return;
            var source = sources[next];
            next = (next + 1) % sources.Length;
            source.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
            source.PlayOneShot(clip, volume);
        }
    }
}
