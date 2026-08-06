using Microsoft.Extensions.Logging;

namespace LocalCloudBridge.Mobile;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
#if ANDROID
		LocalCloudBridge.Services.WakeOnLan.AcquireMulticastLock = () =>
		{
			var context = Android.App.Application.Context;
			var wifiManager = (Android.Net.Wifi.WifiManager?)context.GetSystemService(Android.Content.Context.WifiService);
			var mLock = wifiManager?.CreateMulticastLock("LocalCloudBridgeWolLock");
			mLock?.Acquire();
			return new LockReleaser(mLock);
		};
#endif

		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}

#if ANDROID
internal sealed class LockReleaser : IDisposable
{
	private readonly Android.Net.Wifi.WifiManager.MulticastLock? _lock;
	public LockReleaser(Android.Net.Wifi.WifiManager.MulticastLock? mLock) => _lock = mLock;
	public void Dispose() => _lock?.Release();
}
#endif
