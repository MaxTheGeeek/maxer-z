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
        var port = 0;
        
        // Wait up to 12 seconds for the active port to be initialized in-process
        for (int i = 0; i < 24; i++)
        {
            if (MaxerZ.Api.Program.ActivePort > 0)
            {
                port = MaxerZ.Api.Program.ActivePort;
                break;
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
