using System;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Reflection;
using GitFlow.VS;
using GitFlowVS.Extension.UI;
using Microsoft.TeamFoundation.Controls;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TeamFoundation.Git.Extensibility;
using Microsoft.VisualStudio.Threading;
using TeamExplorer.Common;

namespace GitFlowVS.Extension
{
    [TeamExplorerPage(GuidList.GitFlowPage, Undockable = true)]
    public class GitFlowPage : TeamExplorerBasePage
    {
        private static IGitExt gitService;
        private static ITeamExplorer teamExplorer;
        private static IVsOutputWindowPane outputWindow;
        private GitFlowPageUI ui;

        public static IGitRepositoryInfo ActiveRepo
        {
            get
            {
                try
                {
                    return gitService?.ActiveRepositories?.FirstOrDefault();
                }
                catch (Exception ex)
                {
                    Logger.Exception(ex);
                    return null;
                }
            }
        }

        public static IVsOutputWindowPane OutputWindow
        {
            get { return outputWindow; }
        }

        public static string ActiveRepoPath
        {
            get { return ActiveRepo?.RepositoryPath; }
        }

        public override void Refresh()
        {
            ITeamExplorerSection[] teamExplorerSections = this.GetSections();
            foreach (var section in teamExplorerSections.Where(s => s is IGitFlowSection))
            {
                ITeamExplorerSection section1 = section;
#pragma warning disable VSSDK007
                ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    ((IGitFlowSection)section1).UpdateVisibleState();
                }).FileAndForget("gitflow/refresh");
#pragma warning restore VSSDK007
            }
            ui.Refresh();
        }

        [ImportingConstructor]
        public GitFlowPage([Import(typeof(SVsServiceProvider))] IServiceProvider serviceProvider)
        {
            Title = "GitFlow";
            gitService = serviceProvider.GetService(typeof(IGitExt)) as IGitExt;
            teamExplorer = serviceProvider.GetService(typeof(ITeamExplorer)) as ITeamExplorer;

            if (gitService != null)
            {
                gitService.PropertyChanged += OnGitServicePropertyChanged;
            }

            #pragma warning disable VSSDK007
            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                var outWindow = Package.GetGlobalService(typeof(SVsOutputWindow)) as IVsOutputWindow;
                if (outWindow != null)
                {
                    var customGuid = new Guid("B85225F6-B15E-4A8A-AF6E-2BE96A4FE672");
                    outWindow.CreatePane(ref customGuid, "GitFlow.VS", 1, 1);
                    outWindow.GetPane(ref customGuid, out outputWindow);
                }
            }).FileAndForget("gitflow/outputwindow");
#pragma warning restore VSSDK007

            ui = new GitFlowPageUI();
            PageContent = ui;
        }

        private void OnGitServicePropertyChanged(object sender, PropertyChangedEventArgs propertyChangedEventArgs)
        {
            Refresh();
        }

        public static void ActiveOutputWindow()
        {
            ThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                OutputWindow?.Activate();
            });
        }

        public static bool GitFlowIsInstalled
        {
            get
            {
                //Read PATH to find git installation path
                //Check if extension has been configured
                string binariesPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Dependencies\\binaries");
                if (!Directory.Exists(binariesPath))
                    return false;

                var gitBinPath = GitHelper.GetGitBinPath();
                if (gitBinPath == null)
                    return false;

                string gitFlowFile = Path.Combine(gitBinPath,"git-flow");
                if (!File.Exists(gitFlowFile))
                    return false;
                return true;
            }
        }

        public static void ShowPage(string page)
        {
#pragma warning disable VSSDK007
            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                teamExplorer?.NavigateToPage(new Guid(page), null);
            }).FileAndForget("gitflow/navigation");
#pragma warning restore VSSDK007
        }
    }
}
