using System.Collections.Immutable;

namespace MgsScriptTool;

sealed class StringTagsSpec {
	readonly Dictionary<string, StringTagSpec> _byName = [];
	readonly Dictionary<int, StringTagSpec> _byOpcode = [];

	public StringTagsSpec(ImmutableArray<StringTagSpec> specs) {
		foreach (StringTagSpec spec in specs) {
			if (_byName.ContainsKey(spec.Name)) {
				throw new Exception($"Duplicate string tag name: {spec.Name}.");
			}
			if (_byOpcode.ContainsKey(spec.Opcode)) {
				throw new Exception($"Duplicate string tag opcode: 0x{spec.Opcode:X02}.");
			}
			_byName[spec.Name] = spec;
			_byOpcode[spec.Opcode] = spec;
		}
	}

	public StringTagSpec? GetSpec(string name) {
		return _byName.GetValueOrDefault(name);
	}

	public StringTagSpec? GetSpec(int opcode) {
		return _byOpcode.GetValueOrDefault(opcode);
	}
}
