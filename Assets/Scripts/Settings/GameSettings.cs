using System;
using System.Collections.Generic;
using UnityEngine;

// Player-facing audio settings, persisted in PlayerPrefs so they survive
// between sessions and scene loads. Music sources register themselves (see
// MusicSource) and are re-levelled whenever the value changes; one-shot SFX
// read SfxVolume at the moment they play.
public static class GameSettings
{
    private const string MusicKey = "settings.musicVolume";
    private const string SfxKey   = "settings.sfxVolume";

    private static float _music = -1f;
    private static float _sfx = -1f;

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
