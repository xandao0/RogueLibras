using UnityEngine;

// Marks an AudioSource as background music so it follows the music volume
// setting. Drop this on any looping music source.
[RequireComponent(typeof(AudioSource))]
public class MusicSource : MonoBehaviour
{
    private AudioSource _source;

    private void Awake()
    {
        _source = GetComponent<AudioSource>();
        GameSettings.RegisterMusicSource(_source);
    }

    private void OnDestroy()
    {
        GameSettings.UnregisterMusicSource(_source);
    }
}
