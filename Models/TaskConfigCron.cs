namespace WebReader.Models;

public enum TaskConfigCron
{
    //TODO: calc next run task time from last runed day
    EveryHour,
    EveryDay,
    EveryWeek,
    EveryMonth
}
