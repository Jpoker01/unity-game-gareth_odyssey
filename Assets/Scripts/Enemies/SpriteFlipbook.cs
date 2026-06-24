using UnityEngine;

/// <summary>
/// Minimal frame-sequencing helper shared by the Level 2 enemy scripts.
/// Mirrors PlayerSpriteAnimation's frame-array + FPS + timer approach, but is
/// driven explicitly via Tick() from each enemy's state machine so that a
/// state can ask "did my clip finish?" (e.g. end of a draw, strike or charge
/// telegraph) instead of fighting a second independent Update loop.
/// </summary>
public class SpriteFlipbook
{
    readonly SpriteRenderer sr;

    Sprite[] frames;
    float    fps;
    bool     loop;
    float    timer;
    int      index;

    public SpriteFlipbook(SpriteRenderer renderer) { sr = renderer; }

    public bool IsFinished { get; private set; }
    public int  FrameIndex => index;

    /// <summary>Start playing a clip. No-op if this exact clip is already looping/playing.</summary>
    public void Play(Sprite[] clip, float framesPerSecond, bool doLoop, bool restart = false)
    {
        if (clip == null || clip.Length == 0) return;
        if (!restart && frames == clip && loop == doLoop && !IsFinished) return;

        frames     = clip;
        fps        = framesPerSecond;
        loop       = doLoop;
        timer      = 0f;
        index      = 0;
        IsFinished = false;
        sr.sprite  = frames[0];
    }

    /// <summary>Advance playback by dt and push the resulting frame onto the SpriteRenderer.</summary>
    public void Tick(float dt)
    {
        if (frames == null || frames.Length == 0 || fps <= 0f || IsFinished) return;

        timer += dt;
        float spf = 1f / fps;
        while (timer >= spf)
        {
            timer -= spf;
            index++;
            if (index >= frames.Length)
            {
                if (loop) index = 0;
                else      { index = frames.Length - 1; IsFinished = true; break; }
            }
        }
        sr.sprite = frames[index];
    }
}
