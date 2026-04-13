using Microsoft.UI.Xaml;
using System;
using System.Diagnostics;
using System.IO;
using zavod.Bootstrap;
using zavod.Demo;
using zavod.Persistence;
using zavod.UI.Shell.Verification;

namespace zavod
{
    public partial class App : Application
    {
        private Window? _window;

        public App()
        {
            InitializeComponent();
            UnhandledException += App_UnhandledException;
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            var repositoryRoot = ProjectRootResolver.Resolve();
            var isDemoMode = DemoMode.IsEnabled(repositoryRoot);
            var projectRoot = DemoMode.ResolveRuntimeProjectRoot(repositoryRoot);
            ProjectBootstrap.Initialize(projectRoot);
            UiVerificationSeedSupport.TryApplyAtStartup(projectRoot);

            _window = new MainWindow(projectRoot, isDemoMode);
            _window.Activate();
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
