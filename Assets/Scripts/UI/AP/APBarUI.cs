using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class APBarUI : MonoBehaviour
{
    [Header("Binding")]
    [SerializeField] private RectTransform container; // parent for AP pips
    [SerializeField] private Image pipTemplate;       // disabled Image used as a template

    [Header("Sprites")]
    [SerializeField] private Sprite pipFull;
    [SerializeField] private Sprite pipEmpty;

    [Header("Layout")]
    [SerializeField, Range(1, 20)] private int maxPips = 5;
    [SerializeField] private Vector2 pipSize = new Vector2(28, 28);
    [SerializeField] private Vector2 spacing = new Vector2(4, 4);
    [SerializeField] private bool horizontal = true; // row vs column

    private readonly List<Image> _pips = new();
    private int _maxAP;
    private int _currentAP;

    // Public API
    public void SetMaxAP(int maxAP, int currentAP)
    {
        _maxAP = Mathf.Clamp(maxAP, 0, maxPips);
        _currentAP = Mathf.Clamp(currentAP, 0, _maxAP);
        Rebuild();
        Refresh();
    }

    public void SetAP(int currentAP)
    {
        _currentAP = Mathf.Clamp(currentAP, 0, _maxAP);
        Refresh();
    }

    // ---------- internals ----------
    private void Rebuild()
    {
        if (container == null) container = (RectTransform)transform;
        if (pipTemplate == null)
        {
            Debug.LogError("[APBarUI] Assign a disabled pipTemplate Image.");
            return;
        }

        foreach (Transform c in container)
            if (c != pipTemplate.transform) Destroy(c.gameObject);
        _pips.Clear();

        var grid = container.GetComponent<GridLayoutGroup>();
        if (grid == null) grid = container.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = pipSize;
        grid.spacing = spacing;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = horizontal ? _maxAP : 1;
        grid.startAxis = horizontal ? GridLayoutGroup.Axis.Horizontal : GridLayoutGroup.Axis.Vertical;
        grid.childAlignment = TextAnchor.MiddleCenter;

        for (int i = 0; i < _maxAP; i++)
        {
            var img = Instantiate(pipTemplate, container);
            img.gameObject.SetActive(true);
            img.raycastTarget = false;
            img.rectTransform.sizeDelta = pipSize;
            img.sprite = pipEmpty;
            _pips.Add(img);
        }

        pipTemplate.gameObject.SetActive(false);
    }

    private void Refresh()
    {
        for (int i = 0; i < _pips.Count; i++)
            _pips[i].sprite = (i < _currentAP) ? pipFull : pipEmpty;
    }
}
