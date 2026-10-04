using System.Collections.Generic;
using UnityEngine;

// Sounds of pooled effects (e.g. the flamethrower's hit sparks). Every effect still shows, but at most
// MaxVoicesPerClip copies of the same clip play at once, like the pop sound's throttle.
public static class EffectAudio
{
    public const int MaxVoicesPerClip = 4;

    private static readonly Dictionary<AudioClip, Queue<float>> playing = new Dictionary<AudioClip, Queue<float>>();
    private static readonly List<AudioSource> voices = new List<AudioSource>();
    private static int nextVoice;

    // Plays the effect's own source (pooled copies)
    public static void Play(AudioSource source)
    {
        if (TakeVoice(source))
            source.Play();
    }

    // Plays the template's clip and settings at a position (batched effects share one template)
    public static void PlayAt(AudioSource template, Vector3 position)
    {
        if (!TakeVoice(template))
            return;

        AudioSource voice = NextVoice();
        voice.transform.position = position;
        voice.clip = template.clip;
        voice.outputAudioMixerGroup = template.outputAudioMixerGroup;
        voice.volume = template.volume;
        voice.pitch = template.pitch;
        voice.spatialBlend = template.spatialBlend;
        voice.minDistance = template.minDistance;
        voice.maxDistance = template.maxDistance;
        voice.rolloffMode = template.rolloffMode;
        voice.dopplerLevel = template.dopplerLevel;
        voice.priority = template.priority;
        voice.Play();
    }

    private static bool TakeVoice(AudioSource source)
    {
        AudioClip clip = source.clip;
        if (clip == null)
            return false;

        if (!playing.TryGetValue(clip, out Queue<float> endTimes))
        {
            endTimes = new Queue<float>();
            playing.Add(clip, endTimes);
        }

        // Audio runs in real time, not game time
        float now = Time.realtimeSinceStartup;
        while (endTimes.Count > 0 && endTimes.Peek() <= now)
            endTimes.Dequeue();
        if (endTimes.Count >= MaxVoicesPerClip)
            return false;

        endTimes.Enqueue(now + clip.length / Mathf.Max(0.01f, Mathf.Abs(source.pitch)));
        return true;
    }

    private static AudioSource NextVoice()
    {
        voices.RemoveAll(voice => voice == null);
        if (voices.Count < 16)
        {
            GameObject host = new GameObject("Effect Voice");
            AudioSource voice = host.AddComponent<AudioSource>();
            voice.playOnAwake = false;
            voices.Add(voice);
            return voice;
        }
        nextVoice = (nextVoice + 1) % voices.Count;
        return voices[nextVoice];
    }
}
