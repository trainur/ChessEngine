using System;
using TMPro;
using UnityEngine;
using System.Collections;

public class StatsHandler : MonoBehaviour
{
    [Header("Game Statistics")]
    [SerializeField] private TMP_Text ScoreText;
    [SerializeField] private TMP_Text ReasonText;
    [SerializeField] private TMP_Text MovesText;
    [SerializeField] private TMP_Text WinnerText;

    [Header("Agent Statistics")]
    [SerializeField] private AgentStatsPanel WhiteStats;
    [SerializeField] private AgentStatsPanel BlackStats;

    [HideInInspector] public int whiteWins = 0;
    [HideInInspector] public int blackWins = 0;
    [HideInInspector] public int draws = 0;

    private ChessAgent WhiteAgent;
    private ChessAgent BlackAgent;

    private Coroutine whiteThinkingCoroutine;
    private Coroutine blackThinkingCoroutine;

    private void Update()
    {
        WhiteStats.RefreshDepth();
        BlackStats.RefreshDepth();
    }

    public void InitialiseAgents(ChessAgent whiteAgent, ChessAgent blackAgent)
    {
        WhiteAgent = whiteAgent;
        BlackAgent = blackAgent;

        WhiteStats.Init("White", WhiteAgent);
        BlackStats.Init("Black", BlackAgent);
    }

    public void ResetStats()
    {

        WhiteStats.Init("White", WhiteAgent);
        BlackStats.Init("Black", BlackAgent);
    }

    public void UpdateAgentStats(bool moveWasWhite, ThinkStats? thinkStats = null)
    {
        if (thinkStats.HasValue)
        {
            if (moveWasWhite)
                WhiteStats.SetStats(thinkStats.Value);
            else
                BlackStats.SetStats(thinkStats.Value);
        }
    }
    public void UpdateGameStats(GameResult gameResult)
    {
        ScoreText.text = $"{whiteWins} - {draws} - {blackWins}";
        ReasonText.text = $"<color=#a9a9a9>Reason:</color> <b>{gameResult.Reason}</b>";
        WinnerText.text = $"<color=#a9a9a9>Winner:</color> <b>{gameResult.Winner}</b>";
        MovesText.text = $"<color=#a9a9a9>Moves:</color> <b>{gameResult.MoveCount}</b>";
    }

    public void StartThinking(bool isWhite)
    {
        if (isWhite)
        {
            if (whiteThinkingCoroutine != null) StopCoroutine(whiteThinkingCoroutine);
            whiteThinkingCoroutine = StartCoroutine(ThinkingAnimation(WhiteStats));
        }
        else
        {
            if (blackThinkingCoroutine != null) StopCoroutine(blackThinkingCoroutine);
            blackThinkingCoroutine = StartCoroutine(ThinkingAnimation(BlackStats));
        }
    }

    public void StopThinking(bool isWhite)
    {
        if (isWhite)
        {
            if (whiteThinkingCoroutine != null) StopCoroutine(whiteThinkingCoroutine);
            whiteThinkingCoroutine = null;
        }
        else
        {
            if (blackThinkingCoroutine != null) StopCoroutine(blackThinkingCoroutine);
            blackThinkingCoroutine = null;
        }
    }

    private IEnumerator ThinkingAnimation(AgentStatsPanel panel)
    {
        string[] frames = { "Thinking.  ", "Thinking.. ", "Thinking..." };
        int i = 0;

        while (true)
        {
            panel.StartThinkingText(frames[i % frames.Length]);
            i++;
            yield return new WaitForSeconds(0.4f);
        }
    }

    [Serializable]
    private sealed class AgentStatsPanel
    {
        [SerializeField] private TMP_Text Header;
        [SerializeField] private TMP_Text ThinkTimeText;
        [SerializeField] private TMP_Text PosEvalText;
        [SerializeField] private TMP_Text DepthText;
        [SerializeField] private TMP_Text EvalScoreText;
        [SerializeField] private bool ReverseEval;

        private ChessAgent Agent;
        private string SideName;

        private string lastAvgTime = null;

        private int? lastDepth;
        private float totalThinkTime;
        private int thinkCount;

        public void Init(string sideName, ChessAgent agent)
        {
            SideName = sideName;
            Agent = agent;

            totalThinkTime = 0f;
            thinkCount = 0;

            Header.text = $"{SideName} Stats ({Agent.Name})";

            SetLine(ThinkTimeText, "Think Time", "N/A");
            SetLine(PosEvalText, "Positions Evaluated", "N/A");
            SetLine(EvalScoreText, "Last Evaluated Score", "N/A");

            RefreshDepth(force: true);
        }

        public void RefreshDepth(bool force = false)
        {
            if (Agent == null) return;

            int? currentDepth = Agent.SearchDepth;

            if (!force && currentDepth == lastDepth) return;

            lastDepth = currentDepth;

            string depthText = currentDepth.HasValue
                ? $"{currentDepth.Value} ply"
                : "N/A";

            SetLine(DepthText, "Search Depth", depthText);
        }

        public void StartThinkingText(string text)
        {
            string avg = lastAvgTime != null
                ? $" <color=#9AA3B2>(avg {lastAvgTime}s)</color>"
                : "";

            SetLine(ThinkTimeText, "Think Time", text + avg);
        }

        public void SetStats(ThinkStats stats)
        {
            thinkCount++;
            totalThinkTime += stats.ThinkTime;

            float averageThinkTime = totalThinkTime / thinkCount;
            lastAvgTime = $"{averageThinkTime:F3}s";

            SetLine(
                ThinkTimeText,
                "Think Time",
                $"{stats.ThinkTime:F3}s <color=#9AA3B2>(avg {lastAvgTime})</color>"
            );

            SetLine(
                PosEvalText,
                "Positions Evaluated",
                stats.PositionsEvaluated.ToString("N0")
            );

            if (stats.EvaluationScore.HasValue)
            {
                string evalText = FormatEvalScore(stats.EvaluationScore.Value);
                SetLine(EvalScoreText, "Last Evaluated Score", evalText);
            }
        }

        private static void SetLine(TMP_Text text, string label, string value)
        {
            text.text = $"<color=#a9a9a9>{label}:</color> <b>{value}</b>";
        }

        private string FormatEvalScore(float evalScore)
        {
            int score = float.IsInfinity(evalScore) || float.IsNaN(evalScore)
                ? (int)Mathf.Sign(evalScore) * int.MaxValue
                : (int)evalScore;

            if (score >= int.MaxValue / 2) return "+M";
            if (score <= int.MinValue / 2) return "-M";

            if (Agent != null && Math.Abs(score) >= Agent.MateScore)
            {
                int remainingDepth = Math.Abs(score) - Agent.MateScore;

                int rootDepth = Agent.SearchDepth ?? remainingDepth;
                int mateInPlies = Math.Max(1, rootDepth - remainingDepth);
                int mateInMoves = (int)Math.Ceiling(mateInPlies / 2f);

                return score > 0 ? $"+M{mateInMoves}" : $"-M{mateInMoves}";
            }

            if (ReverseEval) score *= -1;

            return score.ToString("+0;-0;0");
        }
    } 
}
