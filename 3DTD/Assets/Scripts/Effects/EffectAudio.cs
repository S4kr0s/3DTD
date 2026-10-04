using System.Collections.Generic;
using UnityEngine;

// Sounds of pooled effects (e.g. the flamethrower's hit sparks). Every effect still shows, but at most
// MaxVoicesPerClip copies of the same clip play at once, like the pop sound's throttle.
public static class EffectAudio
{
    public const int MaxVoicesPerClip = 4;

    private static readonly Dictionary<AudioClip, Queue<float>> playing = new Dictionary<AudioClip, Queue<float>>();

    public static void Play(AudioSource source)
    {
        AudioClip clip = source.clip;
        if (clip == null)
            return;

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
            return;

        source.Play();
        endTimes.Enqueue(now + clip.length / Mathf.Max(0.01f, Mathf.Abs(source.pitch)));
    }
}
