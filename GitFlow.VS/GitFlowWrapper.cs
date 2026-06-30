using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using LibGit2Sharp;

namespace GitFlow.VS
{
    public class GitFlowWrapper
    {
        public delegate void CommandOutputReceivedEventHandler(object sender, CommandOutputEventArgs args);
        public delegate void CommandErrorReceivedEventHandler(object sender, CommandOutputEventArgs args);

        private readonly string repoDirectory;
        public static StringBuilder Output = new StringBuilder("");
        public static StringBuilder Error = new StringBuilder("");
        private const string GitFlowDefaultValueRegExp = @"\[(.*?)\]";

        public event CommandOutputReceivedEventHandler CommandOutputDataReceived;
        public event CommandErrorReceivedEventHandler CommandErrorDataReceived;

        public bool IsOnFeatureBranch
        {
            get
            {
                if (!IsInitialized)
                    return false;

                using (var repo = new Repository(repoDirectory))
                {
                    var featurePrefix = repo.Config.Get<string>("gitflow.prefix.feature");
                    if (featurePrefix == null)
                        return false;
                    return repo.Head.FriendlyName.StartsWith(featurePrefix.Value);
                }
            }
        }

        public bool IsOnBugfixBranch
        {
            get
            {
                if (!IsInitialized)
                    return false;

                using (var repo = new Repository(repoDirectory))
                {
                    var bugfixPrefix = repo.Config.Get<string>("gitflow.prefix.bugfix");
                    if (bugfixPrefix == null)
                        return false;
                    return repo.Head.FriendlyName.StartsWith(bugfixPrefix.Value);
                }
            }
        }

        public bool IsOnHotfixBranch
        {
            get
            {
                if (!IsInitialized)
                    return false;

                using (var repo = new Repository(repoDirectory))
                {
                    var hotfixPrefix = repo.Config.Get<string>("gitflow.prefix.hotfix");
                    if (hotfixPrefix == null)
                        return false;
                    return repo.Head.FriendlyName.StartsWith(hotfixPrefix.Value);
                }
            }
        }

        public bool IsOnMasterBranch
        {
            get
            {
                if (!IsInitialized)
                    return false;

                using (var repo = new Repository(repoDirectory))
                {
                    var masterBranch = repo.Config.Get<string>("gitflow.branch.master");
                    if (masterBranch == null)
                        return false;
                    return repo.Head.FriendlyName == masterBranch.Value;
                }
            }
        }

        public bool IsInitialized
        {
            get
            {
                using (var repo = new Repository(repoDirectory))
                {
                    return repo.Config.Any(c => c.Key.StartsWith("gitflow.branch.master")) && 
						repo.Config.Any(c => c.Key.StartsWith("gitflow.branch.develop"));
                }
            }
        }

        public string CurrentStatus
        {
            get
            {
                string status = "";
                if (IsOnDevelopBranch)
                    status = "Develop: " + CurrentBranchLeafName;
                else if (IsOnFeatureBranch)
                    status = "Feature: " + CurrentBranchLeafName;
                else if (IsOnBugfixBranch)
                    status = "Bugfix: " + CurrentBranchLeafName;
                else if (IsOnHotfixBranch)
                    status = "Hotfix: " + CurrentBranchLeafName;
                else if (IsOnReleaseBranch)
                    status = "Release: " + CurrentBranchLeafName;
                else if (IsOnSupportBranch)
                    status = "Support: " + CurrentBranchLeafName;

                return status;
            }
        }

        public IEnumerable<string> AllFeatures
        {
            get
            {
                return GetAllBranchesThatStartsWithConfigPrefix("gitflow.prefix.feature");
            }
        }

