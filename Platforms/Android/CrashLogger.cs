#if ANDROID
using Android.App;
using Android.Content;
using Android.OS;
using System.Text;

namespace CVGenerator.Platforms.Android;

public static class CrashLogger
{
    public static void LogCrash(Exception ex, string source)
    {
        try
        {
            var context = Application.Context;
            
            // Get the app-specific external files directory (no permissions required on Android 10+)
            var dir = context.GetExternalFilesDir(null);
            if (dir == null) return;
            
            var logPath = Path.Combine(dir.AbsolutePath, "crash.log");
            
            var sb = new StringBuilder();
            sb.AppendLine($"--- CRASH AT {DateTime.Now:O} from {source} ---");
            sb.AppendLine($"Message: {ex.Message}");
            sb.AppendLine($"Type: {ex.GetType().FullName}");
            sb.AppendLine($"Stack Trace:\n{ex.StackTrace}");
            if (ex.InnerException != null)
            {
                sb.AppendLine($"\n--- Inner Exception ---");
                sb.AppendLine($"Message: {ex.InnerException.Message}");
                sb.AppendLine($"Stack Trace:\n{ex.InnerException.StackTrace}");
            }
            sb.AppendLine("--------------------------------------------------");
            sb.AppendLine();
            
            File.AppendAllText(logPath, sb.ToString());
            
            // Try to show a toast message right before dying
            new Handler(Looper.MainLooper!).Post(() =>
            {
                Android.Widget.Toast.MakeText(context, $"FATAL CRASH: {ex.Message}. See crash.log", Android.Widget.ToastLength.Long)!.Show();
            });
            
            // Give the toast a tiny bit of time to render
            Thread.Sleep(1000);
        }
        catch
        {
            // If the logger itself crashes, we can't do much
        }
    }
}
#endif
