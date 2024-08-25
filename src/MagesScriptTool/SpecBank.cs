using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Text.Json;
using YamlDotNet.Serialization;

namespace MagesScriptTool;

class SpecBank {
	readonly string _path;
	readonly SpecBankIndex _index;

	SpecBank(string path, SpecBankIndex index) {
		_path = path;
		_index = index;
	}

	public ImmutableArray<InstructionSpec> GetInstructionSpecs(ImmutableDictionary<string, bool> flags) {
		List<InstructionSpec> result = [];
		foreach (string path in _index.Instructions) {
			SerializedInstructionSpec[] specs = SerializedInstructionSpec.LoadList(Path.Join(_path, path));
			foreach (SerializedInstructionSpec spec in specs) {
				if (!spec.CheckFlags(flags)) {
					continue;
				}
				result.Add(new(spec.Name, spec.ParsePattern(), spec.ParseOperands()));
			}
		}
		return [..result];
	}

	public ImmutableArray<StringTagSpec> GetStringTagSpecs(ImmutableDictionary<string, bool> flags) {
		List<StringTagSpec> result = [];
		foreach (string path in _index.StringTags) {
			SerializedStringTagSpec[] specs = SerializedStringTagSpec.LoadList(Path.Join(_path, path));
			foreach (SerializedStringTagSpec spec in specs) {
				if (!spec.CheckFlags(flags)) {
					continue;
				}
				result.Add(new(spec.Name, spec.Opcode, spec.ParseOperands()));
			}
		}
		return [..result];
	}

	public ImmutableArray<GlyphSpec> GetGlyphSpecs(string key) {
		string path = _index.Charset[key];
		string text = File.ReadAllText(Path.Join(_path, path), new UTF8Encoding(false, true));
		using JsonDocument document = JsonDocument.Parse(text)!;
		List<GlyphSpec> glyphs = [];
		foreach (JsonProperty prop in document.RootElement.EnumerateObject()) {
			int index = int.Parse(prop.Name, NumberStyles.HexNumber);
			GlyphSpec glyph = ToGlyphSpec(prop.Value, index);
			glyphs.Add(glyph);
		}
		return [..glyphs];
	}

	static GlyphSpec ToGlyphSpec(JsonElement json, int index) {
		string text = json.GetProperty("text").GetString()!;
		bool regular = true;
		bool italic = false;
		if (json.TryGetProperty("italic", out JsonElement italicJson)) {
			regular = false;
			italic = italicJson.GetBoolean();
		}
		if (json.TryGetProperty("regular", out JsonElement regularJson)) {
			regular = regularJson.GetBoolean();
		}
		return new(index, text, regular, italic);
	}

	public ImmutableDictionary<string, bool> GetFlags(string key) {
		return _index.Flags[key].ToImmutableDictionary();
	}

	public static SpecBank Load(string path) {
		SpecBankIndex index = SpecBankIndex.Load(Path.Join(path, "data.yaml"));
		return new(path, index);
	}

	sealed class SpecBankIndex {
		[YamlMember(Alias = "instructions", ApplyNamingConventions = false)]
		public string[] Instructions { get; set; } = default!;

		[YamlMember(Alias = "stringtags", ApplyNamingConventions = false)]
		public string[] StringTags { get; set; } = default!;

		[YamlMember(Alias = "macros", ApplyNamingConventions = false)]
		public string[] Macros { get; set; } = default!;

		[YamlMember(Alias = "charset", ApplyNamingConventions = false)]
		public Dictionary<string, string> Charset { get; set; } = default!;

		[YamlMember(Alias = "flags", ApplyNamingConventions = false)]
		public Dictionary<string, Dictionary<string, bool>> Flags { get; set; } = default!;

		public static SpecBankIndex Load(string path) {
			string text = File.ReadAllText(path, new UTF8Encoding(false, true));
			IDeserializer deserializer = new DeserializerBuilder().Build();
			return deserializer.Deserialize<SpecBankIndex>(text);
		}
	}

