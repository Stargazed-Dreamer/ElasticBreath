namespace ElasticBreath.App.Domain;

public enum ElasticBreathState
{
    Idle,
    Working,
    Paused,
    Resting
}

public enum WorkingPressureLevel
{
    Safe,
    Warning,
    Hard
}

public enum RestPressureLevel
{
    Base,
    Elastic,
    Overtime
}

public enum PendingTransitionKind
{
    IdleToWorking,
    WorkingToPaused,
    PausedToWorking
}
