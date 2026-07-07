using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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

    private TimeManager TimeHandler;

    private void Awake()
    {
        TimeHandler = GetComponent<TimeManager>();
    }

    private void Update()
    {
        WhiteStats.RefreshDepth();
        BlackStats.RefreshDepth();

        if (TimeHandler == null)
            return;

        WhiteStats.UpdateTime(
            TimeHandler.GetRemainingTime(true)
        );

        BlackStats.UpdateTime(
            TimeHandler.GetRemainingTime(false)
        );
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
        StopThinking(true);
        StopThinking(false);

        WhiteStats.Init("White", WhiteAgent);
        BlackStats.Init("Black", BlackAgent);

        SetTimerActive(true, false);
        SetTimerActive(false, false);
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
        // Ensure the other animation is stopped.
        StopThinking(!isWhite);

        SetTimerActive(isWhite, true);
        SetTimerActive(!isWhite, false);

        if (isWhite)
        {
            if (whiteThinkingCoroutine != null)
                StopCoroutine(whiteThinkingCoroutine);

            whiteThinkingCoroutine =
                StartCoroutine(ThinkingAnimation(WhiteStats));
        }
        else
        {
            if (blackThinkingCoroutine != null)
                StopCoroutine(blackThinkingCoroutine);

            blackThinkingCoroutine =
                StartCoroutine(ThinkingAnimation(BlackStats));
        }
    }

    public void StopThinking(bool isWhite)
    {
        if (isWhite)
        {
            if (whiteThinkingCoroutine != null)
                StopCoroutine(whiteThinkingCoroutine);

            whiteThinkingCoroutine = null;
        }
        else
        {
            if (blackThinkingCoroutine != null)
                StopCoroutine(blackThinkingCoroutine);

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
            yield return new WaitForSecondsRealtime(0.4f);
        }
    }

    public void SetTimerActive(bool isWhite, bool active)
    {
        if (isWhite) WhiteStats.SetTimerActive(active);
        else BlackStats.SetTimerActive(active);
    }

    [Serializable]
    private sealed class AgentStatsPanel
    {
        [SerializeField] private TMP_Text Header;
        [SerializeField] private TMP_Text ThinkTimeText;
        [SerializeField] private TMP_Text PosEvalText;
        [SerializeField] private TMP_Text DepthText;
        [SerializeField] private TMP_Text EvalScoreText;
        [SerializeField] private TMP_Text TimeRemainingText;
        [SerializeField] private bool ReverseEval;

        private ChessAgent Agent;
        private string SideName;

        private string lastAvgTime = null;

        private int? lastDepth;
        private float totalThinkTime;
        private int thinkCount;

        [Header("Dim Target")]
        [Tooltip("If set, this background image is dimmed/lit instead of the timer text.")]
        [SerializeField] private Graphic DimBackground;

        private readonly Color TimerDimColour = new Color(0.55f, 0.55f, 0.55f);
        private Color activeColour;
        private bool activeColourCaptured;

        public void Init(string sideName, ChessAgent agent)
        {
            SideName = sideName;
            Agent = agent;

            lastAvgTime = null;
            lastDepth = null;
            totalThinkTime = 0f;
            thinkCount = 0;

            if (!activeColourCaptured)
            {
                activeColour = DimBackground != null
                    ? DimBackground.color
                    : TimeRemainingText.color;

                activeColourCaptured = true;
            }


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
                ? $" <color=#9AA3B2>(avg {lastAvgTime})</color>"
                : "";

            SetLine(ThinkTimeText, "Think Time", text + avg);
        }

        public void UpdateTime(float? remainingTime)
        {
            if (!remainingTime.HasValue)
            {
                TimeRemainingText.text = "N/A";
                return;
            }

            float time = Mathf.Max(0f, remainingTime.Value);

            if (time < 1f)
            {
                int hundredths = Mathf.FloorToInt(time * 100f);

                TimeRemainingText.text = $"00:00.{hundredths:D2}";
                return;
            }

            int totalSeconds = Mathf.FloorToInt(time);

            int mins = totalSeconds / 60;
            int secs = totalSeconds % 60;

            TimeRemainingText.text = $"{mins:D2}:{secs:D2}";
        }

        public void SetTimerActive(bool active)
        {
            Color target = active ? activeColour : TimerDimColour;

            if (DimBackground != null) DimBackground.color = target;
            else TimeRemainingText.color = target;
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
