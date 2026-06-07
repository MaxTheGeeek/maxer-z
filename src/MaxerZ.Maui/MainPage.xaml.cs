using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.ApplicationModel;

namespace MaxerZ.Maui;

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();
        _ = LoadAppAsync();
    }

    private async Task LoadAppAsync()
    {
        var portFilePath = Path.Combine(Path.GetTempPath(), "maxerz_port.txt");
        var port = 0;
        
        // Wait up to 10 seconds for the port handshake file to exist
        for (int i = 0; i < 20; i++)
        {
            if (File.Exists(portFilePath))
            {
                try
                {
                    var content = await File.ReadAllTextAsync(portFilePath);
                    if (int.TryParse(content.Trim(), out var p) && p > 0)
                    {
                        port = p;
                        break;
                    }
                }
                catch
                {
                    // File might be locked by write operation, wait and try again
                }
            }
            await Task.Delay(500);
        }

        string targetUrl;
        if (port > 0)
        {
            targetUrl = $"http://localhost:{port}";
        }
        else
        {
            // Fallback for development/production missing state
            targetUrl = "http://localhost:4200";
        }

        System.Diagnostics.Debug.WriteLine($"Loading Web UI: {targetUrl}");
        MainThread.BeginInvokeOnMainThread(() =>
        {
            MainWebView.Source = targetUrl;
        });
    }
}
