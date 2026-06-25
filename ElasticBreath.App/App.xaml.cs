using ElasticBreath.App.Services;

namespace ElasticBreath.App;

/// <summary>
/// App类，负责WPF应用程序的异常处理，记录未捕获的异常以防止应用程序崩溃。
/// </summary>
public partial class App : System.Windows.Application
{
/// <summary>
    /// 初始化 App 类的新实例，并设置全局异常处理程序。
    /// </summary>
    public App()
    {
        // 订阅DispatcherUnhandledException事件，捕获UI线程的未处理异常
        DispatcherUnhandledException += (_, e) =>
        {
            // 记录异常信息到CrashLogger
            CrashLogger.Write("DispatcherUnhandledException", e.Exception);
            // 标记异常为已处理，避免应用程序终止
            e.Handled = true;
        };

        // 订阅AppDomain.CurrentDomain.UnhandledException事件，捕获应用程序域的未处理异常
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            // 检查异常对象是否为Exception类型
            if (e.ExceptionObject is Exception ex)
            {
                // 如果是，记录异常
                CrashLogger.Write("AppDomainUnhandledException", ex);
            }
            else
            {
                // 否则，记录一个未知异常对象
                CrashLogger.Write("AppDomainUnhandledException", new Exception("Unknown exception object"));
            }
        };

        // 订阅TaskScheduler.UnobservedTaskException事件，捕获任务调度器的未观察异常
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            // 记录异常信息
            CrashLogger.Write("TaskSchedulerUnobservedTaskException", e.Exception);
            // 设置异常为已观察，防止未处理异常影响程序
            e.SetObserved();
        };
    }
}
