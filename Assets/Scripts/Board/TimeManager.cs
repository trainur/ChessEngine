using UnityEngine;
using System;

public class TimeManager : MonoBehaviour
{
    [Header("Time Control")]
    [SerializeField, Min(0f)] private float GameTimeSeconds = 60f;
    [SerializeField, Min(0f)] private float FischerIncrementSeconds = 0.2f;

    public event Action<bool> FlagFell;

    private float whiteRemaining;
    private float blackRemaining;

    private double turnStartTime;
    private bool activeIsWhite;
    private bool isRunning = false;

    public float WhiteRemaining => GetRemainingTime(isWhite: true);
    public float BlackRemaining => GetRemainingTime(isWhite: false);

    public float IncrementSeconds => FischerIncrementSeconds;
    public bool IsRunning => isRunning;
    public bool ActiveIsWhite => activeIsWhite;

    private double Now => Time.realtimeSinceStartupAsDouble;

    private void Update()
    {
        if (!isRunning) return;

        float remaining = GetActiveRemainingTime();

        if (remaining <= 0f) HandleFlagFall(); 
    }

    public void StartNewGame(bool whiteToMove)
    {
        whiteRemaining = GameTimeSeconds;
        blackRemaining = GameTimeSeconds;

        activeIsWhite = whiteToMove;
        turnStartTime = Now;
        isRunning = true;

        Debug.Log(
        $"[CLOCK START] " +
        $"Unity time={Now:F3}, " +
        $"side={(whiteToMove ? "White" : "Black")}, " +
        $"remaining={GameTimeSeconds:F3}"
    );
    }

    public void StartTurn(bool isWhite)
    {
        if (isRunning) throw new InvalidOperationException("Cannot start a new turn while the clock is already running!");

        activeIsWhite = isWhite;
        turnStartTime = Now;
        isRunning = true;
    }

    public bool TryCompleteTurn(bool whiteMoved)
    {
        if (!isRunning)
        {
            Debug.LogWarning("Tried to complete a turn while the clock was not running.");

            return false;
        }

        // Catch timer mismatches
        if (activeIsWhite != whiteMoved)
        {
            Debug.LogWarning(
                $"Clock turn mismatch. " +
                $"Active: {(activeIsWhite ? "White" : "Black")}, " +
                $"Moved: {(whiteMoved ? "White" : "Black")}."
            );

            return false;
        }

        CommitElapsedTime();

        isRunning = false;

        float remaining = GetStoredTime(whiteMoved);

        if (remaining <= 0f)
        {
            SetStoredTime(whiteMoved, 0f);
            FlagFell?.Invoke(whiteMoved);

            return false;
        }

        SetStoredTime(whiteMoved, remaining + FischerIncrementSeconds);

        return true;
    }

    public void StopClock()
    {
        if (!isRunning) return;

        CommitElapsedTime();

        float remaining = GetStoredTime(activeIsWhite);

        if (remaining < 0f) SetStoredTime(activeIsWhite, 0f);

        isRunning = false;
    }

    public float GetRemainingTime(bool isWhite)
    {
        float remaining = GetStoredTime(isWhite);

        if (isRunning && activeIsWhite == isWhite)
        {
            double elapsed = Math.Max(0.0, Now - turnStartTime);

            remaining -= (float)elapsed;
        }

        return Mathf.Max(0f, remaining);
    }

    private float GetActiveRemainingTime()
    {
        return GetRemainingTime(activeIsWhite);
    }

    private void CommitElapsedTime()
    {
        double elapsed = Math.Max(0.0, Now - turnStartTime);

        float remaining = GetStoredTime(activeIsWhite) - (float)elapsed;

        SetStoredTime(activeIsWhite, remaining);

        // Keeps the internal timestamp consistent if this method is followed by further clock operations.
        turnStartTime = Now;
    }

    private void HandleFlagFall()
    {
        if (!isRunning)
            return;

        bool flaggedPlayerIsWhite = activeIsWhite;

        SetStoredTime(flaggedPlayerIsWhite, 0f);
        isRunning = false;

        FlagFell?.Invoke(flaggedPlayerIsWhite);
    }

    private float GetStoredTime(bool isWhite)
    {
        return isWhite
            ? whiteRemaining
            : blackRemaining;
    }

    private void SetStoredTime(bool isWhite, float value)
    {
        if (isWhite)
            whiteRemaining = value;
        else
            blackRemaining = value;
    }
}