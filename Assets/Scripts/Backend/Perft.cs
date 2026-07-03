using System;
using System.Diagnostics;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using Debug = UnityEngine.Debug;

public static class Perft
{
    // https://www.chessprogramming.org/Perft_Results
    // Expected node count indexed per depth level for starting FEN
    private static readonly ulong[] DepthNodes = new ulong[11]
    {
        1,
        20,
        400,
        8_902,
        197_281,
        4_865_609,
        119_060_324,
        3_195_901_560,
        84_998_978_956,
        2_439_530_234_167,
        69_352_859_712_417
    };

    // https://www.chessprogramming.org/Perft
    private static ulong PerftTest(int depth, ref BoardState state)
    {
        if (depth == 0) return 1UL;

        var moves = MoveGenerator.GenerateMoves(ref state);

        if (depth == 1) return (ulong)moves.Count;

        ulong nodes = 0;

        foreach(Move move in moves)
        {
            UndoInfo undo = state.MakeMove(move);
            nodes += PerftTest(depth - 1, ref state);
            state.UnmakeMove(move, undo);
        }

        return nodes;
    }

    // Put into a new thread to avoid halting game execution during check
    [Conditional("PERFT_VALIDATION")]
    public static void PerftTestUpToDepth(MonoBehaviour runner, int depth, BoardState state)
    {
        runner.StartCoroutine(PerftTestUpToDepthCoroutine(depth, state));
    }

    private static IEnumerator PerftTestUpToDepthCoroutine(int depth, BoardState state)
    {
        if (depth < 0 || depth >= DepthNodes.Length) throw new ArgumentOutOfRangeException(nameof(depth));

        Debug.Log("Background perft testing in progress.");

        yield return null;

        for (int i = 0; i <= depth; i++)
        {
            int testDepth = i;
            BoardState stateCopy = state;

            Task<ulong> task = Task.Run(() =>
            {
                return PerftTest(testDepth, ref stateCopy);
            });

            while (!task.IsCompleted)
                yield return null;

            if (task.IsFaulted)
                throw task.Exception;

            ulong ac = task.Result;
            ulong exp = DepthNodes[i];

            if (ac != exp) throw new InvalidOperationException($"Perft failed at depth {i}. Expected {exp:N0}, got {ac:N0}");

            Debug.Log($"Perft depth {i} passed. Nodes: {ac:N0}");

            yield return null;
        }

        Debug.Log($"Perft test passed up to depth {depth}");
    }
}