        public IEnumerable<BranchItem> AllFeatureBranches
        {
            get
            {
                if (!IsInitialized)
                    return new List<BranchItem>();

                using (var repo = new Repository(repoDirectory))
                {
                    var prefix = repo.Config.Get<string>("gitflow.prefix.feature").Value;
                    var featureBranches = 
                        repo.Branches.Where(b => !b.IsRemote && b.FriendlyName.StartsWith(prefix) )
                            .Select(c => new BranchItem
                            {
                                Author = c.Tip.Author.Name,
                                Name = c.FriendlyName.Replace(prefix,""),
                                LastCommit = c.Tip.Author.When,
                                IsTracking = c.IsTracking,
                                IsCurrentBranch = c.IsCurrentRepositoryHead,
                                IsRemote = c.IsRemote,
                                CommitId = c.Tip.Id.ToString(),
                                Message = c.Tip.MessageShort
                            }).ToList();

                    var remoteFeatureBranches =
                        repo.Branches.Where(b => b.IsRemote && b.FriendlyName.Contains(prefix)
                        && !repo.Branches.Any(br => !br.IsRemote && br.IsTracking && br.TrackedBranch.CanonicalName== b.CanonicalName))
                            .Select(c => new BranchItem
                            {
                                Author = c.Tip.Author.Name,
                                Name = c.FriendlyName,
                                LastCommit = c.Tip.Author.When,
                                IsTracking = c.IsTracking,
                                IsCurrentBranch = c.IsCurrentRepositoryHead,
                                IsRemote = c.IsRemote,
                                CommitId = c.Tip.Id.ToString(),
                                Message = c.Tip.MessageShort
                            }).ToList();

                    featureBranches.AddRange(remoteFeatureBranches);
                    return featureBranches;
                }   

            }
        }

		public IEnumerable<BranchItem> AllReleaseBranches
		{
			get
			{
				if (!IsInitialized)
					return new List<BranchItem>();

				using (var repo = new Repository(repoDirectory))
				{
					var prefix = repo.Config.Get<string>("gitflow.prefix.release").Value;
					var releaseBranches =
						repo.Branches.Where(b => !b.IsRemote && b.FriendlyName.StartsWith(prefix))
							.Select(c => new BranchItem
							{
								Author = c.Tip.Author.Name,
								Name = c.FriendlyName.Replace(prefix, ""),
								LastCommit = c.Tip.Author.When,
								IsTracking = c.IsTracking,
								IsCurrentBranch = c.IsCurrentRepositoryHead,
								IsRemote = c.IsRemote,
								CommitId = c.Tip.Id.ToString(),
								Message = c.Tip.MessageShort
							}).ToList();

					return releaseBranches;
				}

			}
		}

		public GitFlowCommandResult PublishFeature(string featureName)
        {
            string gitArguments = "feature publish \"" + TrimBranchName(featureName) + "\"";
            return RunGitFlow(gitArguments);
        }

		public GitFlowCommandResult PublishRelease(string releaseName)
		{
			string gitArguments = "release publish \"" + TrimBranchName(releaseName) + "\"";
			return RunGitFlow(gitArguments);
		}

		private string TrimBranchName(string branchName)
        {
            if( branchName.LastIndexOf('/') >= 0)
            {
                branchName = branchName.Substring(branchName.LastIndexOf('/')+1);
            }
            return branchName.Trim().Replace(" ", "-");
        }

        public GitFlowCommandResult TrackFeature(string featureName)
        {
            string gitArguments = "feature track \"" + TrimBranchName(featureName) + "\"";
            return RunGitFlow(gitArguments);
        }

        public GitFlowCommandResult CheckoutFeature(string featureName)
        {
            string gitArguments = "feature checkout \"" + TrimBranchName(featureName) + "\"";
            return RunGitFlow(gitArguments);
        }

        public IEnumerable<string> AllBugfixes
        {
            get
            {
                return GetAllBranchesThatStartsWithConfigPrefix("gitflow.prefix.bugfix");
            }
        }

        public IEnumerable<string> AllReleases
        {
            get
            {
                return GetAllBranchesThatStartsWithConfigPrefix("gitflow.prefix.release");
            }
        }


        public IEnumerable<string> AllHotfixes
        {
            get
            {
                return GetAllBranchesThatStartsWithConfigPrefix("gitflow.prefix.hotfix");
            }
        }

        public IEnumerable<string> GetAllBranchesThatStartsWithConfigPrefix(string config)
        {
            if (!IsInitialized)
                return new List<string>();

            using (var repo = new Repository(repoDirectory))
            {
                var prefix = repo.Config.Get<string>(config).Value;
                var gitFlowBranches =
                    repo.Branches.Where(b => !b.IsRemote && b.FriendlyName.StartsWith(prefix)).ToList();

                return gitFlowBranches.Select(b => b.FriendlyName.Replace(prefix, "")).ToList();
            }   
        }

        public bool IsOnDevelopBranch
        {
            get
            {
                if (!IsInitialized)
                    return false;

                using (var repo = new Repository(repoDirectory))
                {
                    var developBranch = repo.Config.Get<string>("gitflow.branch.develop");
                    if (developBranch == null)
                        return false;
                    return repo.Head.FriendlyName == developBranch.Value;
                }
            }
        }

