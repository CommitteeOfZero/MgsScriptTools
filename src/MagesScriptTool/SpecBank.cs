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
		using JsonDocument document = JsonDocument.Parse(
			File.ReadAllText(Path.Join(_path, path), new UTF8Encoding(false, true))
		);
		List<GlyphSpec> glyphs = [];
		foreach (JsonElement groupJson in document.RootElement.EnumerateArray()) {
			glyphs.AddRange(toGlyphSpecs(groupJson));
		}
		return [..glyphs];

		static ImmutableArray<GlyphSpec> toGlyphSpecs(JsonElement json) {
			JsonElement parametersJson = json.GetProperty("parameters").GetProperty("encoding");
			if (parametersJson.ValueKind == JsonValueKind.Null) {
				return [];
			}
			GlyphStyle? style = toStyle(parametersJson.GetProperty("style"));

			List<GlyphSpec> glyphs = [];
			JsonElement glyphsJson = json.GetProperty("glyphs");
			if(glyphsJson.ValueKind == JsonValueKind.Object) {
				Int16	unitsOffset = glyphsJson.GetProperty("unitsOffset").GetInt16();
				string text = toText(glyphsJson.GetProperty("text"));
				int index = 0;
                TextElementEnumerator charEnum = StringInfo.GetTextElementEnumerator(text);
                while (charEnum.MoveNext()) {
					glyphs.Add(new([unitsOffset + index], charEnum.GetTextElement(), style));
					index++;
				}
			} else if (glyphsJson.ValueKind == JsonValueKind.Array) {
				foreach (JsonElement glyphJson in glyphsJson.EnumerateArray()) {
					ImmutableArray<int> units = toUnits(glyphJson.GetProperty("units"));
					string text = toText(glyphJson.GetProperty("text"));
					glyphs.Add(new(units, text, style));
				}
			} else {
				throw new NotImplementedException($"{glyphsJson.ValueKind} is not implemented.");
			}
			return [..glyphs];
		}

		static GlyphStyle? toStyle(JsonElement json) {
			switch (json.ValueKind) {
				case JsonValueKind.Null: {
					return null;
				}
				case JsonValueKind.String: {
					string value = json.GetString()!;
					return value switch {
						"normal" => GlyphStyle.Normal,
						"italic" => GlyphStyle.Italic,
						_ => throw new NotImplementedException($"'{value}' is not implemented."),
					};
				}
				default: {
					throw new NotImplementedException($"{json.ValueKind} is not implemented.");
				}
			}
		}

		static ImmutableArray<int> toUnits(JsonElement json) {
			List<int> units = [];
			foreach (JsonElement unitJson in json.EnumerateArray()) {
				units.Add(unitJson.GetInt32());
			}
			return [..units];
		}

		static string toText(JsonElement json) {
			switch (json.ValueKind) {
				case JsonValueKind.String: {
					return json.GetString()!;
				}
				case JsonValueKind.Object: {
					return json.GetProperty("encoding").GetString()!;
				}
				default: {
					throw new NotImplementedException($"{json.ValueKind} is not implemented.");
				}
			}
		}
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
