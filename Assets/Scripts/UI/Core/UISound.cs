using UnityEngine;

/// <summary>
/// Звуки интерфейса, сгенерированные кодом (без аудиофайлов):
/// наведение, клик, открытие окна, монета, ошибка, предмет.
/// Громкость задаётся в настройках (AudioListener.volume).
/// </summary>
public static class UISound
{
    private static AudioSource _source;
    private static AudioClip _hover, _click, _open, _coin, _error, _item;

    private static AudioSource Source
    {
        get
        {
            if (_source == null)
            {
                var go = new GameObject("UISound");
                Object.DontDestroyOnLoad(go);
                _source = go.AddComponent<AudioSource>();
                _source.playOnAwake = false;
                _source.spatialBlend = 0f;
            }
            return _source;
        }
    }

    public static void PlayHover() => Play(ref _hover, "ui_hover", 1200f, 1300f, 0.03f, 0.08f);
    public static void PlayClick() => Play(ref _click, "ui_click", 900f, 600f, 0.06f, 0.25f);
    public static void PlayOpen() => Play(ref _open, "ui_open", 500f, 900f, 0.12f, 0.18f);
    public static void PlayCoin() => Play(ref _coin, "ui_coin", 1300f, 2000f, 0.12f, 0.2f);
    public static void PlayError() => Play(ref _error, "ui_error", 220f, 160f, 0.18f, 0.25f);
    public static void PlayItem() => Play(ref _item, "ui_item", 600f, 1400f, 0.25f, 0.22f);

    private static void Play(ref AudioClip clip, string name, float fromHz, float toHz, float length, float volume)
    {
        if (clip == null)
            clip = CreateTone(name, fromHz, toHz, length);
        Source.PlayOneShot(clip, volume);
    }

    private static AudioClip CreateTone(string name, float fromHz, float toHz, float length)
    {
        const int sampleRate = 44100;
        int samples = Mathf.Max(1, Mathf.RoundToInt(sampleRate * length));
        var data = new float[samples];
        float phase = 0f;

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / samples;
            float frequency = Mathf.Lerp(fromHz, toHz, t);
            phase += 2f * Mathf.PI * frequency / sampleRate;
            float attack = Mathf.Clamp01(t * 40f);
            float release = (1f - t) * (1f - t);
            data[i] = Mathf.Sin(phase) * attack * release;
        }

        AudioClip clip = AudioClip.Create(name, samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
