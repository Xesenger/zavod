using Microsoft.UI.Xaml;
using zavod.Bootstrap;
using zavod.Demo;
using zavod.Persistence;

namespace zavod
{
    public partial class App : Application
    {
        private Window? _window;

        public App()
        {
            InitializeComponent();
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            var repositoryRoot = ProjectRootResolver.Resolve();
            var isDemoMode = DemoMode.IsEnabled(repositoryRoot);
            var projectRoot = DemoMode.ResolveRuntimeProjectRoot(repositoryRoot);
            ProjectBootstrap.Initialize(projectRoot);

            _window = new MainWindow(projectRoot, isDemoMode);
            _window.Activate();
        }
    }
}
