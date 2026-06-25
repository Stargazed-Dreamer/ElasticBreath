namespace ElasticBreath.App.Domain;

/// <summary>
/// 表示弹性呼吸或相关流程的运行状态。
/// </summary>
public enum ElasticBreathState
{
    /// <summary>
    /// 空闲状态，表示当前未运行任何流程。
    /// </summary>
    Idle,

    /// <summary>
    /// 工作状态，表示流程正在运行中。
    /// </summary>
    Working,

    /// <summary>
    /// 暂停状态，表示流程被用户或系统临时中断。
    /// </summary>
    Paused,

    /// <summary>
    /// 休息状态，表示流程在主动运行后进入的自然恢复阶段。
    /// </summary>
    Resting
}

/// <summary>
/// 表示工作压力级别的枚举，用于标识设备或系统的压力状态。
/// </summary>
public enum WorkingPressureLevel
{
    Safe, // 安全级别，表示压力在正常范围内
    Warning, // 警告级别，表示压力接近危险阈值
    Hard // 硬级别，可能表示高压或紧急状态
}

/// <summary>
/// 表示休息压力的等级。
/// </summary>
public enum RestPressureLevel
{
    Base,
    Elastic,
    Overtime
}

/// <summary>
/// 定义了各种待处理的状态转换类型，用于系统状态管理。
/// </summary>
public enum PendingTransitionKind
{
    IdleToWorking, // 从空闲状态转换到工作状态
    WorkingToPaused, // 从工作状态转换到暂停状态
    PausedToWorking, // 从暂停状态转换回工作状态
    WorkingToResting, // 从工作状态转换到休息状态
    RestingToWorking // 从休息状态转换回工作状态
}
