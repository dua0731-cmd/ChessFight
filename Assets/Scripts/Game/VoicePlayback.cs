using System.Collections.Generic;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.Game
{
    // Plays the voices (Docs/Architecture/UI.md "음성 채팅", R131): one 2D audio source per speaker, fed
    // on the audio thread from a VoiceStream, everyone at the same loudness wherever they stand (the user:
    // no near-only voices), at settings → 소리 → 음성 채팅. Steam-agnostic: the runtime pushes decoded
    // 16-bit PCM in (SteamVoice.Sink).
    [DisallowMultipleComponent]
    public sealed class VoicePlayback : MonoBehaviour
    {
        // A speaker silent this long gives its source back.
        const float IdleSeconds = 20f;

        sealed class Speaker
        {
            public GameObject Host;
            public VoiceStream Stream;
            public VoiceOutput Output;
        }

        readonly Dictionary<ulong, Speaker> speakers = new Dictionary<ulong, Speaker>();
        readonly List<ulong> idle = new List<ulong>();
        AudioClip silence;
        int outputRate;

        public float Volume { get; set; } = 1f;

        int OutputRate
        {
            get
            {
                if (outputRate <= 0) outputRate = AudioSettings.outputSampleRate > 0 ? AudioSettings.outputSampleRate : 48000;
                return outputRate;
            }
        }

        // The rate decoded voice should come at: the output's own, within what Steam decodes (11025-48000),
        // so most PCs need no resampling at all.
        public int DecodeRate => Mathf.Clamp(OutputRate, 11025, 48000);

        public void Push(ulong id, byte[] pcm, int bytes, float gain)
        {
            if (pcm == null || bytes <= 0) return;
            if (!speakers.TryGetValue(id, out var speaker)) speakers[id] = speaker = Create(id);
            speaker.Stream.Write(pcm, bytes, gain, Time.realtimeSinceStartupAsDouble);
        }

        Speaker Create(ulong id)
        {
            var host = new GameObject("Voice " + id);
            host.transform.SetParent(transform, false);
            var source = host.AddComponent<AudioSource>();
            source.clip = Silence();
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.priority = 0;
            source.bypassEffects = source.bypassListenerEffects = source.bypassReverbZones = true;
            var stream = new VoiceStream(DecodeRate);
            var output = host.AddComponent<VoiceOutput>();
            output.Stream = stream;
            output.Step = (double)stream.SampleRate / OutputRate;
            output.Volume = Volume;
            source.Play();
            return new Speaker { Host = host, Stream = stream, Output = output };
        }

        // The source plays this silence; VoiceOutput writes the voice over it.
        AudioClip Silence()
        {
            if (silence == null)
            {
                silence = AudioClip.Create("Voice carrier", 1024, 1, OutputRate, false);
                silence.SetData(new float[1024], 0);
            }
            return silence;
        }

        void Update()
        {
            double now = Time.realtimeSinceStartupAsDouble;
            idle.Clear();
            foreach (var pair in speakers)
            {
                pair.Value.Output.Volume = Volume;
                if (now - pair.Value.Stream.LastWrite > IdleSeconds) idle.Add(pair.Key);
            }
            foreach (ulong id in idle) Forget(id);
        }

        void Forget(ulong id)
        {
            if (!speakers.TryGetValue(id, out var speaker)) return;
            speakers.Remove(id);
            if (speaker.Host != null) Destroy(speaker.Host);
        }

        public void Clear()
        {
            foreach (var speaker in speakers.Values) if (speaker.Host != null) Destroy(speaker.Host);
            speakers.Clear();
        }

        void OnDestroy()
        {
            Clear();
            if (silence != null) Destroy(silence);
        }
    }

    // The audio thread's end of one speaker: the source's silence replaced by the stream.
    [DisallowMultipleComponent]
    public sealed class VoiceOutput : MonoBehaviour
    {
        public VoiceStream Stream;
        public double Step = 1;
        public volatile float Volume = 1f;

        void OnAudioFilterRead(float[] data, int channels)
        {
            var stream = Stream;
            if (stream == null || channels <= 0) return;
            stream.Read(data, data.Length / channels, channels, Step, Volume);
        }
    }
}
