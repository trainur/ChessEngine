using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class BoardInput : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public event Action<Move> MoveChosen;

    [SerializeField] private GameObject SelectionVisual;
    [SerializeField] private GameObject HighlightVisual;
    [SerializeField] private GameObject PromotionPanel;
    [SerializeField] private Button KnightButton, BishopButton, RookButton, QueenButton;
    [SerializeField] private RectTransform BoardTransform;

    [Header("Cursors")]
    [SerializeField] private Texture2D NormalCursor;
    [SerializeField] private Texture2D SelectableCursor;

    private bool? cursorIsSelectable = null;

    private bool inputEnabled;
    private bool awaitingPromotion;

    private bool hasPieceSelected;

    private bool isDraggingPiece;
    private bool suppressNextClick;

    private BoardState currentState;
    private List<Move> possibleMoves = new();

    private readonly List<GameObject> moveVisualObjects = new();
    private GameObject SelectionHighlightObject;

    private Canvas Canvas;
    private ChessboardImageCreator BoardHandler;

    [SerializeField, Range(0f, 1f)] private float GhostTransparency = 0.6f;

    private Image draggedOriginalImage;
    private Image draggedGhostImage;
    private RectTransform draggedGhostRect;

    void Awake()
    {
        // Board needs an Image or Graphic component to receive raycasts
        Image img = GetComponent<Image>();
        if (img == null)
            Debug.LogError("Board has no Image component - raycasts won't work");
        else
            Debug.Log($"Image raycast target: {img.name}");

        Canvas = BoardTransform.GetComponentInParent<Canvas>();

        BoardHandler = GetComponent<ChessboardImageCreator>();
    }

    private void Start() => SetSelectableCursor(false);

    private void Update()
    {
        if (!inputEnabled || awaitingPromotion)
        {
            SetSelectableCursor(false);
            return;
        }

        if (Mouse.current == null)
        {
            SetSelectableCursor(false);
            return;
        }

        int sq = GetBoardPosition(Mouse.current.position.ReadValue());

        bool hoveringSelectablePiece = sq != -1 && currentState.IsPieceActive(sq);

        bool hoveringLegalMove = sq != -1 && hasPieceSelected && possibleMoves.Any(move => move.To == sq);

        SetSelectableCursor(hoveringSelectablePiece || hoveringLegalMove);
    }

    private void SetSelectableCursor(bool selectable)
    {
        // Avoid unneccesary changing
        if (cursorIsSelectable.HasValue && cursorIsSelectable.Value == selectable) return;

        cursorIsSelectable = selectable;

        Texture2D cursor = selectable ? SelectableCursor : NormalCursor;

        Cursor.SetCursor(cursor, Vector2.zero, CursorMode.Auto);
    }

    public void SetUserInputEnabled(bool enabled, BoardState? state = null)
    {
        inputEnabled = enabled;

        if (state.HasValue) currentState = state.Value;

        if (!enabled)
        {
            Deselect();
            awaitingPromotion = false;
            SetSelectableCursor(false);

            if (PromotionPanel != null) PromotionPanel.SetActive(false);
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!inputEnabled || awaitingPromotion) return;

        int sq = GetBoardPosition(eventData.position);

        if (sq == -1 || !currentState.IsPieceActive(sq)) return;

        Deselect();

        draggedOriginalImage = GetPieceImageOnSquare(sq);

        if (draggedOriginalImage == null) return;

        SetImageAlpha(draggedOriginalImage, GhostTransparency);
        CreateDraggedGhost(draggedOriginalImage, eventData.position);

        SelectPiece(sq);

        isDraggingPiece = true;
        suppressNextClick = true;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDraggingPiece || draggedGhostRect == null) return;

        draggedGhostRect.position = eventData.position;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!isDraggingPiece) return;

        isDraggingPiece = false;

        if (draggedOriginalImage != null) SetImageAlpha(draggedOriginalImage, 1f);

        if (draggedGhostImage != null) Destroy(draggedGhostImage.gameObject);

        draggedOriginalImage = null;
        draggedGhostImage = null;
        draggedGhostRect = null;

        int sq = GetBoardPosition(eventData.position);
        TryChooseMoveTo(sq);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // Ignore release of click if we are dragging
        if (suppressNextClick)
        {
            suppressNextClick = false;
            return;
        }

        if (!inputEnabled || awaitingPromotion) return;

        // Anything other than left click - deselect
        if (eventData.button != PointerEventData.InputButton.Left)
        {
            Deselect();
            return;
        }

        int sq = GetBoardPosition(eventData.position);

        TryChooseMoveTo(sq);
    }

    private void TryChooseMoveTo(int sq)
    {
        // Click is off board
        if (sq == -1)
        {
            Deselect();
            return;
        }

        if (!hasPieceSelected && currentState.IsPieceActive(sq))
        {
            SelectPiece(sq);
        }
        else
        {
            // First check if square selected contains piece of active colour. Enables more fluid feedback of selecting pieces
            if (currentState.IsPieceActive(sq))
            {
                Deselect();
                SelectPiece(sq);
                return;
            }

            // Othwerwise check if player has selected a valid move
            Move? move = null;
            foreach (Move possibleMove in possibleMoves)
            {
                if (possibleMove.To == sq)
                {
                    move = possibleMove;
                    break;
                }
            }

            if (!move.HasValue)
            {
                Deselect();
                return;
            }

            // Promotion handling
            if (move.Value.IsPromotion())
            {
                awaitingPromotion = true;
                ShowPromotionPanel(sq);
                return; // Promotion panel will make move itself
            }

            MoveChosen?.Invoke(move.Value);
            Deselect();
        }
    }

    private int GetBoardPosition(Vector2 screenPos)
    {
        // Use the canvas camera for Screen Space - Camera, null for Overlay
        Camera cam = Canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : Canvas.worldCamera;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            BoardTransform,
            screenPos,
            cam,
            out Vector2 localPoint
        );

        Vector2 boardSize = BoardTransform.rect.size;
        Vector2 bottomLeft = localPoint + boardSize * BoardTransform.pivot;

        int file = Mathf.FloorToInt(bottomLeft.x / (boardSize.x / 8f));
        int rank = Mathf.FloorToInt(bottomLeft.y / (boardSize.y / 8f));

        if (file < 0 || file > 7 || rank < 0 || rank > 7)
            return -1;
 
        return file + (8 * rank);
    }

    private void SelectPiece(int sq)
    {
        hasPieceSelected = true;
        // Lazy workaround, but we can just generate all possible moves and filter for the selected square
        // Negligible performance difference with this use case
        possibleMoves = MoveGenerator.GenerateMoves(ref currentState).Where(move => move.From == sq).ToList();

        HighlightSquare(sq);

        Debug.Log($"Selected piece on square {sq}");

        ShowPossibleMovePositionsVisual(possibleMoves);
    }

    private void Deselect()
    {
        if (!hasPieceSelected) return;
        RemovePossibleMovePositionsVisual();
        RemoveHighlight();
        hasPieceSelected = false;
        possibleMoves = new();
    }

    private void ShowPromotionPanel(int sq)
    {
        // Remove listeners so they dont stack
        KnightButton.onClick.RemoveAllListeners();
        BishopButton.onClick.RemoveAllListeners();
        RookButton.onClick.RemoveAllListeners();
        QueenButton.onClick.RemoveAllListeners();

        PromotionPanel.SetActive(true);

        KnightButton.onClick.AddListener(() => OnPromotionChosen(MoveFlag.PromoteKnight, sq));
        BishopButton.onClick.AddListener(() => OnPromotionChosen(MoveFlag.PromoteBishop, sq));
        RookButton.onClick.AddListener(() => OnPromotionChosen(MoveFlag.PromoteRook, sq));
        QueenButton.onClick.AddListener(() => OnPromotionChosen(MoveFlag.PromoteQueen, sq));
    }

    private void OnPromotionChosen(MoveFlag flag, int sq)
    {
        awaitingPromotion = false;

        KnightButton.onClick.RemoveAllListeners();
        BishopButton.onClick.RemoveAllListeners();
        RookButton.onClick.RemoveAllListeners();
        QueenButton.onClick.RemoveAllListeners();

        PromotionPanel.SetActive(false);

        Move move = possibleMoves.First(move => move.To == sq && move.Flag == flag);

        // No idea, but it cleans up visuals and stuff. bugs without it
        Deselect();

        MoveChosen?.Invoke(move);
    }

    private void ShowPossibleMovePositionsVisual(List<Move> moves)
    {
        foreach (Move move in moves)
        {
            // Skip B,R,Q moves as we only need to represent one promotion move
            if (move.Flag.HasValue && (move.Flag == MoveFlag.PromoteBishop || move.Flag == MoveFlag.PromoteRook || move.Flag == MoveFlag.PromoteQueen)) continue;

            int toFile = move.To % 8;
            int toRank = move.To / 8;

            Transform square = BoardHandler.GetSquare(toFile, toRank);

            if (square == null)
            {
                Debug.LogWarning($"Square not found: {square.name}");
                continue;
            }

            GameObject visual = Instantiate(SelectionVisual, square);

            moveVisualObjects.Add(visual);
        }
    }

    private void RemovePossibleMovePositionsVisual()
    {
        foreach (GameObject visual in moveVisualObjects)
        {
            Destroy(visual);
        }

        moveVisualObjects.Clear();
    }

    private void HighlightSquare(int sq)
    {
        int file = sq % 8;
        int rank = sq / 8;

        Transform square = BoardHandler.GetSquare(file, rank);

        if (square == null)
        {
            Debug.LogWarning($"Square not found: {square.name}");
            return;
        }

        SelectionHighlightObject = Instantiate(HighlightVisual, square);
        SelectionHighlightObject.transform.SetAsFirstSibling();
    }

    private void RemoveHighlight()
    {
        DestroyImmediate(SelectionHighlightObject);
        SelectionHighlightObject = null;
    }


    private Image GetPieceImageOnSquare(int sq)
    {
        int file = sq % 8;
        int rank = sq / 8;

        Transform square = BoardHandler.GetSquare(file, rank);

        if (square == null)
            return null;

        foreach (Transform child in square)
        {
            Image image = child.GetComponent<Image>();

            if (image != null && image.sprite != null)
                return image;
        }

        return null;
    }

    private void CreateDraggedGhost(Image sourceImage, Vector2 screenPosition)
    {
        GameObject ghost = new GameObject("Drag_Ghost");

        ghost.transform.SetParent(Canvas.transform, false);

        draggedGhostImage = ghost.AddComponent<Image>();
        draggedGhostImage.sprite = sourceImage.sprite;
        draggedGhostImage.color = Color.white;
        draggedGhostImage.raycastTarget = false;
        draggedGhostImage.preserveAspect = true;

        draggedGhostRect = ghost.GetComponent<RectTransform>();

        RectTransform sourceRect = sourceImage.GetComponent<RectTransform>();
        draggedGhostRect.sizeDelta = sourceRect.rect.size;
        draggedGhostRect.position = screenPosition;
    }

    private void SetImageAlpha(Image image, float alpha)
    {
        Color c = image.color;
        c.a = alpha;
        image.color = c;
    }

}