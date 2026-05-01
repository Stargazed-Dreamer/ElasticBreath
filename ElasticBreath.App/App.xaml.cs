using ElasticBreath.App.Services;

namespace ElasticBreath.App;

public partial class App : System.Windows.Application
{
    public App()
    {
        DispatcherUnhandledException += (_, e) =>
        {
            CrashLogger.Write("DispatcherUnhandledException", e.Exception);
            e.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex)
            {
                CrashLogger.Write("AppDomainUnhandledException", ex);
            }
            else
            {
                CrashLogger.Write("AppDomainUnhandledException", new Exception("Unknown exception object"));
            }
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            CrashLogger.Write("TaskSchedulerUnobservedTaskException", e.Exception);
            e.SetObserved();
        };
    }
}
