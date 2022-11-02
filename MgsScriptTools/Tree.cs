namespace MgsScriptTools;

internal class Tree<K, V> where K : notnull {
	Dictionary<K, Tree<K, V>> _branches = new();
	V? _value;

	public bool HasBranches => _branches.Count != 0;
	public bool HasValue => _value is not null;

	public Tree<K, V>? this[K key] {
		get => _branches.GetValueOrDefault(key);
	}

	public V? Value {
		get => _value;
		set => _value = value;
	}

	public Tree<K, V> CreateBranch(K key) {
		if (!_branches.ContainsKey(key))
			_branches[key] = new();
		return _branches[key];
	}
}
