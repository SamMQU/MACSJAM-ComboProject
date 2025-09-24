using UnityEngine;

public class VFXTimedDespawn : MonoBehaviour
{
    private float ttl;
    public void Init(float seconds) { ttl = Mathf.Max(0.01f, seconds); }
    private void Update()
    {
        ttl -= Time.deltaTime;
        if (ttl <= 0f) Destroy(gameObject);
    }
}