        public bool IsOnReleaseBranch
        {
            get
            {
                if (!IsInitialized)
                    return false;

                using (var repo = new Repository(repoDirectory))
                {
                    var releasePrefix = repo.Config.Get<string>("gitflow.prefix.release");
                    if (releasePrefix == null)
                        return false;
                    return repo.Head.FriendlyName.StartsWith(releasePrefix.Value);
                }
            }
        }

        public bool IsOnSupportBranch
        {
            get
            {
                if (!IsInitialized)
                    return false;

                using (var repo = new Repository(repoDirectory))
                {
                    var supportPrefix = repo.Config.Get<string>("gitflow.prefix.support");
                    if (supportPrefix == null)
                        return false;
                    return repo.Head.FriendlyName.StartsWith(supportPrefix.Value);
                }
            }
        }

        public IEnumerable<string> AllSupports
        {
            get
            {
                return GetAllBranchesThatStartsWithConfigPrefix("gitflow.prefix.support");
            }
        }

        public IEnumerable<string> AllTags
        {
            get
            {
                using (var repo = new Repository(repoDirectory))
                {
                    return repo.Tags.Select(t => t.FriendlyName).OrderByDescending(t => t).ToList();
                }
            }
        }

        public string CurrentBranch
        {
            get
            {
                using (var repo = new Repository(repoDirectory))
                {
                    return repo.Head.FriendlyName;
                }                
            }
        }

        public string CurrentBranchLeafName
        {
            get
            {
                using (var repo = new Repository(repoDirectory))
                {
                    string fullBranchName = repo.Head.FriendlyName;
                    ConfigurationEntry<string> prefix = null;

                    if (IsOnFeatureBranch)
                    {
                        prefix = repo.Config.Get<string>("gitflow.prefix.feature");
                    }
                    if (IsOnBugfixBranch)
                    {
                        prefix = repo.Config.Get<string>("gitflow.prefix.bugfix");
                    }
                    if (IsOnReleaseBranch)
                    {
                        prefix = repo.Config.Get<string>("gitflow.prefix.release");
                    }
                    if (IsOnHotfixBranch)
                    {
                        prefix = repo.Config.Get<string>("gitflow.prefix.hotfix");
                    }
                    if (IsOnSupportBranch)
                    {
                        prefix = repo.Config.Get<string>("gitflow.prefix.support");
                    }
                    return prefix != null ? fullBranchName.Replace(prefix.Value, "") : fullBranchName;
                }
            }
        }


        protected virtual void OnCommandOutputDataReceived(CommandOutputEventArgs e)
        {
            CommandOutputReceivedEventHandler handler = CommandOutputDataReceived;
            if (handler != null) handler(this, e);
        }

        protected virtual void OnCommandErrorDataReceived(CommandOutputEventArgs e)
        {
            CommandErrorReceivedEventHandler handler = CommandErrorDataReceived;
            if (handler != null) handler(this, e);
        }

        public GitFlowWrapper(string repoDirectory)
        {
            this.repoDirectory = repoDirectory;
        }

        public GitFlowCommandResult StartFeature(string featureName)
        {
            ValidateGitFlowActionName(featureName);
            string gitArguments = "feature start \"" + TrimBranchName(featureName) + "\"";
            return RunGitFlow(gitArguments);
        }

        public GitFlowCommandResult FinishFeature(string featureName, bool rebaseOnDevelopment = false, bool deleteLocalBranch = true, bool deleteRemoteBranch = true, bool squash = false, bool noFastForward = false)
        {
            string gitArguments = "feature finish \"" + TrimBranchName(featureName) + "\"";
            if (rebaseOnDevelopment)
                gitArguments += " -r";
            if (!deleteLocalBranch)
                gitArguments += " --keeplocal";
            if (!deleteRemoteBranch)
                gitArguments += " --keepremote";
            if (squash)
                gitArguments += " --squash";
            if (noFastForward)
                gitArguments += " --no-ff";

            if( squash)
            { 
                //Wait for up to 15 minutes to let the user close the editor for the squashed commit message
                return RunGitFlow(gitArguments, 15*60*1000);
            }
            else
            {
                return RunGitFlow(gitArguments);
            }
        }

