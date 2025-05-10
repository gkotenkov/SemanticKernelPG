using LibGit2Sharp;
using Microsoft.SemanticKernel;

namespace SemanticKernelPlayground.Plagins.GitPlagin
{

	public class GitPlugin
	{
		private static Dictionary<string, string> memory { get; set; } = new();

		[KernelFunction("RememberRepo")]
		public string RememberRepo(string repoPath)
		{
			if (!Repository.IsValid(repoPath))
				return "Invalid Git repository path.";
			try
			{
				memory.Add("repo path", repoPath);
				return "Successpully remembered path to repo";
			}
			catch(Exception ex)
			{
				return $"Failed: {ex.Message}";
			}
		}

		[KernelFunction("GenerateReleaseNotes")]
		public string GenerateReleaseNotes()
		{
			try
			{
				if (!memory.ContainsKey("repo path")) return "no Git repository path was provided.";

				using var repo = new Repository(memory["repo path"]);
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
		public string MakeACommit(string message, string userEmail, string userName)
		{
			try
			{
				if (!memory.ContainsKey("repo path")) return "no Git repository path was provided.";

			if (message == null || message.Length == 0)
				return "Invalid commit message.";

			if (userEmail == null || userEmail.Length == 0)
				return "Invalid user email.";

			if (userName == null || userName.Length == 0)
				return "Invalid user name.";

			var gitUser = new Signature(userName, userEmail, DateTime.Now);

			using var repo = new Repository(memory["repo path"]);
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
