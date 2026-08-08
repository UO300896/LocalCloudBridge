namespace LocalCloudBridge.Mobile.Services;

public static class ProxyNotificationService
{
    public const int NotificationId = 1001;
    public const string ChannelId = "localcloudbridge_proxy_channel";
    public const string ActionStopProxy = "ACTION_STOP_PROXY";

    public static event Action? OnStopRequested;

    public static void HandleIntent(string? action)
    {
        if (action == ActionStopProxy)
        {
            OnStopRequested?.Invoke();
        }
    }

    public static void ShowNotification(string listenUrl, string targetUrl)
    {
#if ANDROID
        try
        {
            var context = Android.App.Application.Context;
            var notificationManager = (Android.App.NotificationManager?)context.GetSystemService(Android.Content.Context.NotificationService);
            if (notificationManager == null) return;

            if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.O)
            {
#pragma warning disable CA1416
                var channel = new Android.App.NotificationChannel(
                    ChannelId,
                    "Proxy Service Notifications",
                    Android.App.NotificationImportance.Low)
                {
                    Description = "Active notifications when LocalCloudBridge proxy is running"
                };
                notificationManager.CreateNotificationChannel(channel);
#pragma warning restore CA1416
            }

            var stopIntent = new Android.Content.Intent(context, typeof(MainActivity));
            stopIntent.SetAction(ActionStopProxy);
            stopIntent.AddFlags(Android.Content.ActivityFlags.SingleTop | Android.Content.ActivityFlags.ClearTop);

#pragma warning disable CA1416
            var pendingFlags = Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.M
                ? Android.App.PendingIntentFlags.UpdateCurrent | Android.App.PendingIntentFlags.Immutable
                : Android.App.PendingIntentFlags.UpdateCurrent;
#pragma warning restore CA1416

            var pendingIntent = Android.App.PendingIntent.GetActivity(
                context,
                NotificationId,
                stopIntent,
                pendingFlags);

            int iconResId = context.Resources?.GetIdentifier("fav", "mipmap", context.PackageName) ?? 0;
            if (iconResId == 0)
            {
                iconResId = context.Resources?.GetIdentifier("appicon", "mipmap", context.PackageName) ?? 0;
            }
            if (iconResId == 0)
            {
                iconResId = Android.Resource.Drawable.IcDialogInfo;
            }

            var builder = new AndroidX.Core.App.NotificationCompat.Builder(context, ChannelId)
                ?.SetSmallIcon(iconResId)
                ?.SetContentTitle("LocalCloudBridge Proxy Active")
                ?.SetContentText($"Listening on {listenUrl} -> {targetUrl}. Tap to stop.")
                ?.SetOngoing(true)
                ?.SetAutoCancel(false)
                ?.SetContentIntent(pendingIntent)
                ?.AddAction(Android.Resource.Drawable.IcMenuCloseClearCancel, "Stop Proxy", pendingIntent);

            if (iconResId != 0 && context.Resources != null)
            {
                try
                {
                    var bitmap = Android.Graphics.BitmapFactory.DecodeResource(context.Resources, iconResId);
                    if (bitmap != null)
                    {
                        builder?.SetLargeIcon(bitmap);
                    }
                }
                catch { }
            }

            if (builder != null)
            {
                var notification = builder.Build();
                if (notification != null)
                {
                    notificationManager.Notify(NotificationId, notification);
                }
            }
        }
        catch { }
#endif
    }

    public static void HideNotification()
    {
#if ANDROID
        try
        {
            var context = Android.App.Application.Context;
            var notificationManager = (Android.App.NotificationManager?)context.GetSystemService(Android.Content.Context.NotificationService);
            notificationManager?.Cancel(NotificationId);
        }
        catch { }
#endif
    }
}