        public GitFlowCommandResult StartBugfix(string bugfixName)
        {
            ValidateGitFlowActionName(bugfixName);
            string gitArguments = "bugfix start \"" + TrimBranchName(bugfixName) + "\"";
            return RunGitFlow(gitArguments);
        }

        public GitFlowCommandResult FinishBugfix(string bugfixName, bool rebaseOnDevelopment = false, bool deleteLocalBranch = true, bool deleteRemoteBranch = true, bool squash = false, bool noFastForward = false)
        {
            string gitArguments = "bugfix finish \"" + TrimBranchName(bugfixName) + "\"";
            if (rebaseOnDevelopment)
                gitArguments += " -r";
            if (!deleteLocalBranch)
                gitArguments += " --keeplocal";
            if (!deleteRemoteBranch)
                gitArguments += " --keepremote";
            if (squash)
                gitArguments += " --squash";
            if (noFastForward)
                gitArguments += " --no-ff";

            if (squash)
            {
                return RunGitFlow(gitArguments, 15 * 60 * 1000);
            }
            else
            {
                return RunGitFlow(gitArguments);
            }
        }

        public GitFlowCommandResult StartRelease(string releaseName)
        {
            ValidateGitFlowActionName(releaseName);
            string gitArguments = "release start \"" + TrimBranchName(releaseName) + "\"";
            return RunGitFlow(gitArguments);
        }

        public GitFlowCommandResult FinishRelease(string releaseName, string tagMessage = null, bool deleteBranch = true, bool forceDeletion=false, bool pushChanges = false, bool releaseNoBackMerge = false)
        {
            string gitArguments = "release finish \"" + TrimBranchName(releaseName) + "\"";
            if (!String.IsNullOrEmpty(tagMessage))
            {
                gitArguments += " -m \"" + tagMessage + "\"";
            }
            else
            {
                gitArguments += " -n";
            }
            if (!deleteBranch)
            {
                gitArguments += " -k";
                if (forceDeletion)
                {
                    gitArguments += " -D";
                }
            }
            if (pushChanges)
            {
                gitArguments += " -p";
            }
			if (releaseNoBackMerge)
			{
				gitArguments += " -b";
			}


			return RunGitFlow(gitArguments);
        }

        public GitFlowCommandResult StartHotfix(string hotfixName, string baseBranch = null)
        {
            ValidateGitFlowActionName(hotfixName);
            string safeName = hotfixName.Trim().Replace(" ", "-");
            string gitArguments = "hotfix start \"" + safeName + "\"";
            if (!String.IsNullOrEmpty(baseBranch))
                gitArguments += " \"" + baseBranch.Trim() + "\"";
            return RunGitFlow(gitArguments);
        }

        public GitFlowCommandResult StartSupport(string supportName, string baseBranch)
        {
            ValidateGitFlowActionName(supportName);
            if (String.IsNullOrEmpty(baseBranch))
                throw new ArgumentException("Base tag or commit is required to start a support branch");
            string gitArguments = "support start \"" + TrimBranchName(supportName) + "\" \"" + baseBranch.Trim() + "\"";
            return RunGitFlow(gitArguments);
        }

        public GitFlowCommandResult FinishHotfix(string hotifxName, string tagMessage = null, bool deleteBranch = true, bool forceDeletion = false, bool pushChanges = false)
        {
            string gitArguments = "hotfix finish \"" + hotifxName.Trim() + "\"";
            if (!String.IsNullOrEmpty(tagMessage))
            {
                gitArguments += " -m \"" + tagMessage + "\"";
            }
            else
            {
                gitArguments += " -n";
            }
            if (!deleteBranch)
            {
                gitArguments += " -k";
                if (forceDeletion)
                {
                    gitArguments += " -D";
                }
            }
            if (pushChanges)
            {
                gitArguments += " -p";
            }

            return RunGitFlow(gitArguments);
        }

        private void ValidateGitFlowActionName(string name)
        {
            if( String.IsNullOrEmpty(name))
            {
                throw new ArgumentException("Name cannot be empty");
            }
            if( name.Contains("'"))
            {
                throw new ArgumentException("Name cannot contain single quotes");
            }
        }

