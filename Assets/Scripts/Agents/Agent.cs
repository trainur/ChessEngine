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
    protected ulong[] PositionStack = new ulong[512];
    protected int PositionStackCount;
    protected int MATE_SCORE = 1_000_000;
    public virtual int MateScore => MATE_SCORE;
    public virtual string Name => GetType().Name;

    protected virtual void Awake() => Manager = FindAnyObjectByType<BoardManager>();

    public virtual int? SearchDepth => null;

    public virtual void StartTurn(BoardState state)
    {
        evaluatedStates = 0;

        CopyPositionHistoryToStack();

        StartCoroutine(ThinkCoroutine(state));
    }

    private void CopyPositionHistoryToStack()
    {
        PositionStackCount = 0;

        foreach (KeyValuePair<ulong, int> pair in Manager.PositionHistory)
            for (int i = 0; i < pair.Value; i++)
                PushPosition(pair.Key);
    }

    protected void PushPosition(ulong zobristKey)
    {
        if (PositionStackCount >= PositionStack.Length) System.Array.Resize(ref PositionStack, PositionStack.Length * 2);

        PositionStack[PositionStackCount] = zobristKey;
        PositionStackCount++;
    }

    protected void PopPosition() => PositionStackCount--;

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

    protected int GetPositionOccurrenceCount(ulong zobristKey)
    {
        int count = 0;

        for (int i = PositionStackCount - 1; i >= 0; i--)
        {
            if (PositionStack[i] == zobristKey)
                count++;
        }

        return count;
    }

    protected bool IsThreefold(ref BoardState state)
    {
        int count = 0;

        // Step by 2 because only positions with the same side to move can repeat
        // Rather than reuse GetPositionOccurenceCount, I've made a code smell. Just to allow early break outs for efficiency.
        for (int i = PositionStackCount - 1; i >= 0; i--)
            if (PositionStack[i] == state.ZobristKey)
            {
                count++;

                if (count >= 3)
                    return true;
            }

        return false;
    }

    protected UndoInfo MakeSearchMove(ref BoardState state, Move move)
    {
        UndoInfo undo = state.MakeMove(move);
        PushPosition(state.ZobristKey);
        return undo;
    }

    protected void UnmakeSearchMove(ref BoardState state, Move move, UndoInfo undo)
    {
        PopPosition();
        state.UnmakeMove(move, undo);
    }

    protected abstract SearchResult ChooseMove(BoardState state);
}

public class HumanAgent : ChessAgent
{
    private BoardInput Input;
    private Stopwatch stopwatch; // Not needed at all for humans, but might as well

    protected override void Awake()
    {
        base.Awake();
        Input = GetComponent<BoardInput>();
    }

    public override void StartTurn(BoardState state)
    {
        stopwatch = Stopwatch.StartNew();

        Input.MoveChosen -= OnMoveChosen; // prevent duplicates
        Input.MoveChosen += OnMoveChosen;

        Input.SetUserInputEnabled(true, state);
    }

    private void OnMoveChosen(Move move)
    {
        stopwatch.Stop();

        Input.MoveChosen -= OnMoveChosen;
        Input.SetUserInputEnabled(false);

        ThinkStats thinkStats = new ThinkStats(
            (float)stopwatch.Elapsed.TotalSeconds,
            0,
            null);

        Manager.MakeMove(move, thinkStats);
    }

    // Bypass ChooseMove as HumanAgent clearly isn't an AI and shouldn't run the AI agent pipeline.
    protected override SearchResult ChooseMove(BoardState state) => throw new System.NotSupportedException("Human agents must not use the ChooseMove agent pipeline.");
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