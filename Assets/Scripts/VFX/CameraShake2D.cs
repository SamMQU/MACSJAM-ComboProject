using UnityEngine;

public class CameraShake2D : MonoBehaviour
{
    private float amp;
    private float timeLeft;
    private Vector3 basePos;

    private void Awake()
    {
        basePos = transform.localPosition;
    }

    public void Shake(float amplitude, float duration)
    {
        amp = Mathf.Max(0f, amplitude);
        timeLeft = Mathf.Max(0f, duration);
    }

    private void LateUpdate()
    {
        if (timeLeft > 0f)
        {
            timeLeft -= Time.deltaTime;
            float nx = (Mathf.PerlinNoise(Time.time * 23.1f, 0f) - 0.5f) * 2f;
            float ny = (Mathf.PerlinNoise(0f, Time.time * 19.7f) - 0.5f) * 2f;
            transform.localPosition = basePos + new Vector3(nx, ny, 0f) * amp;
            if (timeLeft <= 0f) transform.localPosition = basePos;
        }
    }
}
