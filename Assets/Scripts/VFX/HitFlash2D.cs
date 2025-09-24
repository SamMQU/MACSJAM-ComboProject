using UnityEngine;

public class HitFlash2D : MonoBehaviour
{
    private Material runtimeMat;
    private Color original;
    private float timer;
    private float dur;
    private Color flashColor;

    private void EnsureMaterial(Renderer r)
    {
        if (r == null) return;

        if (runtimeMat == null)
        {
            // If there is a shared material, clone it; otherwise build one from a known-good shader
            if (r.sharedMaterial != null)
            {
                runtimeMat = new Material(r.sharedMaterial);
            }
            else
            {
                Shader sh = Shader.Find("Sprites/Default");
                if (sh == null)
                {
                    Debug.LogWarning("[HitFlash2D] Could not find 'Sprites/Default' shader. Please assign a material on the Renderer.");
                    return;
                }
                runtimeMat = new Material(sh);
            }

            r.material = runtimeMat;           // use instance so we don't affect other objects
            original = runtimeMat.color;       // cache the base color
        }
    }

    /// <summary>Flash this renderer to a color, then fade back to its original over 'duration' seconds.</summary>
    public void Play(Color flash, float duration)
    {
        var r = GetComponent<Renderer>();
        if (r == null) return;

        EnsureMaterial(r);
        if (runtimeMat == null) return;

        dur = Mathf.Max(0.01f, duration);
        timer = dur;
        flashColor = flash;

        runtimeMat.color = flashColor;
        enabled = true;
    }

    private void Update()
    {
        if (runtimeMat == null) { enabled = false; return; }

        timer -= Time.deltaTime;
        float t = Mathf.Clamp01(1f - (timer / Mathf.Max(0.0001f, dur)));

        // Lerp from flash back to original
        runtimeMat.color = Color.Lerp(flashColor, original, t);

        if (timer <= 0f)
        {
            runtimeMat.color = original;
            enabled = false;
        }
    }
}
