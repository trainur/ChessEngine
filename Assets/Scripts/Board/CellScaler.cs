using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
[RequireComponent(typeof(GridLayoutGroup))]
[DefaultExecutionOrder(100)]
public class BoardGridScaler : MonoBehaviour
{
    private GridLayoutGroup grid;
    private RectTransform parentRt;

    private Vector2 lastParentSize;
    private float lastGoodCellSize;

    private const float MinValidBoardSize = 10f;

    private void OnEnable()
    {
        Cache();

        lastParentSize = Vector2.zero;
    }

    private void LateUpdate()
    {
        Cache();

        if (parentRt == null || grid == null)
            return;

        Vector2 parentSize = parentRt.rect.size;

        if (parentSize == lastParentSize)
            return;

        lastParentSize = parentSize;
        ResizeCells(parentSize);
    }

    private void OnTransformParentChanged()
    {
        parentRt = null;
        Cache();
        lastParentSize = Vector2.zero;
    }

    private void Cache()
    {
        if (grid == null)
            grid = GetComponent<GridLayoutGroup>();

        if (parentRt == null && transform.parent != null)
            parentRt = transform.parent.GetComponent<RectTransform>();
    }

    private void ResizeCells(Vector2 parentSize)
    {
        float boardSize = Mathf.Min(parentSize.x, parentSize.y);

        if (boardSize < MinValidBoardSize)
            return;

        float cellSize = boardSize / 8f;

        if (Mathf.Approximately(cellSize, lastGoodCellSize))
            return;

        lastGoodCellSize = cellSize;
        grid.cellSize = new Vector2(cellSize, cellSize);
    }
}