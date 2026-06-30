using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(RectTransform))]
public class BoardRootScaler : MonoBehaviour
{
    [SerializeField] private float rightMargin = 20f;
    [SerializeField] private float verticalMargin = 20f;

    private RectTransform rootRt;
    private RectTransform parentRt;

    private Vector2 lastParentSize;

    private void Awake()
    {
        Cache();
        SetupRoot();
        ResizeRoot();
    }

    private void OnEnable()
    {
        Cache();
        SetupRoot();
        ResizeRoot();
    }

    private void Update()
    {
        if (parentRt == null) return;

        Vector2 parentSize = parentRt.rect.size;

        if (parentSize == lastParentSize) return;

        lastParentSize = parentSize;
        ResizeRoot();
    }

    private void Cache()
    {
        if (rootRt == null)
            rootRt = GetComponent<RectTransform>();

        if (rootRt != null)
            parentRt = rootRt.parent as RectTransform;
    }

    private void SetupRoot()
    {
        if (rootRt == null) return;

        rootRt.anchorMin = new Vector2(1f, 0.5f);
        rootRt.anchorMax = new Vector2(1f, 0.5f);
        rootRt.pivot = new Vector2(1f, 0.5f);

        rootRt.anchoredPosition = new Vector2(-rightMargin, 0f);
    }

    private void ResizeRoot()
    {
        if (rootRt == null || parentRt == null) return;

        float availableWidth = parentRt.rect.width - rightMargin;
        float availableHeight = parentRt.rect.height - verticalMargin * 2f;

        float size = Mathf.Min(availableWidth, availableHeight);

        rootRt.sizeDelta = new Vector2(size, size);
    }
}