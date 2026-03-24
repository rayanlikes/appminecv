using CVGenerator.Views;

namespace CVGenerator;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();

#if ANDROID
        AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
        {
            if (args.ExceptionObject is Exception ex)
            {
                Platforms.Android.CrashLogger.LogCrash(ex, "AppDomain.UnhandledException");
            }
        };

        TaskScheduler.UnobservedTaskException += (sender, args) =>
        {
            Platforms.Android.CrashLogger.LogCrash(args.Exception, "TaskScheduler.UnobservedTaskException");
            args.SetObserved();
        };
#endif
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new AppShell());
    }
}
