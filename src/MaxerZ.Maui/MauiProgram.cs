#if MACCATALYST
using UIKit;
using Foundation;
using System.IO;
#endif

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MaxerZ.Maui;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		MaxerZ.Api.Services.FileSaveDialogHelper.SaveFileDialogAsync = async (defaultName, bytes) =>
		{
			var tcs = new TaskCompletionSource<string?>();

			Microsoft.Maui.ApplicationModel.MainThread.BeginInvokeOnMainThread(() =>
			{
				try
				{
#if MACCATALYST
					var tempPath = Path.Combine(Path.GetTempPath(), defaultName);
					File.WriteAllBytes(tempPath, bytes);

					var url = NSUrl.FromFilename(tempPath);
					var picker = new UIDocumentPickerViewController(new[] { url }, UIDocumentPickerMode.ExportToService);

					picker.WasCancelled += (sender, args) =>
					{
						tcs.TrySetResult(null);
					};

					picker.DidPickDocumentAtUrls += (sender, args) =>
					{
						if (args.Urls.Length > 0)
						{
							tcs.TrySetResult(args.Urls[0].Path);
						}
						else
						{
							tcs.TrySetResult(null);
						}
					};

					var rootVC = Microsoft.Maui.ApplicationModel.Platform.GetCurrentUIViewController()
						?? UIApplication.SharedApplication.KeyWindow?.RootViewController;

					if (rootVC != null)
					{
						var topVC = rootVC;
						while (topVC.PresentedViewController != null)
						{
							topVC = topVC.PresentedViewController;
						}
						topVC.PresentViewController(picker, true, null);
					}
					else
					{
						tcs.TrySetResult(null);
					}
#else
					tcs.TrySetResult(null);
#endif
				}
				catch (Exception ex)
				{
					Console.Error.WriteLine($"Error showing Save File Dialog: {ex}");
					tcs.TrySetResult(null);
				}
			});

			return await tcs.Task;
		};

		// Start the embedded API in a background thread on app launch
		Task.Run(() =>
		{
			try
			{
				MaxerZ.Api.Program.Main(Array.Empty<string>());
			}
			catch (Exception ex)
			{
				Console.Error.WriteLine($"API startup failed: {ex.Message}");
			}
		});

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
