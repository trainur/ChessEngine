using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using UnityEngine;

public abstract class ChessAgent : MonoBehaviour
{
    [HideInInspector] public bool IsWhite;
    [SerializeField, Range(0f, 5f)] private float DelayTime = 0f;
    protected BoardManager Manager;
    protected int evaluatedStates;
    protected List<ulong> PositionHistory => Manager.PositionHistory;
    protected int MATE_SCORE = 1_000_000;
    public virtual int MateScore => MATE_SCORE;
    public virtual string Name => GetType().Name;

    protected virtual void Awake() => Manager = FindAnyObjectByType<BoardManager>();

    public virtual int? SearchDepth => null;

    public virtual void StartTurn(BoardState state)
    {
        evaluatedStates = 0;
        StartCoroutine(ThinkCoroutine(state));
    }

    protected virtual IEnumerator ThinkCoroutine(BoardState state)
    {
        // Let frame render first
        yield return null;

        // Think delay
        yield return new WaitForSeconds(DelayTime);

        Stopwatch stopwatch = Stopwatch.StartNew();

        Task<SearchResult> searchTask = Task.Run(() => ChooseMove(state));

        // Yield each frame until search is complete
        while (!searchTask.IsCompleted) yield return null;

        // Rethrow any exceptions from the backgrond thread
        if (searchTask.IsFaulted) throw searchTask.Exception;

        stopwatch.Stop();

        float elapsed = (float)stopwatch.Elapsed.TotalSeconds;

        SearchResult result = searchTask.Result;

        ThinkStats thinkStats = new ThinkStats(
            (float)stopwatch.Elapsed.TotalSeconds,
            result.PositionsEvaluated,
            result.EvaluationScore
        );

        OnMoveChosen(result.Move, thinkStats);
    }
    protected virtual void OnMoveChosen(Move move, ThinkStats thinkStats)
    {
        Manager.MakeMove(move, thinkStats);
    }

    protected abstract SearchResult ChooseMove(BoardState state);
}

public class HumanAgent : ChessAgent
{
    private BoardInput Input;

    protected override void Awake()
    {
        base.Awake();
        Input = GetComponent<BoardInput>();
    }

    public override void StartTurn(BoardState state)
    {
        Input.SetUserInputEnabled(true, state);
        Input.MoveChosen += OnMoveChosen;
    }

    private void OnMoveChosen(Move move)
    {
        Input.MoveChosen -= OnMoveChosen;
        Input.SetUserInputEnabled(false);
        Manager.MakeMove(move, null);
    }

    // Bypass ChooseMove as HumanAgent clearly isn't an AI and shouldn't run the AI agent pipeline.
    protected override SearchResult ChooseMove(BoardState state) => default;
}

public readonly struct ThinkStats
{
    public readonly float ThinkTime;
    public readonly int PositionsEvaluated;
    public readonly int? EvaluationScore;

    public ThinkStats(float thinkTime, int positionsEvaluated, int? evaluationScore = null)
    {
        ThinkTime = thinkTime;
        PositionsEvaluated = positionsEvaluated;
        EvaluationScore = evaluationScore;
    }
}

public readonly struct SearchResult
{
    public readonly Move Move;
    public readonly int? EvaluationScore;
    public readonly int PositionsEvaluated;

    public SearchResult(Move move, int? evaluationScore, int positionsEvaluated)
    {
        Move = move;
        EvaluationScore = evaluationScore;
        PositionsEvaluated = positionsEvaluated;
    }
}