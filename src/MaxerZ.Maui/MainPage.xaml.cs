using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.ApplicationModel;

#if IOS || MACCATALYST
using UIKit;
using Foundation;
using WebKit;
#endif

namespace MaxerZ.Maui;

#if IOS || MACCATALYST
internal class CustomNavigationDelegate : WKNavigationDelegate
{
    private readonly Action _onProcessTerminated;
    public CustomNavigationDelegate(Action onProcessTerminated)
    {
        _onProcessTerminated = onProcessTerminated;
    }

    public override void ContentProcessDidTerminate(WKWebView webView)
    {
        System.Diagnostics.Debug.WriteLine("WKWebView WebContent process terminated. Automatically recovering...");
        _onProcessTerminated?.Invoke();
    }
}
#endif

public partial class MainPage : ContentPage
{
    private string _lastLoadedUrl = "";
#if IOS || MACCATALYST
    private NSObject? _activeObserverToken;
#endif

    public MainPage()
    {
        InitializeComponent();
        _ = LoadAppAsync();
        StartWebViewHealthCheck();
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
#if IOS || MACCATALYST
        if (MainWebView.Handler?.PlatformView is WKWebView wkWebView)
        {
            wkWebView.NavigationDelegate = new CustomNavigationDelegate(RecoverWebView);
        }
#endif
    }

    private void RecoverWebView()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (!string.IsNullOrEmpty(_lastLoadedUrl))
            {
                System.Diagnostics.Debug.WriteLine($"Recovering WebView by re-setting Source: {_lastLoadedUrl}");
                MainWebView.Source = null;
                MainWebView.Source = _lastLoadedUrl;
            }
        });
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

        _lastLoadedUrl = targetUrl;
        System.Diagnostics.Debug.WriteLine($"Loading Web UI: {targetUrl}");
        MainThread.BeginInvokeOnMainThread(() =>
        {
            MainWebView.Source = targetUrl;
        });
    }

    private void StartWebViewHealthCheck()
    {
        // Start a background loop to verify webview responsiveness every 30 seconds
        Task.Run(async () =>
        {
            while (true)
            {
                await Task.Delay(TimeSpan.FromSeconds(30));
                try
                {
                    await MainThread.InvokeOnMainThreadAsync(async () =>
                    {
                        if (MainWebView.Source != null && !string.IsNullOrEmpty(_lastLoadedUrl))
                        {
                            try
                            {
                                var test = await MainWebView.EvaluateJavaScriptAsync("1+1");
                                var cleanTest = test?.Trim()?.Trim('"');
                                if (cleanTest != "2")
                                {
                                    System.Diagnostics.Debug.WriteLine($"WebView health check failed (got '{test}'). Recovering.");
                                    RecoverWebView();
                                }
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"WebView health check threw exception: {ex.Message}. Recovering.");
                                RecoverWebView();
                            }
                        }
                    });
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Health check loop encountered error: {ex.Message}");
                }
            }
        });
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
#if IOS || MACCATALYST
        _activeObserverToken = NSNotificationCenter.DefaultCenter.AddObserver(
            UIApplication.DidBecomeActiveNotification,
            OnAppDidBecomeActive);
#endif
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
#if IOS || MACCATALYST
        if (_activeObserverToken != null)
        {
            NSNotificationCenter.DefaultCenter.RemoveObserver(_activeObserverToken);
            _activeObserverToken = null;
        }
#endif
    }

#if IOS || MACCATALYST
    private void OnAppDidBecomeActive(NSNotification notification)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            if (MainWebView.Source != null && !string.IsNullOrEmpty(_lastLoadedUrl))
            {
                try
                {
                    var test = await MainWebView.EvaluateJavaScriptAsync("1+1");
                    var cleanTest = test?.Trim()?.Trim('"');
                    if (cleanTest != "2")
                    {
                        System.Diagnostics.Debug.WriteLine("App active focus: WebView health check failed. Recovering.");
                        RecoverWebView();
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"App active focus: WebView exception {ex.Message}. Recovering.");
                    RecoverWebView();
                }
            }
        });
    }
#endif
}