	sealed class SerializedInstructionSpec {
		[YamlMember(Alias = "pattern", ApplyNamingConventions = false)]
		public string Pattern { get; set; } = default!;

		[YamlMember(Alias = "name", ApplyNamingConventions = false)]
		public string Name { get; set; } = default!;

		[YamlMember(Alias = "operands", ApplyNamingConventions = false)]
		public string[] Operands { get; set; } = default!;

		[YamlMember(Alias = "flags", ApplyNamingConventions = false)]
		public string[] Flags { get; set; } = default!;

		public ImmutableArray<byte> ParsePattern() {
			string[] values = Pattern.Split(' ');
			List<byte> pattern = [];
			for (int i = 0; i < values.Length; i++) {
				pattern.Add(byte.Parse(values[i], NumberStyles.HexNumber));
			}
			return [..pattern];
		}

		public ImmutableArray<OperandKind> ParseOperands() {
			List<OperandKind> operands = [];
			for (int i = 0; i < Operands.Length; i++) {
				operands.Add(Operands[i] switch {
					"expr" => OperandKind.Expr,
					"uint8" => OperandKind.UInt8,
					"int8" => OperandKind.Int8,
					"int16" => OperandKind.Int16,
					"int32" => OperandKind.Int32,
					_ => throw new Exception($"Unrecognized operand kind name: {Operands[i]}."),
				});
			}
			return [..operands];
		}

		public bool CheckFlags(ImmutableDictionary<string, bool> flags) {
			foreach (string flag in Flags) {
				string name;
				bool value;
				if (flag.StartsWith('~')) {
					(name, value) = (flag[1..], false);
				} else {
					(name, value) = (flag, true);
				}
				if (!flags.ContainsKey(name) || flags[name] != value) {
					return false;
				}
			}
			return true;
		}

		public static SerializedInstructionSpec[] LoadList(string path) {
			string text = File.ReadAllText(path, new UTF8Encoding(false, true));
			IDeserializer deserializer = new DeserializerBuilder().Build();
			return deserializer.Deserialize<SerializedInstructionSpec[]>(text);
		}
	}

	sealed class SerializedStringTagSpec {
		[YamlMember(Alias = "opcode", ApplyNamingConventions = false)]
		public int Opcode { get; set; } = default;

		[YamlMember(Alias = "name", ApplyNamingConventions = false)]
		public string Name { get; set; } = default!;

		[YamlMember(Alias = "operands", ApplyNamingConventions = false)]
		public string[] Operands { get; set; } = default!;

		[YamlMember(Alias = "flags", ApplyNamingConventions = false)]
		public string[] Flags { get; set; } = default!;

		public ImmutableArray<OperandKind> ParseOperands() {
			List<OperandKind> operands = [];
			for (int i = 0; i < Operands.Length; i++) {
				operands.Add(Operands[i] switch {
					"expr" => OperandKind.Expr,
					"uint8" => OperandKind.UInt8,
					"int8" => OperandKind.Int8,
					"int16" => OperandKind.Int16,
					"int32" => OperandKind.Int32,
					_ => throw new Exception($"Unrecognized operand kind name: {Operands[i]}."),
				});
			}
			return [..operands];
		}

		public bool CheckFlags(ImmutableDictionary<string, bool> flags) {
			foreach (string flag in Flags) {
				string name;
				bool value;
				if (flag.StartsWith('~')) {
					(name, value) = (flag[1..], false);
				} else {
					(name, value) = (flag, true);
				}
				if (!flags.ContainsKey(name) || flags[name] != value) {
					return false;
				}
			}
			return true;
		}

		public static SerializedStringTagSpec[] LoadList(string path) {
			string text = File.ReadAllText(path, new UTF8Encoding(false, true));
			IDeserializer deserializer = new DeserializerBuilder().Build();
			return deserializer.Deserialize<SerializedStringTagSpec[]>(text);
		}
	}
}
