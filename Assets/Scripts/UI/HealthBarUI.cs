using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class HeartBarUI : MonoBehaviour
{
    [Header("Binding")]
    [SerializeField] private RectTransform container;   // parent for hearts
    [SerializeField] private Image heartTemplate;        // a disabled Image used as a template (assign any heart sprite)

    [Header("Sprites")]
    [SerializeField] private Sprite heartFull;
    [SerializeField] private Sprite heartHalf;
    [SerializeField] private Sprite heartEmpty;

    [Header("Layout")]
    [SerializeField] private int heartsPerRow = 10;
    [SerializeField] private Vector2 heartSize = new Vector2(32, 32);
    [SerializeField] private Vector2 spacing = new Vector2(4, 4);

    [Header("Rules")]
    [Tooltip("How many HP per heart. 2 = half-heart support like Zelda.")]
    [SerializeField] private int hpPerHeart = 2;

    private readonly List<Image> _hearts = new();
    private int _maxHP;
    private int _currentHP;

    // Public API
    public void SetMaxHP(int maxHP, int currentHP)
    {
        _maxHP = Mathf.Max(0, maxHP);
        _currentHP = Mathf.Clamp(currentHP, 0, _maxHP);
        Rebuild();
        Refresh();
    }

    public void SetHP(int currentHP)
    {
        _currentHP = Mathf.Clamp(currentHP, 0, _maxHP);
        Refresh();
    }

    // ------- Internals -------
    private void Rebuild()
    {
        if (container == null) container = (RectTransform)transform;
        if (heartTemplate == null)
        {
            Debug.LogError("[HeartBarUI] Assign a heartTemplate Image (disabled template child).");
            return;
        }

        // Clear old
        foreach (Transform c in container) if (c != heartTemplate.transform) Destroy(c.gameObject);
        _hearts.Clear();

        int heartCount = Mathf.CeilToInt(_maxHP / (float)hpPerHeart);

        // Optional: auto grid
        var grid = container.GetComponent<GridLayoutGroup>();
        if (grid == null) grid = container.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = heartSize;
        grid.spacing = spacing;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = Mathf.Max(1, heartsPerRow);
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.childAlignment = TextAnchor.UpperCenter;

        // Build hearts
        for (int i = 0; i < heartCount; i++)
        {
            var img = Instantiate(heartTemplate, container);
            img.gameObject.SetActive(true);
            img.raycastTarget = false;
            img.rectTransform.sizeDelta = heartSize;
            img.sprite = heartEmpty;
            _hearts.Add(img);
        }

        heartTemplate.gameObject.SetActive(false); // keep template hidden
    }

    private void Refresh()
    {
        int heartCount = _hearts.Count;
        for (int i = 0; i < heartCount; i++)
        {
            int heartMin = i * hpPerHeart;          // inclusive
            int heartMax = heartMin + hpPerHeart;   // exclusive

            if (_currentHP >= heartMax)            _hearts[i].sprite = heartFull;
            else if (_currentHP <= heartMin)       _hearts[i].sprite = heartEmpty;
            else                                   _hearts[i].sprite = heartHalf;
        }
    }
}
