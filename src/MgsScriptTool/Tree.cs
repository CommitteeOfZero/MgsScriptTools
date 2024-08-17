using System.Collections.Immutable;

namespace MgsScriptTool;

sealed class Tree<K, V> where K : notnull {
	readonly Dictionary<K, Tree<K, V>> _branches = [];
	public V? Value { get; set; }

	public bool HasBranches => _branches.Count != 0;
	public bool HasValue => Value is not null;

	public Tree<K, V>? this[K key] {
		get => _branches.GetValueOrDefault(key);
	}

	public Tree<K, V> EnsureBranch(K key) {
		if (!_branches.ContainsKey(key)) {
			_branches[key] = new();
		}
		return _branches[key];
	}

	public ImmutableTree<K, V> ToImmutableTree() {
		Dictionary<K, ImmutableTree<K, V>> branches = [];
		foreach ((K key, Tree<K, V> branch) in _branches) {
			branches[key] = branch.ToImmutableTree();
		}
		return new(branches.ToImmutableDictionary(), Value);
	}
}
