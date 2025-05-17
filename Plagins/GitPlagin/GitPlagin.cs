using LibGit2Sharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using Microsoft.SemanticKernel.Embeddings;
using System.ComponentModel;
using Microsoft.SemanticKernel.Memory;


namespace SemanticKernelPlayground.Plagins.GitPlagin
{

	public class GitPlugin
	{
		private static Dictionary<string, string> PluginMemory { get; set; } = new();
		public static IEnumerable<MethodDeclarationSyntax> methods { get; set; }

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

		[KernelFunction("ScanFilesInRepo")]
		public async Task<string> ScanFilesInRepo([Description("Semantic Kernel")] Kernel kernel)
		{
			if (!PluginMemory.TryGetValue("repo path", out var repoPath))
				return "No Git repository path was provided.";

			var chatService = kernel.GetRequiredService<IChatCompletionService>();
			var embeddingGen = kernel.GetRequiredService<ITextEmbeddingGenerationService>();
			var memoryStore = kernel.GetRequiredService<IMemoryStore>();

			int count = 0;

			foreach (var file in Directory.GetFiles(repoPath, "*.cs", SearchOption.AllDirectories))
			{
				var code = File.ReadAllText(file);
				var tree = CSharpSyntaxTree.ParseText(code);
				var root = tree.GetRoot();
				var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();

				foreach (var method in methods)
				{
					var methodCode = method.ToFullString();
					var prompt = $"Describe what method does:\n{methodCode}";
					var chat = new ChatHistory(prompt);

					var response = await chatService.GetChatMessageContentAsync(
						chat,
						new AzureOpenAIPromptExecutionSettings(),
						kernel);

					if (string.IsNullOrWhiteSpace(response?.Content)) continue;

					var embedding = await embeddingGen.GenerateEmbeddingAsync(response.Content);

					var record = MemoryRecord.LocalRecord(
						id: Guid.NewGuid().ToString(),
						text: response.Content,
						embedding: embedding,
						description: method.Identifier.Text,
						additionalMetadata: null
						);

					await memoryStore.UpsertAsync("code-docs", record);

					count++;
				}
			}

			return $"Added {count} method descriptions to memory";
		}


	}
}
