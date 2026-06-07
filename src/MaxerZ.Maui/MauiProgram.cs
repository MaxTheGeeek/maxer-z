using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MaxerZ.Maui;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
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
