using System.Globalization;
using System.Text.Json;
using YamlDotNet.Serialization;

namespace MgsScriptTools;

class SpecBank {
	string _path;
	SpecBankIndex _index;

	SpecBank(string path, SpecBankIndex index) {
		_path = path;
		_index = index;
	}

	public InstructionSpec[] GetOpcodeSpecs(string[] keys, Dictionary<string, bool> flags) {
		List<InstructionSpec> result = new();
		foreach (var key in keys) {
			foreach (var path in _index.Instructions[key]) {
				var specs = SerializedInstructionSpec.LoadList(Path.Join(_path, path));
				foreach (var spec in specs) {
					if (!spec.CheckFlags(flags))
						continue;
					result.Add(new InstructionSpec {
						Name = spec.Name,
						Opcode = spec.ParsePattern(),
						Operands = spec.ParseOperands(),
					});
				}
			}
		}
		return result.ToArray();
	}

	public GlyphSpec[] GetGlyphSpecs(string key) {
		var path = _index.Charset[key];
		var text = File.ReadAllText(Path.Join(_path, path));
		using var document = JsonDocument.Parse(text)!;
		List<GlyphSpec> glyphs = new();
		foreach (var prop in document.RootElement.EnumerateObject()) {
			int index = int.Parse(prop.Name, NumberStyles.HexNumber);
			var glyph = ToGlyphSpec(prop.Value);
			glyph.Index = index;
			glyphs.Add(glyph);
		}
		return glyphs.ToArray();
	}

	static GlyphSpec ToGlyphSpec(JsonElement json) {
		string text = json.GetProperty("text").GetString()!;
		bool regular = true;
		bool italic = false;
		if (json.TryGetProperty("italic", out var italicJson)) {
			regular = false;
			italic = italicJson.GetBoolean();
		}
		if (json.TryGetProperty("regular", out var regularJson))
			regular = regularJson.GetBoolean();
		return new() {
			Text = text,
			Regular = regular,
			Italic = italic,
		};
	}

	public Dictionary<string, bool> GetFlags(string key) {
		return _index.Flags[key];
	}

	public static SpecBank Load(string path) {
		var index = SpecBankIndex.Load(Path.Join(path, "data.yaml"));
		return new(path, index);
	}

	class SpecBankIndex {
		[YamlMember(Alias = "instructions", ApplyNamingConventions = false)]
		public Dictionary<string, string[]> Instructions { get; set; } = null!;

		[YamlMember(Alias = "macros", ApplyNamingConventions = false)]
		public Dictionary<string, string> Macros { get; set; } = null!;

		[YamlMember(Alias = "charset", ApplyNamingConventions = false)]
		public Dictionary<string, string> Charset { get; set; } = null!;

		[YamlMember(Alias = "flags", ApplyNamingConventions = false)]
		public Dictionary<string, Dictionary<string, bool>> Flags { get; set; } = null!;

		public static SpecBankIndex Load(string path) {
			var text = File.ReadAllText(path);
			var deserializer = new DeserializerBuilder().Build();
			return deserializer.Deserialize<SpecBankIndex>(text);
		}
	}

	class SerializedInstructionSpec {
		[YamlMember(Alias = "pattern", ApplyNamingConventions = false)]
		public string Pattern { get; set; } = null!;

		[YamlMember(Alias = "name", ApplyNamingConventions = false)]
		public string Name { get; set; } = null!;

		[YamlMember(Alias = "operands", ApplyNamingConventions = false)]
		public string[] Operands { get; set; } = null!;

		[YamlMember(Alias = "flags", ApplyNamingConventions = false)]
		public string[] Flags { get; set; } = null!;

		public byte[] ParsePattern() {
			var values = Pattern.Split(' ');
			var pattern = new byte[values.Length];
			for (int i = 0; i < values.Length; i++)
				pattern[i] = byte.Parse(values[i], NumberStyles.HexNumber);
			return pattern;
		}

		public OperandKind[] ParseOperands() {
			var operands = new OperandKind[Operands.Length];
			for (int i = 0; i < Operands.Length; i++) {
				operands[i] = Operands[i] switch {
					"int8" => OperandKind.Int8,
					"int16" => OperandKind.Int16,
					"int32" => OperandKind.Int32,
					"calc" => OperandKind.Calc,
					_ => throw new Exception($"Unrecognized operand kind name: {Operands[i]}"),
				};
			}
			return operands;
		}

		public bool CheckFlags(Dictionary<string, bool> flags) {
			foreach (var flag in Flags) {
				string name;
				bool value;
				if (flag.StartsWith('~'))
					(name, value) = (flag[1..], false);
				else
					(name, value) = (flag, true);
				if (!flags.ContainsKey(name) || flags[name] != value)
					return false;
			}
			return true;
		}

		public static SerializedInstructionSpec[] LoadList(string path) {
			var text = File.ReadAllText(path);
			var deserializer = new DeserializerBuilder().Build();
			return deserializer.Deserialize<SerializedInstructionSpec[]>(text);
		}
	}
}

class GlyphSpec {
	public int Index;
	public string Text = null!;
	public bool Regular;
	public bool Italic;
}
