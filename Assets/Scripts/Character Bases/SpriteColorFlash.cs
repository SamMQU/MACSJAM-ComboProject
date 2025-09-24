using UnityEngine;

public class SpriteColorFlash : MonoBehaviour
{
    private SpriteRenderer sr;
    private Color original;
    private Color flash;
    private float timer;
    private float duration;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        if (sr != null) original = sr.color;
    }

    public void Play(Color flashColor, float dur)
    {
        if (sr == null) sr = GetComponent<SpriteRenderer>();
        if (sr == null) return;

        original = sr.color;
        flash = flashColor;
        duration = Mathf.Max(0.01f, dur);
        timer = duration;
        sr.color = flash;
        enabled = true;
    }

    private void Update()
    {
        if (sr == null) { enabled = false; return; }
        timer -= Time.deltaTime;
        float t = Mathf.Clamp01(1f - (timer / duration));
        sr.color = Color.Lerp(flash, original, t);
        if (timer <= 0f) { sr.color = original; enabled = false; }
    }
}
