using Microsoft.UI.Xaml;
using System;
using System.Diagnostics;
using System.IO;
using zavod.Bootstrap;
using zavod.Diagnostics;
using zavod.Demo;
using zavod.Persistence;
using zavod.UI.Shell.Verification;
using zavod.UI.Text;

namespace zavod
{
    public partial class App : Application
    {
        private Window? _window;

        public App()
        {
            InitializeComponent();
            RootCauseTrace.Mark("app_ctor");
            UnhandledException += App_UnhandledException;
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            RootCauseTrace.Mark("on_launched_start");
            var repositoryRoot = ProjectRootResolver.Resolve();
            var isDemoMode = DemoMode.IsEnabled(repositoryRoot);
            var projectRoot = DemoMode.ResolveRuntimeProjectRoot(repositoryRoot);
            RootCauseTrace.Initialize(projectRoot);
            RootCauseTrace.Mark("root_trace_initialized", projectRoot);
            ProjectBootstrap.Initialize(projectRoot);
            UiVerificationSeedSupport.TryApplyAtStartup(projectRoot);
            AppText.Use(UiLanguageResolver.Resolve());
            RootCauseTrace.Mark("bootstrap_complete");

            _window = new MainWindow(projectRoot, isDemoMode);
            RootCauseTrace.Mark("main_window_created");
            _window.Activate();
            RootCauseTrace.Mark("window_activated");
        }

        private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            try
            {
                var repositoryRoot = ProjectRootResolver.Resolve();
                var projectRoot = DemoMode.ResolveRuntimeProjectRoot(repositoryRoot);
                var logDirectory = Path.Combine(projectRoot, ".zavod", "logs");
                Directory.CreateDirectory(logDirectory);
                var logPath = Path.Combine(logDirectory, "ui-unhandled-exception.log");
                var lines = new[]
                {
                    $"[{DateTimeOffset.Now:O}] Unhandled UI exception",
                    e.Exception?.ToString() ?? "<no exception>",
                    string.Empty
                };
                File.AppendAllLines(logPath, lines);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to persist UI exception log: {ex}");
            }
        }
    }
}
