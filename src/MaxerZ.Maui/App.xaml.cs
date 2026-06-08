using Microsoft.Extensions.DependencyInjection;

namespace MaxerZ.Maui;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		var window = new Window(new AppShell());

		// Set default size
		window.Width = 1200;
		window.Height = 800;

		// Set minimum size constraints
		window.MinimumWidth = 1000;
		window.MinimumHeight = 700;

		return window;
	}
}