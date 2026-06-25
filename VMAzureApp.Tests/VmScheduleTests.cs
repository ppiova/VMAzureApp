using VMAzureApp.Models;
using Xunit;

namespace VMAzureApp.Tests;

public class VmScheduleTests
{
    // A reference "now": the time component is what each test overrides.
    // The day-of-week is read from this value so tests never hardcode a weekday.
    private static readonly DateTime ReferenceDay = new(2026, 6, 25, 0, 0, 0, DateTimeKind.Local);

    private static VmSchedule ScheduleFor(DayOfWeek day, string time = "18:00")
    {
        return new VmSchedule
        {
            ResourceId = "/subscriptions/x/resourceGroups/rg/providers/Microsoft.Compute/virtualMachines/vm",
            VmName = "vm",
            Enabled = true,
            ScheduledTime = time,
            Action = VmScheduleAction.StopDeallocate,
            Monday = day == DayOfWeek.Monday,
            Tuesday = day == DayOfWeek.Tuesday,
            Wednesday = day == DayOfWeek.Wednesday,
            Thursday = day == DayOfWeek.Thursday,
            Friday = day == DayOfWeek.Friday,
            Saturday = day == DayOfWeek.Saturday,
            Sunday = day == DayOfWeek.Sunday,
        };
    }

    private static DateTime At(int hour, int minute)
    {
        return ReferenceDay.Date.AddHours(hour).AddMinutes(minute);
    }

    [Fact]
    public void IsDue_True_WhenJustPastScheduledTime()
    {
        DateTime now = At(18, 5);
        VmSchedule schedule = ScheduleFor(now.DayOfWeek, "18:00");

        Assert.True(schedule.IsDue(now));
    }

    [Fact]
    public void IsDue_True_AtExactScheduledTime()
    {
        DateTime now = At(18, 0);
        VmSchedule schedule = ScheduleFor(now.DayOfWeek, "18:00");

        Assert.True(schedule.IsDue(now));
    }

    [Fact]
    public void IsDue_False_BeforeScheduledTime()
    {
        DateTime now = At(17, 55);
        VmSchedule schedule = ScheduleFor(now.DayOfWeek, "18:00");

        Assert.False(schedule.IsDue(now));
    }

    [Fact]
    public void IsDue_False_AfterCatchUpWindow()
    {
        DateTime now = At(18, 0).Add(VmSchedule.CatchUpWindow).AddMinutes(1);
        VmSchedule schedule = ScheduleFor(now.DayOfWeek, "18:00");

        Assert.False(schedule.IsDue(now));
    }

    [Fact]
    public void IsDue_True_AtEdgeOfCatchUpWindow()
    {
        DateTime now = At(18, 0).Add(VmSchedule.CatchUpWindow);
        VmSchedule schedule = ScheduleFor(now.DayOfWeek, "18:00");

        Assert.True(schedule.IsDue(now));
    }

    [Fact]
    public void IsDue_False_WhenDisabled()
    {
        DateTime now = At(18, 5);
        VmSchedule schedule = ScheduleFor(now.DayOfWeek, "18:00");
        schedule.Enabled = false;

        Assert.False(schedule.IsDue(now));
    }

    [Fact]
    public void IsDue_False_WhenNotScheduledForToday()
    {
        DateTime now = At(18, 5);
        DayOfWeek otherDay = (DayOfWeek)(((int)now.DayOfWeek + 1) % 7);
        VmSchedule schedule = ScheduleFor(otherDay, "18:00");

        Assert.False(schedule.IsDue(now));
    }

    [Fact]
    public void IsDue_False_WhenAlreadyRanToday()
    {
        DateTime now = At(18, 5);
        VmSchedule schedule = ScheduleFor(now.DayOfWeek, "18:00");
        schedule.LastRunLocal = At(18, 1);

        Assert.False(schedule.IsDue(now));
    }

    [Fact]
    public void IsDue_True_WhenLastRunWasYesterday()
    {
        DateTime now = At(18, 5);
        VmSchedule schedule = ScheduleFor(now.DayOfWeek, "18:00");
        schedule.LastRunLocal = now.AddDays(-1);

        Assert.True(schedule.IsDue(now));
    }

    [Fact]
    public void IsDue_False_WhenAttemptWithinRetryBackoff()
    {
        DateTime now = At(18, 30);
        VmSchedule schedule = ScheduleFor(now.DayOfWeek, "18:00");
        // Failed run a moment ago (LastRunLocal not set), within the backoff.
        schedule.LastAttemptLocal = now.AddMinutes(-1);

        Assert.False(schedule.IsDue(now));
    }

    [Fact]
    public void IsDue_True_WhenAttemptOlderThanRetryBackoff()
    {
        DateTime now = At(18, 30);
        VmSchedule schedule = ScheduleFor(now.DayOfWeek, "18:00");
        // Failed earlier; backoff elapsed and still inside the catch-up window.
        schedule.LastAttemptLocal = now.Add(-VmSchedule.RetryBackoff).AddMinutes(-1);

        Assert.True(schedule.IsDue(now));
    }

    [Fact]
    public void IsDue_False_WhenTimeIsInvalid()
    {
        DateTime now = At(18, 5);
        VmSchedule schedule = ScheduleFor(now.DayOfWeek, "not-a-time");

        Assert.False(schedule.IsDue(now));
    }

    [Theory]
    [InlineData("18:00", "18:00")]
    [InlineData("07:30", "07:30")]
    [InlineData("00:00", "00:00")]
    public void ScheduledTime_IsNormalizedToHHmm(string input, string expected)
    {
        VmSchedule schedule = new() { ScheduledTime = input };

        Assert.Equal(expected, schedule.ScheduledTime);
    }

    [Fact]
    public void ScheduledTime_KeepsInvalidValueAsIs()
    {
        VmSchedule schedule = new() { ScheduledTime = "garbage" };

        Assert.Equal("garbage", schedule.ScheduledTime);
        Assert.False(schedule.TryGetScheduledTime(out _));
    }

    [Fact]
    public void DaysDisplay_ShowsEveryDay_WhenAllSelected()
    {
        VmSchedule schedule = new()
        {
            Monday = true,
            Tuesday = true,
            Wednesday = true,
            Thursday = true,
            Friday = true,
            Saturday = true,
            Sunday = true,
        };

        Assert.Equal("Every day", schedule.DaysDisplay);
    }

    [Fact]
    public void DaysDisplay_ListsSelectedDays_InOrder()
    {
        VmSchedule schedule = ScheduleFor(DayOfWeek.Monday);
        schedule.Wednesday = true;

        Assert.Equal("Mon, Wed", schedule.DaysDisplay);
    }

    [Fact]
    public void Summary_IncludesActionTimeAndDays()
    {
        VmSchedule schedule = ScheduleFor(DayOfWeek.Monday, "09:15");
        schedule.VmName = "build-agent";
        schedule.Action = VmScheduleAction.Start;

        Assert.Equal("Start build-agent at 09:15 (Mon)", schedule.Summary);
    }

    [Fact]
    public void IsScheduledFor_MatchesTheConfiguredDay()
    {
        VmSchedule schedule = ScheduleFor(DayOfWeek.Monday);

        Assert.True(schedule.IsScheduledFor(NextDate(DayOfWeek.Monday)));
        Assert.False(schedule.IsScheduledFor(NextDate(DayOfWeek.Tuesday)));
    }

    private static DateTime NextDate(DayOfWeek day)
    {
        DateTime date = ReferenceDay.Date;
        while (date.DayOfWeek != day)
        {
            date = date.AddDays(1);
        }

        return date;
    }
}
