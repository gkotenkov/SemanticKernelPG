using LibGit2Sharp;
using Microsoft.SemanticKernel;

namespace SemanticKernelPlayground.Plagins.GitPlagin
{

	public class GitPlugin
	{
		private static Dictionary<string, string> PluginMemory { get; set; } = new();

		[KernelFunction("RememberRepo")]
		public string RememberRepo(string repoPath)
		{
			if (!Repository.IsValid(repoPath))
				return "Invalid Git repository path.";
			try
			{
				if (PluginMemory.ContainsKey("repo path")) PluginMemory["repo path"] = repoPath;
				else PluginMemory.Add("repo path", repoPath);
				
				return "Successpully remembered path to repo";
			}
			catch(Exception ex)
			{
				return $"Failed: {ex.Message}";
			}
		}


		[KernelFunction("RememberUserCredentials")]
		public string RememberUserCreds(string userEmail, string userName)
		{
			try
			{
				if (userEmail == null || userEmail.Length == 0)
					return "Invalid user email.";

				if (userName == null || userName.Length == 0)
					return "Invalid user name.";

				if (PluginMemory.ContainsKey("user name")) PluginMemory["user name"] = userName;
				PluginMemory.Add("user name", userName);

				if (PluginMemory.ContainsKey("user email")) PluginMemory["user email"] = userEmail;
				PluginMemory.Add("user email", userEmail);
				
				return "Successpully remembered user credentials";
			}
			catch (Exception ex)
			{
				return $"Failed: {ex.Message}";
			}
		}


		[KernelFunction("ListCommits")]
		public string GenerateReleaseNotes()
		{
			try
			{
				if (!PluginMemory.ContainsKey("repo path")) return "no Git repository path was provided.";

				using var repo = new Repository(PluginMemory["repo path"]);
				var commits = repo.Commits
					.Take(10)
					.Select(c => $"- {c.MessageShort} ({c.Author.Name}, {c.Author.When.LocalDateTime})");

				return string.Join("\n", commits);
			}
			catch(Exception ex)
			{
				return $"Failed: {ex.Message}";
			}
		}

		[KernelFunction("MakeACommit")]
		public string MakeACommit(string message)
		{
			try
			{
				if (!PluginMemory.ContainsKey("repo path")) return "no Git repository path was provided.";

				if (message == null || message.Length == 0)
					return "Invalid commit message.";

				if (!PluginMemory.ContainsKey("user email")) 
					return "Invalid user email.";

				if (!PluginMemory.ContainsKey("user name"))
					return "Invalid user name.";

				var gitUser = new Signature(PluginMemory["user name"], PluginMemory["user email"], DateTime.Now);

				using var repo = new Repository(PluginMemory["repo path"]);
					var staged = repo.RetrieveStatus()
					.Where(s => s.State != FileStatus.Ignored)
					.Select(s => s.FilePath)
					.ToList();

				Commands.Stage(repo, staged);

				if (!repo.RetrieveStatus().Any(s => s.State != FileStatus.Ignored))
					return "No changes to commit.";

				repo.Commit(message, gitUser, gitUser);


				return $"Commit created with {staged.Count} files: {string.Join(", ", staged)}";
			}
			catch (Exception ex)
			{
				return $"Commit failed: {ex.Message}";
			}

		}

	}
}
