using System;
using System.Collections.Generic;
using UnityEngine;

// Player-facing audio settings, persisted in PlayerPrefs so they survive
// between sessions and scene loads. Music sources register themselves (see
// MusicSource) and are re-levelled whenever the value changes; one-shot SFX
// read SfxVolume at the moment they play.
//
// Mute sits on top of both: it silences the AudioListener itself, so music,
// SFX and anything else that makes a sound go quiet without the volume
// sliders losing their values.
public static class GameSettings
{
    private const string MusicKey = "settings.musicVolume";
    private const string SfxKey   = "settings.sfxVolume";
    private const string MuteKey  = "settings.muted";

    private static float _music = -1f;
    private static float _sfx = -1f;
    private static int _muted = -1;

    private static readonly List<AudioSource> MusicSources = new List<AudioSource>();

    /// Raised whenever any setting changes, so open UI can refresh.
    public static event Action Changed;

    public static float MusicVolume
    {
        get
        {
            if (_music < 0f) _music = PlayerPrefs.GetFloat(MusicKey, 0.6f);
            return _music;
        }
        set
        {
            _music = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(MusicKey, _music);
            ApplyToMusicSources();
            if (Changed != null) Changed();
        }
    }

    public static float SfxVolume
    {
        get
        {
            if (_sfx < 0f) _sfx = PlayerPrefs.GetFloat(SfxKey, 0.8f);
            return _sfx;
        }
        set
        {
            _sfx = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(SfxKey, _sfx);
            if (Changed != null) Changed();
        }
    }

    public static bool Muted
    {
        get
        {
            if (_muted < 0) _muted = PlayerPrefs.GetInt(MuteKey, 0);
            return _muted == 1;
        }
        set
        {
            _muted = value ? 1 : 0;
            PlayerPrefs.SetInt(MuteKey, _muted);
            PlayerPrefs.Save();
            ApplyMute();
            if (Changed != null) Changed();
        }
    }

    public static void ToggleMute() { Muted = !Muted; }

    // The listener's volume is global and outlives scene loads, so setting it
    // once at startup (and on every toggle) is enough.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ApplyMute()
    {
        AudioListener.volume = Muted ? 0f : 1f;
    }

    public static void Save() { PlayerPrefs.Save(); }

    public static void RegisterMusicSource(AudioSource source)
    {
        if (source == null || MusicSources.Contains(source)) return;
        MusicSources.Add(source);
        source.volume = MusicVolume;
    }

    public static void UnregisterMusicSource(AudioSource source)
    {
        MusicSources.Remove(source);
    }

    private static void ApplyToMusicSources()
    {
        for (int i = MusicSources.Count - 1; i >= 0; i--)
        {
            if (MusicSources[i] == null) { MusicSources.RemoveAt(i); continue; }
            MusicSources[i].volume = _music;
        }
    }
}
