using System.Collections.Immutable;

namespace MagesScriptTool;

sealed class DataDirectivesSpec {
	readonly Dictionary<string, DataDirectiveSpec> _byName = [];

	public DataDirectivesSpec(ImmutableArray<DataDirectiveSpec> specs) {
		foreach (DataDirectiveSpec spec in specs) {
			if (_byName.ContainsKey(spec.Name)) {
				throw new Exception($"Duplicate string tag name: {spec.Name}.");
			}
			_byName[spec.Name] = spec;
		}
	}

	public DataDirectiveSpec? GetSpec(string name) {
		return _byName.GetValueOrDefault(name);
	}
}
