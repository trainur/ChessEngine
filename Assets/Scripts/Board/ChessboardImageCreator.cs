using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

[RequireComponent(typeof(GridLayoutGroup))]
public class ChessboardImageCreator : MonoBehaviour
{
    [Header("Square Colours")]
    [SerializeField] private Color lightSquareColour = new Color(0.93f, 0.82f, 0.65f);
    [SerializeField] private Color darkSquareColour = new Color(0.55f, 0.36f, 0.22f);

    private const int BoardSize = 8;

    [ContextMenu("Generate Chessboard")]
    private void GenerateChessboard()
    {
        ClearChessboard();

        ConfigureGrid();

        for (int rank = 7; rank >= 0; rank--)
            for (int file = 0; file < BoardSize; file++)
                CreateSquare(file, rank);

#if UNITY_EDITOR
        EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
    }

    private void ConfigureGrid()
    {
        GridLayoutGroup grid = GetComponent<GridLayoutGroup>();

        // Derive cell size from the RectTransform so it always fits the board
        RectTransform rt = GetComponent<RectTransform>();
        float boardWidth = rt.rect.width > 0 ? rt.rect.width : 800f;
        float boardHeight = rt.rect.height > 0 ? rt.rect.height : 800f;
        float cell = Mathf.Min(boardWidth, boardHeight) / BoardSize;

        grid.cellSize = new Vector2(cell, cell);
        grid.spacing = Vector2.zero;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = BoardSize;
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.padding = new RectOffset(0, 0, 0, 0);
    }

    private void CreateSquare(int file, int rank)
    {
        string squareName = $"Square_{(char)('a' + file)}{rank + 1}";

        GameObject square = new GameObject(squareName, typeof(RectTransform), typeof(Image));

#if UNITY_EDITOR
        Undo.RegisterCreatedObjectUndo(square, "Generate Chessboard");
#endif

        square.transform.SetParent(transform, false);

        bool isLight = (file + rank) % 2 != 0;

        Image image = square.GetComponent<Image>();
        image.color = isLight ? lightSquareColour : darkSquareColour;
        image.raycastTarget = false;
    }

    private void ClearChessboard()
    {
        while (transform.childCount > 0)
        {
            GameObject child = transform.GetChild(0).gameObject;
#if UNITY_EDITOR
            if (!Application.isPlaying) { Undo.DestroyObjectImmediate(child); continue; }
#endif
            Destroy(child);
        }

#if UNITY_EDITOR
        EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
    }

    // Utility used by BoardManager to locate a square's Transform
    public Transform GetSquare(int file, int rank)
    {
        string name = $"Square_{(char)('a' + file)}{rank + 1}";
        return transform.Find(name);
    }
}