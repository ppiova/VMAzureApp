using System.Globalization;
using VMAzureApp.ViewModels;

namespace VMAzureApp.Models;

public sealed class VmSchedule : ObservableObject
{
    private VmScheduleAction _action;
    private bool _enabled = true;
    private bool _friday = true;
    private DateTime? _lastRunLocal;
    private string _lastResult = "Never run";
    private bool _monday = true;
    private bool _saturday;
    private string _scheduledTime = "18:00";
    private bool _sunday;
    private bool _thursday = true;
    private bool _tuesday = true;
    private bool _wednesday = true;

    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string ResourceId { get; set; } = string.Empty;

    public string VmName { get; set; } = string.Empty;

    public string SubscriptionName { get; set; } = string.Empty;

    public string ResourceGroupName { get; set; } = string.Empty;

    public VmScheduleAction Action
    {
        get => _action;
        set
        {
            if (SetProperty(ref _action, value))
            {
                OnPropertyChanged(nameof(ActionDisplay));
                OnPropertyChanged(nameof(Summary));
            }
        }
    }

    public string ScheduledTime
    {
        get => _scheduledTime;
        set
        {
            string normalized = NormalizeTime(value);
            if (SetProperty(ref _scheduledTime, normalized))
            {
                OnPropertyChanged(nameof(Summary));
            }
        }
    }

    public bool Enabled
    {
        get => _enabled;
        set => SetProperty(ref _enabled, value);
    }

    public bool Monday
    {
        get => _monday;
        set
        {
            if (SetProperty(ref _monday, value))
            {
                RaiseDaysChanged();
            }
        }
    }

    public bool Tuesday
    {
        get => _tuesday;
        set
        {
            if (SetProperty(ref _tuesday, value))
            {
                RaiseDaysChanged();
            }
        }
    }

    public bool Wednesday
    {
        get => _wednesday;
        set
        {
            if (SetProperty(ref _wednesday, value))
            {
                RaiseDaysChanged();
            }
        }
    }

    public bool Thursday
    {
        get => _thursday;
        set
        {
            if (SetProperty(ref _thursday, value))
            {
                RaiseDaysChanged();
            }
        }
    }

    public bool Friday
    {
        get => _friday;
        set
        {
            if (SetProperty(ref _friday, value))
            {
                RaiseDaysChanged();
            }
        }
    }

    public bool Saturday
    {
        get => _saturday;
        set
        {
            if (SetProperty(ref _saturday, value))
            {
                RaiseDaysChanged();
            }
        }
    }

    public bool Sunday
    {
        get => _sunday;
        set
        {
            if (SetProperty(ref _sunday, value))
            {
                RaiseDaysChanged();
            }
        }
    }

    public DateTime? LastRunLocal
    {
        get => _lastRunLocal;
        set
        {
            if (SetProperty(ref _lastRunLocal, value))
            {
                OnPropertyChanged(nameof(LastRunDisplay));
            }
        }
    }

    /// <summary>
    /// Timestamp of the last execution attempt (success or failure). Used to
    /// throttle retries within the catch-up window so a failing run is retried
    /// after <see cref="RetryBackoff"/> rather than every timer tick.
    /// </summary>
    public DateTime? LastAttemptLocal { get; set; }

    public string LastResult
    {
        get => _lastResult;
        set => SetProperty(ref _lastResult, value);
    }

    public string ActionDisplay => Action == VmScheduleAction.Start ? "Start" : "Stop / Deallocate";

    public string DaysDisplay
    {
        get
        {
            List<string> days = [];
            if (Monday) days.Add("Mon");
            if (Tuesday) days.Add("Tue");
            if (Wednesday) days.Add("Wed");
            if (Thursday) days.Add("Thu");
            if (Friday) days.Add("Fri");
            if (Saturday) days.Add("Sat");
            if (Sunday) days.Add("Sun");

            return days.Count == 7 ? "Every day" : string.Join(", ", days);
        }
    }

    public string LastRunDisplay => LastRunLocal is null
        ? "Never"
        : LastRunLocal.Value.ToString("g", CultureInfo.CurrentCulture);

    public string Summary => $"{ActionDisplay} {VmName} at {ScheduledTime} ({DaysDisplay})";

    /// <summary>
    /// Window after the scheduled time during which a missed run is still
    /// executed (for example, if the app was closed at the exact minute).
    /// </summary>
    public static readonly TimeSpan CatchUpWindow = TimeSpan.FromMinutes(90);

    /// <summary>Minimum delay between execution attempts after a failed run.</summary>
    public static readonly TimeSpan RetryBackoff = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Determines whether this schedule should run at the given local time,
    /// accounting for the enabled flag, the selected day, the catch-up window,
    /// whether it already ran successfully today, and a retry backoff so a
    /// failed run is retried after <see cref="RetryBackoff"/> rather than on
    /// every tick.
    /// </summary>
    public bool IsDue(DateTime now)
    {
        if (!Enabled || !IsScheduledFor(now) || !TryGetScheduledTime(out TimeOnly scheduledTime))
        {
            return false;
        }

        // Already completed successfully today.
        if (LastRunLocal?.Date == now.Date)
        {
            return false;
        }

        // A recent attempt failed; wait for the backoff before retrying.
        if (LastAttemptLocal is DateTime lastAttempt && now - lastAttempt < RetryBackoff)
        {
            return false;
        }

        DateTime scheduledDateTime = now.Date.Add(scheduledTime.ToTimeSpan());
        return now >= scheduledDateTime && now <= scheduledDateTime.Add(CatchUpWindow);
    }

    public bool IsScheduledFor(DateTime localDateTime)
    {
        return localDateTime.DayOfWeek switch
        {
            DayOfWeek.Monday => Monday,
            DayOfWeek.Tuesday => Tuesday,
            DayOfWeek.Wednesday => Wednesday,
            DayOfWeek.Thursday => Thursday,
            DayOfWeek.Friday => Friday,
            DayOfWeek.Saturday => Saturday,
            DayOfWeek.Sunday => Sunday,
            _ => false
        };
    }

    public bool TryGetScheduledTime(out TimeOnly time)
    {
        return TimeOnly.TryParseExact(ScheduledTime, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out time);
    }

    private static string NormalizeTime(string value)
    {
        return TimeOnly.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.None, out TimeOnly parsed)
            || TimeOnly.TryParseExact(value, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed)
            ? parsed.ToString("HH:mm", CultureInfo.InvariantCulture)
            : value;
    }

    private void RaiseDaysChanged()
    {
        OnPropertyChanged(nameof(DaysDisplay));
        OnPropertyChanged(nameof(Summary));
    }
}
