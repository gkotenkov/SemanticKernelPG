using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SemanticKernelPlayground
{
	using Microsoft.SemanticKernel;
	using Microsoft.SemanticKernel.Embeddings;
	using Microsoft.SemanticKernel.Memory;
	using System.Collections.Concurrent;
	using System.Runtime.CompilerServices;
	using System.Threading;

	public class MyMemoryStore : IMemoryStore
	{
		private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, MemoryRecord>> _collections = new();

		public Task CreateCollectionAsync(string collectionName, CancellationToken cancellationToken = default)
		{
			_collections.TryAdd(collectionName, new ConcurrentDictionary<string, MemoryRecord>());
			return Task.CompletedTask;
		}

		public Task DeleteCollectionAsync(string collectionName, CancellationToken cancellationToken = default)
		{
			_collections.TryRemove(collectionName, out _);
			return Task.CompletedTask;
		}

		public Task<bool> DoesCollectionExistAsync(string collectionName, CancellationToken cancellationToken = default)
		{
			return Task.FromResult(_collections.ContainsKey(collectionName));
		}

		public Task<MemoryRecord?> GetAsync(string collectionName, string key, bool withEmbedding = false, CancellationToken cancellationToken = default)
		{
			if (_collections.TryGetValue(collectionName, out var collection) &&
				collection.TryGetValue(key, out var record))
			{
				return Task.FromResult<MemoryRecord?>(record);
			}

			return Task.FromResult<MemoryRecord?>(null);
		}

		public async IAsyncEnumerable<MemoryRecord> GetBatchAsync(string collectionName, IEnumerable<string> keys, bool withEmbeddings = false, [EnumeratorCancellation] CancellationToken cancellationToken = default)
		{
			if (_collections.TryGetValue(collectionName, out var collection))
			{
				foreach (var key in keys)
				{
					if (collection.TryGetValue(key, out var record))
					{
						yield return record;
					}
				}
			}
		}

		public async IAsyncEnumerable<string> GetCollectionsAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
		{
			foreach (var collection in _collections.Keys)
			{
				yield return collection;
			}
		}

		public async Task<(MemoryRecord, double)?> GetNearestMatchAsync(string collectionName, ReadOnlyMemory<float> embedding, double minRelevanceScore = 0, bool withEmbedding = false, CancellationToken cancellationToken = default)
		{
			var results = (await GetNearestMatchAsync(collectionName, embedding, minRelevanceScore, withEmbedding));
			return results;
		}

		public async IAsyncEnumerable<(MemoryRecord, double)> GetNearestMatchesAsync(string collectionName, ReadOnlyMemory<float> embedding, int limit, double minRelevanceScore = 0, bool withEmbeddings = false, [EnumeratorCancellation] CancellationToken cancellationToken = default)
		{
			if (!_collections.TryGetValue(collectionName, out var collection))
				yield break;

			foreach (var (key, record) in collection)
			{
				if (!record.Embedding.IsEmpty)
				{
					double score = CosineSimilarity(record.Embedding.Span, embedding.Span);
					if (score >= minRelevanceScore)
						yield return (record, score);
				}
			}
		}

		public Task RemoveAsync(string collectionName, string key, CancellationToken cancellationToken = default)
		{
			if (_collections.TryGetValue(collectionName, out var collection))
			{
				collection.TryRemove(key, out _);
			}

			return Task.CompletedTask;
		}

		public Task RemoveBatchAsync(string collectionName, IEnumerable<string> keys, CancellationToken cancellationToken = default)
		{
			if (_collections.TryGetValue(collectionName, out var collection))
			{
				foreach (var key in keys)
				{
					collection.TryRemove(key, out _);
				}
			}

			return Task.CompletedTask;
		}

		public Task<string> UpsertAsync(string collectionName, MemoryRecord record, CancellationToken cancellationToken = default)
		{
			if (!_collections.TryGetValue(collectionName, out var collection))
			{
				collection = new ConcurrentDictionary<string, MemoryRecord>();
				_collections[collectionName] = collection;
			}

			var key = record.Metadata.Id;
			collection[key] = record;
			return Task.FromResult(key);
		}

		public async IAsyncEnumerable<string> UpsertBatchAsync(string collectionName, IEnumerable<MemoryRecord> records, [EnumeratorCancellation] CancellationToken cancellationToken = default)
		{
			foreach (var record in records)
			{
				var id = await UpsertAsync(collectionName, record, cancellationToken);
				yield return id;
			}
		}

		private static double CosineSimilarity(ReadOnlySpan<float> v1, ReadOnlySpan<float> v2)
		{
			if (v1.Length != v2.Length) return 0;

			double dot = 0, mag1 = 0, mag2 = 0;
			for (int i = 0; i < v1.Length; i++)
			{
				dot += v1[i] * v2[i];
				mag1 += v1[i] * v1[i];
				mag2 += v2[i] * v2[i];
			}

			return dot / (Math.Sqrt(mag1) * Math.Sqrt(mag2) + 1e-8); 
		}
	}

	public class MyEmbeddingService : ITextEmbeddingGenerationService
	{
		public IReadOnlyDictionary<string, object?> Attributes { get; } = new Dictionary<string, object?>();

		public Task<IList<ReadOnlyMemory<float>>> GenerateEmbeddingsAsync(IList<string> data, CancellationToken cancellationToken = default)
		{
			var result = data
				.Select(_ => new ReadOnlyMemory<float>(Enumerable.Repeat(0.1f, 1536).ToArray()))
				.ToList();

			return Task.FromResult<IList<ReadOnlyMemory<float>>>(result);
		}

		public Task<IList<ReadOnlyMemory<float>>> GenerateEmbeddingsAsync(IList<string> data, Kernel? kernel = null, CancellationToken cancellationToken = default)
		{
			return GenerateEmbeddingsAsync(data, cancellationToken);
		}
	}

}