        public GitFlowCommandResult Init(GitFlowRepoSettings settings)
        {
            Error = new StringBuilder("");
            Output = new StringBuilder("");

            using (var p = CreateGitFlowProcess("init -f", repoDirectory))
            {
                OnCommandOutputDataReceived(new CommandOutputEventArgs("Running git " + p.StartInfo.Arguments + Environment.NewLine));
                p.Start();
                p.ErrorDataReceived += OnErrorReceived;
                p.BeginErrorReadLine();
                var input = new StringBuilder();

                var sr = p.StandardOutput;
                while (!sr.EndOfStream)
                {
                    var inputChar = (char) sr.Read();
                    input.Append(inputChar);
                    if (StringBuilderEndsWith(input, Environment.NewLine))
                    {
                        Output.AppendLine(input.ToString());
                        OnCommandOutputDataReceived(new CommandOutputEventArgs(input.ToString()));
                        input = new StringBuilder();
                    }
                    if (IsMasterBranchQuery(input.ToString()))
                    {
                        p.StandardInput.Write(settings.MasterBranch + "\n");
                        Output.Append(input);
                        OnCommandOutputDataReceived(new CommandOutputEventArgs(input + Environment.NewLine));
                        input = new StringBuilder();
                    }
                    else if (IsDevelopBranchQuery(input.ToString()))
                    {
                        p.StandardInput.Write(settings.DevelopBranch + "\n");
                        Output.Append(input);
                        OnCommandOutputDataReceived(new CommandOutputEventArgs(input + Environment.NewLine));
                        input = new StringBuilder();
                    }
                    else if (IsFeatureBranchQuery(input.ToString()))
                    {
                        p.StandardInput.Write(settings.FeatureBranch + "\n");
                        Output.Append(input);
                        OnCommandOutputDataReceived(new CommandOutputEventArgs(input + Environment.NewLine));
                        input = new StringBuilder();
                    }
                    else if (IsBugfixBranchQuery(input.ToString()))
                    {
                        p.StandardInput.Write(settings.BugfixBranch + "\n");
                        Output.Append(input);
                        OnCommandOutputDataReceived(new CommandOutputEventArgs(input + Environment.NewLine));
                        input = new StringBuilder();
                    }
                    else if (IsReleaseBranchQuery(input.ToString()))
                    {
                        p.StandardInput.Write(settings.ReleaseBranch + "\n");
                        Output.Append(input);
                        OnCommandOutputDataReceived(new CommandOutputEventArgs(input + Environment.NewLine));
                        input = new StringBuilder();
                    }
                    else if (IsHotfixBranchQuery(input.ToString()))
                    {
                        p.StandardInput.Write(settings.HotfixBranch + "\n");
                        Output.Append(input);
                        OnCommandOutputDataReceived(new CommandOutputEventArgs(input + Environment.NewLine));
                        input = new StringBuilder();
                    }
                    else if (IsSupportBranchQuery(input.ToString()))
                    {
                        p.StandardInput.Write(settings.SupportBranch + "\n");
                        Output.Append(input);
                        OnCommandOutputDataReceived(new CommandOutputEventArgs(input + Environment.NewLine));
                        input = new StringBuilder();
                    }
                    else if (IsVersionTagPrefixQuery(input.ToString()))
                    {
                        p.StandardInput.WriteLine(settings.VersionTag);
                        Output.Append(input);
                        OnCommandOutputDataReceived(new CommandOutputEventArgs(input + Environment.NewLine));
                        input = new StringBuilder();
                    }
                    else if (IsHooksAndFiltersQuery(input.ToString()))
                    {
                        p.StandardInput.WriteLine("");
                        Output.Append(input);
                        OnCommandOutputDataReceived(new CommandOutputEventArgs(input + Environment.NewLine));
                        input = new StringBuilder();
                    }
                }
            }
            if (Error != null && Error.Length > 0)
            {
                return new GitFlowCommandResult(false, Error.ToString());
            }
            return new GitFlowCommandResult(true, Output.ToString());
        }

        private static Process CreateGitFlowProcess(string arguments, string repoDirectory)
        {
            var gitInstallationPath = GitHelper.GetGitInstallationPath();
            string pathToGit = Path.Combine(Path.Combine(gitInstallationPath,"bin\\git.exe"));
            return new Process
            {
                StartInfo =
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    FileName = pathToGit,
                    Arguments = "flow " + arguments,
                    WorkingDirectory = repoDirectory
                }
            };
        }


