using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class BoardManager : MonoBehaviour
{
    [SerializeField] private GameObject[] PiecePrefabs;

    [Header("Starting Position")]
    [SerializeField] private string OverrideFen;
    [SerializeField] private bool UseRandomPositions;
    [SerializeField] private string[] RandomFens;

    [Header("Game Agents")]
    [SerializeField] private ChessAgent WhiteAgent;
    [SerializeField] private ChessAgent BlackAgent;

    [Header("Sounds")]
    [SerializeField] private AudioClip MoveClip;
    [SerializeField] private AudioClip CheckClip;
    [SerializeField] private AudioClip EndClip;

    [Header("Highlights")]
    [SerializeField] private GameObject HighlightFromPrefab;
    [SerializeField] private GameObject HighlightToPrefab;

    [SerializeField] private bool UpdateVisuals = true;
    [SerializeField] private bool UpdateStatsUI = true;

    private bool hasPerfTested = false;
    private const int PERFT_TEST_DEPTH = 9;

    public Dictionary<ulong, int> PositionHistory { get; private set; } = new Dictionary<ulong, int>();

    private GameObject fromHighlight = null;
    private GameObject toHighlight = null;

    private GameObject[,] pieceObjects = new GameObject[8, 8];

    private AudioSource AudioS;

    private TimeManager TimeHandler;
    private ChessboardImageCreator BoardHandler;

    private BoardState State;

    private UndoInfo undoInfo;

    private StatsHandler Stats;

    private bool isGameOver;

    void Awake()
    {
        BoardHandler = GetComponent<ChessboardImageCreator>();
        AudioS = GetComponent<AudioSource>();
        Stats = GetComponent<StatsHandler>();
        TimeHandler = GetComponent<TimeManager>();

        TimeHandler.FlagFell += HandleFlagFall;
    }

    private void OnDestroy()
    {
        if (TimeHandler != null)
            TimeHandler.FlagFell -= HandleFlagFall;
    }

    void Start()
    {
        InitialiseAgents();
        StartGame();
    }

    private void InitialiseAgents()
    {
        if (WhiteAgent == null) WhiteAgent = gameObject.AddComponent<HumanAgent>();
        if (BlackAgent == null) BlackAgent = gameObject.AddComponent<HumanAgent>();

        WhiteAgent.IsWhite = true;
        BlackAgent.IsWhite = false;

        Stats.InitialiseAgents(WhiteAgent, BlackAgent);
    }

    private void StartGame()
    {
        Debug.Log("Starting game");

        isGameOver = false;

        PositionHistory.Clear();

        string fen = GetStartingFen();

        State = FenParser.Parse(fen) ?? throw new ArgumentNullException(nameof(fen));

        // Perft test
        if (!hasPerfTested && fen == FenParser.INITFEN) Perft.PerftTestUpToDepth(this, PERFT_TEST_DEPTH, State);
        hasPerfTested = true;

        Stats.ResetStats();
        SyncVisuals();

        RecordPosition(State.ZobristKey);

        TimeHandler.StartNewGame(State.IsWhiteTurn);
        Stats.StartThinking(State.IsWhiteTurn);

        ChessAgent startingAgent = State.IsWhiteTurn ? WhiteAgent : BlackAgent;
        float remainingTime = TimeHandler.GetRemainingTime(State.IsWhiteTurn);

        startingAgent.StartTurn(State, remainingTime, TimeHandler.IncrementSeconds);
    }

    private string GetStartingFen()
    {
        if (!string.IsNullOrWhiteSpace(OverrideFen)) return OverrideFen;

        if (UseRandomPositions && RandomFens != null && RandomFens.Length > 0) return RandomFens[Random.Range(0, RandomFens.Length)]; // Random fen strings are selected without replacement

        return FenParser.INITFEN;
    }

    private void RecordPosition(ulong key)
    {
        PositionHistory.TryGetValue(key, out int count);
        PositionHistory[key] = count + 1;
    }

    private void RemovePosition(ulong key)
    {
        int count = PositionHistory[key] - 1;

        if (count == 0) PositionHistory.Remove(key);
        else PositionHistory[key] = count;
    }

    public bool IsThreefold() => PositionHistory.TryGetValue(State.ZobristKey, out int count) && count >= 3;

    public void MakeMove(Move move, ThinkStats? thinkStats = null)
    {
        if (isGameOver) return; // Debounce

        bool moveWasWhite = State.IsWhiteTurn;

        if (!TimeHandler.TryCompleteTurn(moveWasWhite)) return;

        Stats.StopThinking(moveWasWhite);

        undoInfo = State.MakeMove(move);

        RecordPosition(State.ZobristKey);
        SyncVisuals((move.From, move.To));
        Stats.UpdateAgentStats(moveWasWhite, thinkStats);

        GameResult? result = DetermineStatus();

        if (result.HasValue)
        {
            OnGameEnd(result.Value);
            return;
        }

        bool nextIsWhite = State.IsWhiteTurn;

        TimeHandler.StartTurn(nextIsWhite);

        float remainingTime = TimeHandler.GetRemainingTime(nextIsWhite);

        Stats.StartThinking(nextIsWhite);

        ChessAgent nextAgent = nextIsWhite ? WhiteAgent : BlackAgent;
        nextAgent.StartTurn(State, remainingTime, TimeHandler.IncrementSeconds);
    }

    private GameResult? DetermineStatus()
    {
        // Game end
        // MoveCount is decremented one if white plays to keep move count accurate
        if (State.IsMate())
        {
            if (State.IsWhiteTurn) Stats.blackWins += 1; else Stats.whiteWins += 1;
            AudioS.PlayOneShot(EndClip);
            return new GameResult(State.IsWhiteTurn ? "Black" : "White", "Checkmate", State.IsWhiteTurn ? State.FullMoveNumber - 1 : State.FullMoveNumber);
        }
        if (State.IsStalemate())
        {
            Stats.draws++;
            AudioS.PlayOneShot(EndClip);
            return new GameResult("Draw", "Stalemate", State.IsWhiteTurn ? State.FullMoveNumber - 1 : State.FullMoveNumber);
        }
        if (State.IsInsufficientMaterial())
        {
            Stats.draws++;
            AudioS.PlayOneShot(EndClip);
            return new GameResult("Draw", "Insufficient Material", State.IsWhiteTurn ? State.FullMoveNumber - 1 : State.FullMoveNumber);
        }
        if (State.IsFifty())
        {
            Stats.draws++;
            AudioS.PlayOneShot(EndClip);
            return new GameResult("Draw", "Fifty Move Rule", State.IsWhiteTurn ? State.FullMoveNumber - 1 : State.FullMoveNumber);
        }
        // Three-fold
        if (PositionHistory.TryGetValue(State.ZobristKey, out int count) && count >= 3)
        { 
            Stats.draws++;
            AudioS.PlayOneShot(EndClip);
            return new GameResult("Draw", "Three-Fold Repetition", State.IsWhiteTurn ? State.FullMoveNumber - 1 : State.FullMoveNumber);
        }

        // Game continues
        if (State.IsCheck()) AudioS.PlayOneShot(CheckClip);
        else AudioS.PlayOneShot(MoveClip);

        return null;
    }

    private void OnGameEnd(GameResult result)
    {
        if (isGameOver) return;

        isGameOver = true;

        TimeHandler.StopClock();

        // Later add reasons to csv or something
        Debug.Log($"Game over — Winner: {result.Winner}, Reason: {result.Reason}, Moves: {result.MoveCount}");

        Stats.UpdateGameStats(result);

        // Wait a couple of secs before starting new game
        StartCoroutine(RestartAfterDelay());
    }

    private void HandleFlagFall(bool whiteFlagged)
    {
        ChessAgent flaggedAgent = whiteFlagged
            ? WhiteAgent
            : BlackAgent;

        flaggedAgent.AbortTurn();

        Stats.StopThinking(whiteFlagged);

        if (whiteFlagged) Stats.blackWins++;
        else Stats.whiteWins++;

        AudioS.PlayOneShot(EndClip);

        int moveCount = State.IsWhiteTurn
            ? State.FullMoveNumber - 1
            : State.FullMoveNumber;

        OnGameEnd(new GameResult(whiteFlagged ? "Black" : "White", "Time", moveCount));
    }

    private IEnumerator RestartAfterDelay()
    {
        yield return new WaitForSeconds(2f);

        StartGame();
    }

    private void SpawnAllPieces()
    {
        for (int rank = 0; rank < 8; rank++)
        { 
            for (int file = 0; file < 8; file++)
            {
                int square = rank * 8 + file;

                ulong bit = 1UL << square;

                int prefabIndex = -1;

                if ((State.WhitePawns & bit) != 0) prefabIndex = (int)PiecePrefabIndex.WPawn;
                else if ((State.WhiteKnights & bit) != 0) prefabIndex = (int)PiecePrefabIndex.WKnight;
                else if ((State.WhiteBishops & bit) != 0) prefabIndex = (int)PiecePrefabIndex.WBishop;
                else if ((State.WhiteRooks & bit) != 0) prefabIndex = (int)PiecePrefabIndex.WRook;
                else if ((State.WhiteQueens & bit) != 0) prefabIndex = (int)PiecePrefabIndex.WQueen;
                else if ((State.WhiteKing & bit) != 0) prefabIndex = (int)PiecePrefabIndex.WKing;

                else if ((State.BlackPawns & bit) != 0) prefabIndex = (int)PiecePrefabIndex.BPawn;
                else if ((State.BlackKnights & bit) != 0) prefabIndex = (int)PiecePrefabIndex.BKnight;
                else if ((State.BlackBishops & bit) != 0) prefabIndex = (int)PiecePrefabIndex.BBishop;
                else if ((State.BlackRooks & bit) != 0) prefabIndex = (int)PiecePrefabIndex.BRook;
                else if ((State.BlackQueens & bit) != 0) prefabIndex = (int)PiecePrefabIndex.BQueen;
                else if ((State.BlackKing & bit) != 0) prefabIndex = (int)PiecePrefabIndex.BKing;

                if (prefabIndex != -1) SpawnPiece(file, rank, prefabIndex);
            }
        }   
    }

    private void SpawnPiece(int file, int rank, int prefabIndex)
    {
        GameObject prefab = PiecePrefabs[prefabIndex];
        if (prefab == null) throw new ArgumentException($"Invalid piece prefab index recieved: {prefabIndex}");

        int childIndex = (7 - rank) * 8 + file;
        Transform cell = BoardHandler.GetSquare(file, rank);

        GameObject go = Instantiate(prefab, cell);

        pieceObjects[file, rank] = go;
    }

    public void SyncVisuals((int fromSq, int toSq)? fromToSq = null)
    {
        // Clear board
        foreach (var go in pieceObjects)
            if (go != null) Destroy(go);
        if (fromHighlight != null)
        {
            Destroy(fromHighlight);
            fromHighlight = null;
        }
        if (toHighlight != null)
        {
            Destroy(toHighlight);
            toHighlight = null;
        }

        // Add move highlights
        if (fromToSq.HasValue)
        {
            int fromSq = fromToSq.Value.fromSq;
            int toSq = fromToSq.Value.toSq;

            int fromFile = fromSq % 8;
            int fromRank = fromSq / 8;

            int toFile = toSq % 8;
            int toRank = toSq / 8;

            Transform fromCell = BoardHandler.GetSquare(fromFile, fromRank);
            Transform toCell = BoardHandler.GetSquare(toFile, toRank);

            fromHighlight = Instantiate(HighlightFromPrefab, fromCell);
            toHighlight = Instantiate(HighlightToPrefab, toCell);
        }

        pieceObjects = new GameObject[8, 8];
        SpawnAllPieces();
    }

    public GameObject GetPieceObjectOnSquare(int sq) => GetPieceObjectOnSquare(sq % 8, sq / 8);
    public GameObject GetPieceObjectOnSquare(int file, int rank) => pieceObjects[file, rank];
}

public struct GameResult
{
    public readonly string Winner;
    public readonly string Reason;
    public readonly int MoveCount;

    public GameResult(string winner, string reason, int moveCount)
    {
        Winner = winner;
        Reason = reason;
        MoveCount = moveCount;
    }
}

public enum PiecePrefabIndex
{
    WPawn = 0,
    WKnight = 1,
    WBishop = 2,
    WRook = 3,
    WQueen = 4,
    WKing = 5,
    BPawn = 6,
    BKnight = 7,
    BBishop = 8,
    BRook = 9,
    BQueen = 10,
    BKing = 11,
}