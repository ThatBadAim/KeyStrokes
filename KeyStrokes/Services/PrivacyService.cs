using KeyStrokes.Interop;
using KeyStrokes.Models;

namespace KeyStrokes.Services;

/// <summary>
/// Decides whether capture should be suspended for the currently focused window.
/// This is the "sensitive field masking" guardrail: when a password manager or a
/// login/authentication window is in the foreground, keystrokes are discarded at
/// the source and never counted or stored.
/// </summary>
public sealed class PrivacyService
{
    private volatile string[] _processes = Array.Empty<string>();
    private volatile string[] _titleKeywords = Array.Empty<string>();

    private volatile bool _currentlyExcluded;
    private volatile string _currentReason = string.Empty;

    /// <summary>True when the focused app matches an exclusion rule.</summary>
    public bool IsCurrentlyExcluded => _currentlyExcluded;

    /// <summary>Human-readable description of why capture is paused (for the UI).</summary>
    public string CurrentReason => _currentReason;

    public event Action? ExclusionChanged;

    public void UpdateRules(AppSettings settings)
    {
        _processes = settings.ExcludedProcesses
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p.Trim().ToLowerInvariant())
            .ToArray();

        _titleKeywords = settings.ExcludedTitleKeywords
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p.Trim().ToLowerInvariant())
            .ToArray();
    }

    /// <summary>Re-evaluate against a newly focused window. Called from the monitor thread.</summary>
    public void Evaluate(ForegroundInfo info)
    {
        bool excluded = false;
        string reason = string.Empty;

        string proc = info.ProcessName?.ToLowerInvariant() ?? string.Empty;
        if (proc.Length > 0)
        {
            foreach (var rule in _processes)
            {
                if (proc.Contains(rule))
                {
                    excluded = true;
                    reason = $"Paused — {info.ProcessName} is a protected app";
                    break;
                }
            }
        }

        if (!excluded && !string.IsNullOrEmpty(info.Title))
        {
            string title = info.Title.ToLowerInvariant();
            foreach (var kw in _titleKeywords)
            {
                if (title.Contains(kw))
                {
                    excluded = true;
                    reason = "Paused — a sign-in / password field is focused";
                    break;
                }
            }
        }

        if (excluded != _currentlyExcluded || reason != _currentReason)
        {
            _currentlyExcluded = excluded;
            _currentReason = reason;
            ExclusionChanged?.Invoke();
        }
    }
}
