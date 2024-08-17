using System.Collections.Immutable;

namespace MgsScriptTool;

internal class ImmutableTree<K, V> where K : notnull {
	readonly ImmutableDictionary<K, ImmutableTree<K, V>> _branches;
	readonly V? _value;

	public ImmutableTree(ImmutableDictionary<K, ImmutableTree<K, V>> branches, V? value) {
		_branches = branches;
		_value = value;
	}

	public bool HasBranches => _branches.Count != 0;
	public bool HasValue => _value is not null;

	public ImmutableTree<K, V>? this[K key] {
		get => _branches.GetValueOrDefault(key);
	}

	public V? Value {
		get => _value;
	}
}
