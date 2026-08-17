using System.Collections.Generic;
using UnityEngine;

public class Sfx : MonoBehaviour
{
    const float Master = 1.0f;

    const float MusicVol      = 0.22f;
    const float AmbienceVol   = 0.12f;
    const float TickVol       = 0.12f;
    const float HoverVol      = 0.14f;
    const float DragVol       = 0.16f;
    const float CardDrawVol   = 0.26f;
    const float CardPlayVol   = 0.30f;
    const float ButtonVol     = 0.28f;
    const float PlaceVol      = 0.34f;
    const float RemoveVol     = 0.30f;
    const float CoinVol       = 0.38f;
    const float BuyVol        = 0.40f;
    const float AnimalVol     = 0.34f;
    const float ScoreVol      = 0.36f;
    const float ErrorVol      = 0.42f;
    const float GachaVol      = 0.48f;
    const float RewardVol     = 0.45f;
    const float StingVol      = 0.65f;

    static Sfx instance;

    readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();

    AudioSource source;
    AudioSource music;
    AudioSource ambience;
    AudioSource dragging;

    [RuntimeInitializeOnLoadMethod]
    static void Create()
    {
        if (instance != null)
            return;

        var holder = new GameObject("Sfx");
        DontDestroyOnLoad(holder);
        holder.AddComponent<Sfx>();
    }

    void Awake()
    {
        instance = this;

        foreach (var clip in Resources.LoadAll<AudioClip>("Audio"))
            clips[clip.name] = clip;

        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;

        music    = Loop("music", MusicVol);
        ambience = Loop("ambience", AmbienceVol);
        dragging = Loop("pathdrag", DragVol);

        if (music.clip != null)
            music.Play();

        if (ambience.clip != null)
            ambience.Play();
    }

    AudioSource Loop(string name, float volume)
    {
        var loop = gameObject.AddComponent<AudioSource>();
        loop.clip = Find(name);
        loop.loop = true;
        loop.volume = volume * Master;
        loop.playOnAwake = false;
        return loop;
    }

    static AudioClip Find(string name)
    {
        AudioClip clip;
        return instance != null && instance.clips.TryGetValue(name, out clip) ? clip : null;
    }

    static void Play(string name, float volume, float pitch)
    {
        var clip = Find(name);
        if (clip == null)
            return;

        instance.source.pitch = pitch;
        instance.source.PlayOneShot(clip, volume * Master);
    }

    public static void PathDrag(bool held)
    {
        if (instance == null || instance.dragging.clip == null)
            return;

        if (held && !instance.dragging.isPlaying)
            instance.dragging.Play();
        else if (!held)
            instance.dragging.Stop();
    }

    public static void Music(bool on)
    {
        if (instance == null || instance.music.clip == null)
            return;

        if (on && !instance.music.isPlaying)
            instance.music.Play();
        else if (!on)
            instance.music.Stop();
    }

    public static void CardDraw()
    {
        Play("carddraw", CardDrawVol, Random.Range(0.96f, 1.04f));
    }

    public static void CardPlay()
    {
        Play("cardplay", CardPlayVol, 1f);
    }

    public static void CardHover()
    {
        Play("cardhover", HoverVol, Random.Range(0.97f, 1.03f));
    }

    public static void CardSelect()
    {
        Play("cardselect", HoverVol, 1f);
    }

    public static void PlaceEnclosure()
    {
        Play("place", PlaceVol, Random.Range(0.97f, 1.03f));
    }

    public static void PlacePath()
    {
        Play("pathplace", PlaceVol, Random.Range(0.94f, 1.06f));
    }

    public static void Remove()
    {
        Play("remove", RemoveVol, 1f);
    }

    public static void MoveEnclosure()
    {
        Play("move", PlaceVol, 1f);
    }

    public static void Invalid()
    {
        Play("error", ErrorVol, 1f);
    }

    public static void Animal(string species)
    {
        if (instance == null)
            return;

        var clip = string.IsNullOrEmpty(species)
            ? null
            : Find("animal_" + species.ToLowerInvariant().Replace(" ", ""));

        if (clip == null)
            clip = Find("animal");

        if (clip == null)
            return;

        instance.source.pitch = Random.Range(0.94f, 1.06f);
        instance.source.PlayOneShot(clip, AnimalVol * Master);
    }

    public static void Coin()
    {
        Play("coin", CoinVol, 1f);
    }

    public static void Buy()
    {
        Play("buy", BuyVol, 1f);
    }

    public static void Gacha()
    {
        Play("gacha", GachaVol, 1f);
    }

    public static void ButtonPress()
    {
        Play("buttonpress", ButtonVol, 1f);
    }

    public static void Tick()
    {
        Play("tick", TickVol, 1f);
    }

    public static void ScorePop(int step)
    {
        Play("scorepop", ScoreVol, 1f + Mathf.Min(step, 12) * 0.05f);
    }

    public static void GoodReview()
    {
        Play("goodreview", ScoreVol, 1f);
    }

    public static void DayAdvance()
    {
        Play("dayadvance", ButtonVol, 1f);
    }

    public static void RewardAppear()
    {
        Play("rewardappear", RewardVol, 1f);
    }

    public static void RewardPick()
    {
        Play("rewardpick", RewardVol, 1f);
    }

    public static void Win()
    {
        Play("win", StingVol, 1f);
    }

    public static void Lose()
    {
        Play("lose", StingVol, 1f);
    }
}