        private void OnOutputDataReceived(object sender, DataReceivedEventArgs dataReceivedEventArgs)
        {
            if (dataReceivedEventArgs.Data != null)
            {
                Output.Append(dataReceivedEventArgs.Data);
                Debug.WriteLine(dataReceivedEventArgs.Data);
                OnCommandOutputDataReceived(new CommandOutputEventArgs(dataReceivedEventArgs.Data + Environment.NewLine));
            }
        }
        private void OnErrorReceived(object sender, DataReceivedEventArgs dataReceivedEventArgs)
        {
            if (dataReceivedEventArgs.Data != null && dataReceivedEventArgs.Data.StartsWith("fatal:", StringComparison.OrdinalIgnoreCase))
            {
                Error = new StringBuilder();
                Error.Append(dataReceivedEventArgs.Data);
                Debug.WriteLine(dataReceivedEventArgs.Data);
                OnCommandErrorDataReceived(new CommandOutputEventArgs(dataReceivedEventArgs.Data + Environment.NewLine));
            }
        }

        public bool IsMasterBranchQuery(string input)
        {
            var regex = new Regex(@"Branch name for production releases: " + GitFlowDefaultValueRegExp);
            return MatchInput(input, regex);
        }

        public bool IsDevelopBranchQuery(string input)
        {
            var regex = new Regex(@"Branch name for ""next release"" development: " + GitFlowDefaultValueRegExp);
            return MatchInput(input, regex);
        }

        public bool IsFeatureBranchQuery(string input)
        {
            var regex = new Regex(@"Feature branches\? " + GitFlowDefaultValueRegExp);
            return MatchInput(input, regex);
        }

        public bool IsBugfixBranchQuery(string input)
        {
            var regex = new Regex(@"Bugfix branches\? " + GitFlowDefaultValueRegExp);
            return MatchInput(input, regex);
        }

        public bool IsReleaseBranchQuery(string input)
        {
            var regex = new Regex(@"Release branches\? " + GitFlowDefaultValueRegExp);
            return MatchInput(input, regex);
        }
        public bool IsHotfixBranchQuery(string input)
        {
            var regex = new Regex(@"Hotfix branches\? " + GitFlowDefaultValueRegExp);
            return MatchInput(input, regex);
        }

        public bool IsSupportBranchQuery(string input)
        {
            var regex = new Regex(@"Support branches\? "  + GitFlowDefaultValueRegExp);
            return MatchInput(input, regex);
        }

        public bool IsVersionTagPrefixQuery(string input)
        {
            var regex = new Regex(@"Version tag prefix\? " + GitFlowDefaultValueRegExp);
            return MatchInput(input, regex);
        }

        public bool IsHooksAndFiltersQuery(string input)
        {
            var regex = new Regex(@"Hooks and filters directory\? " + GitFlowDefaultValueRegExp);
            return MatchInput(input, regex);
        }

        private static bool MatchInput(string input, Regex regex)
        {
            var match = regex.Match(input);
            if (match.Success)
            {
                return true;
            }
            return false;
        }

        private static bool StringBuilderEndsWith(StringBuilder haystack, string needle)
        {
            if (haystack.Length == 0)
                return false;

            var needleLength = needle.Length - 1;
            var haystackLength = haystack.Length - 1;
            for (var i = 0; i < needleLength; i++)
            {
                if (haystack[haystackLength - i] != needle[needleLength - i])
                {
                    return false;
                }
            }
            return true;
        }

        private GitFlowCommandResult RunGitFlow(string gitArguments, int timeout = 15000)
        {
            Error = new StringBuilder("");
            Output = new StringBuilder("");

            using (var p = CreateGitFlowProcess(gitArguments, repoDirectory))
            {
                OnCommandOutputDataReceived(new CommandOutputEventArgs("Running git " + p.StartInfo.Arguments + "\n"));
                p.Start();
                p.ErrorDataReceived += OnErrorReceived;
                p.OutputDataReceived += OnOutputDataReceived;
                p.BeginErrorReadLine();
                p.BeginOutputReadLine();
                p.WaitForExit(timeout);
                if (!p.HasExited)
                {
                    OnCommandOutputDataReceived(new CommandOutputEventArgs("The command is taking longer than expected\n"));

                    p.WaitForExit(timeout);
                    if (!p.HasExited)
                    {
                        return new GitFlowTimedOutCommandResult("git " + p.StartInfo.Arguments);
                    }
                }
                if (Error != null && Error.Length > 0)
                {
                    return new GitFlowCommandResult(false, Error.ToString());
                }
                return new GitFlowCommandResult(true, Output.ToString());
            }
        }
    }
}
