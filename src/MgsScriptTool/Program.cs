using System.Text;

using System.CommandLine;
using System.CommandLine.Parsing;
using System.Collections.Immutable;

namespace MgsScriptTool;

static class Program {
	static readonly Option<ToolMode> ModeOption = new(
		name: "--mode",
		description: "The tool operation mode."
	);

	static readonly Option<string> PlainDirectoryOption = new(
		name: "--plain-directory",
		description: "The path to the directory for the files in plain form."
	) {
		IsRequired = true,
	};

	static readonly Option<string> RawDirectoryOption = new(
		name: "--raw-directory",
		description: "The path to the directory for the files in raw form."
	) {
		IsRequired = true,
	};

	static readonly Option<string> ScriptPackageExtensionOption = new(
		name: "--script-package-extension",
		getDefaultValue: () => "scx",
		description: "The file extension to be used for raw script package files."
	);

	static readonly Option<string> StringTableExtensionOption = new(
		name: "--string-table-extension",
		getDefaultValue: () => "msb",
		description: "The file extension to be used for raw string table files."
	);

	static readonly Option<string> BankDirectoryOption = new(
		name: "--bank-directory",
		description: "The path to the specifications bank directory."
	) {
		IsRequired = true,
	};

	static readonly Option<string> FlagSetOption = new(
		name: "--flag-set",
		description: "The flag set to be used for selecting data to be loaded from the specifications bank."
	) {
		IsRequired = true,
	};

	static readonly Option<string> InstructionSetsOption = new(
		name: "--instruction-sets",
		description: "The instruction sets (comma-delimited) to be loaded from the specifications bank."
	) {
		IsRequired = true,
	};

	static readonly Option<string> CharsetOption = new(
		name: "--charset",
		description: "The charset to be loaded from the specifications bank."
	) {
		IsRequired = true,
	};

	static readonly Option<bool> GenerateSdbOption = new(
		name: "--generate-sdb",
		description: "Enable generation of SDB files. (currently only works for decompilation)"
	);

	enum ToolMode {
		Compile,
		Decompile,
	}

	sealed class Tool {
		public readonly ToolMode Mode;
		public readonly string RawDirectory;
		public readonly string PlainDirectory;
		public readonly string RawScriptPackageExtension;
		public readonly string RawStringTableExtension;
		public readonly bool GenerateSdb;

		public readonly InstructionEncoding InstructionEncoding;
		public readonly PlainStringTableSyntax PlainStringTableSyntax;
		public readonly StringGlyphSyntax StringGlyphSyntax;
		public readonly RawScriptPackageEncoding RawScriptPackageEncoding;
		public readonly RawStringTableEncoding RawStringTableEncoding;

		public Tool(ParseResult result) {
			Mode = result.GetValueForOption(ModeOption);
			RawDirectory = result.GetValueForOption(RawDirectoryOption)!;
			PlainDirectory = result.GetValueForOption(PlainDirectoryOption)!;
			RawScriptPackageExtension = $".{result.GetValueForOption(ScriptPackageExtensionOption)!}";
			RawStringTableExtension = $".{result.GetValueForOption(StringTableExtensionOption)!}";
			GenerateSdb = result.GetValueForOption(GenerateSdbOption);

			string bankDirectory = result.GetValueForOption(BankDirectoryOption)!;
			string flagSet = result.GetValueForOption(FlagSetOption)!;
			ImmutableArray<string> instructionSets = [..result.GetValueForOption(InstructionSetsOption)!.Split(",")];
			string charsetName = result.GetValueForOption(CharsetOption)!;

			SpecBank bank = SpecBank.Load(bankDirectory);
			ImmutableDictionary<string, bool> flags = bank.GetFlags(flagSet);

			ImmutableArray<InstructionSpec> instructionSpecs = bank.GetInstructionSpecs(instructionSets, flags);
			InstructionEncoding = InstructionEncoding.BuildFrom(instructionSpecs);

            PlainStringSyntax plainStringSyntax = new();
			PlainStringTableSyntax = new(plainStringSyntax);

			ImmutableArray<GlyphSpec> glyphSpecs = bank.GetGlyphSpecs(charsetName);
			StringGlyphSyntax = StringGlyphSyntax.BuildFrom(glyphSpecs);

			StringTagsSpec stringTagsSpec = new();
			RawStringEncoding rawStringEncoding = new(stringTagsSpec);
			RawScriptPackageEncoding = new(rawStringEncoding);
			RawStringTableEncoding = new(rawStringEncoding);
		}

		public async Task Run() {
			switch (Mode) {
				case ToolMode.Compile: {
					await DoCompile(this);
					break;
				}
				case ToolMode.Decompile: {
					await DoDecompile(this);
					break;
				}
				default: {
					throw new NotImplementedException(Mode.ToString());
				}
			}
		}
	}

	static async Task<int> Main(string[] args) {
		RootCommand rootCommand = new("A tool for working with MAGES. engine script packages and string tables.");
		rootCommand.AddGlobalOption(ModeOption);
		rootCommand.AddGlobalOption(PlainDirectoryOption);
		rootCommand.AddGlobalOption(RawDirectoryOption);
		rootCommand.AddGlobalOption(ScriptPackageExtensionOption);
		rootCommand.AddGlobalOption(StringTableExtensionOption);
		rootCommand.AddGlobalOption(GenerateSdbOption);
		rootCommand.AddGlobalOption(BankDirectoryOption);
		rootCommand.AddGlobalOption(FlagSetOption);
		rootCommand.AddGlobalOption(InstructionSetsOption);
		rootCommand.AddGlobalOption(CharsetOption);

		rootCommand.SetHandler(async context => {
			try {
				Tool tool = new(context.ParseResult);
				await tool.Run();
				context.ExitCode = 0;
			} catch (Exception e) {
				context.ExitCode = 1;
				Console.Error.WriteLine(e);
			}
		});

		return await rootCommand.InvokeAsync(args);
	}

	static async Task<int> DoCompile(Tool tool) {
		string plainDir = tool.PlainDirectory;
		IEnumerable<string> plainPaths;
		try {
			plainPaths = Directory.EnumerateFiles(plainDir, "*", SearchOption.AllDirectories);
		} catch (Exception e) {
			Console.Error.WriteLine($"Error: {e}");
			return 1;
		}
		bool errorOccurred = false;
		foreach (string plainPath in plainPaths) {
			string plainName = Path.GetRelativePath(plainDir, plainPath);
			if (Path.IsPathRooted(plainName)) {
				continue;
			}
			string extension = Path.GetExtension(plainPath);
			if (extension == ".scs") {
				errorOccurred |= !await CompileScriptPackage(tool, plainName);
			}
			if (extension == ".mst") {
				errorOccurred |= !await CompileStringTable(tool, plainName);
			}
		}
		if (errorOccurred) {
			return 1;
		}
		return 0;
	}

	static async Task<int> DoDecompile(Tool tool) {
		string rawDir = tool.RawDirectory;
		IEnumerable<string> rawPaths;
		try {
			rawPaths = Directory.EnumerateFiles(rawDir, "*", SearchOption.AllDirectories);
		} catch (Exception e) {
			Console.Error.WriteLine($"Error: {e}");
			return 1;
		}
		bool errorOccurred = false;
		foreach (string rawPath in rawPaths) {
			string rawName = Path.GetRelativePath(rawDir, rawPath);
			if (Path.IsPathRooted(rawName)) {
				continue;
			}
			string extension = Path.GetExtension(rawPath);
			if (extension == tool.RawScriptPackageExtension) {
				errorOccurred |= !await DecompileScriptPackage(tool, rawName);
			}
			if (extension == tool.RawStringTableExtension) {
				errorOccurred |= !await DecompileStringTable(tool, rawName);
			}
		}
		if (errorOccurred) {
			return 1;
		}
		return 0;
	}

	static async Task<bool> CompileScriptPackage(Tool tool, string plainScriptName) {
		string plainStringTableName = Path.ChangeExtension(plainScriptName, ".sct");
		string rawScriptPackageName = Path.ChangeExtension(plainScriptName, tool.RawScriptPackageExtension);

		string plainScriptPath = Path.Join(tool.PlainDirectory, plainScriptName);
		string plainStringTablePath = Path.Join(tool.PlainDirectory, plainStringTableName);
		string rawScriptPackagePath = Path.Join(tool.RawDirectory, rawScriptPackageName);

		try {
			ImmutableArray<PlainScriptElement> plainScriptElements = await ParsePlainScript(tool, plainScriptPath);
			ImmutableArray<StringTableEntry> plainStringTableEntries = await ParsePlainStringTable(tool, plainStringTablePath);

			ScriptCompiler compiler = new(tool.InstructionEncoding);
			RawScript rawScript = compiler.Compile(plainScriptElements);

			List<ImmutableArray<StringToken>> rawStrings = [];
			for (int i = 0; i < plainStringTableEntries.Length; i++) {
				StringTableEntry plainEntry = plainStringTableEntries[i];
				if (plainEntry.Index != i) {
					throw new Exception($"Missing string with index {i}.");
				}
				rawStrings.Add(tool.StringGlyphSyntax.Parse(plainEntry.Tokens));
			}
			RawScriptPackage rawScriptPackage = new(rawScript, [..rawStrings]);

			Directory.CreateDirectory(Path.GetDirectoryName(rawScriptPackagePath)!);

			using FileStream file = File.Open(rawScriptPackagePath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
			tool.RawScriptPackageEncoding.Encode(file, rawScriptPackage);
		} catch (Exception e) {
			Console.Error.WriteLine($"\nError while compiling {plainScriptPath}: {e}");
			return false;
		}
		return true;
	}

	static async Task<bool> CompileStringTable(Tool tool, string plainName) {
		string rawName = Path.ChangeExtension(plainName, tool.RawStringTableExtension);

		string plainPath = Path.Join(tool.PlainDirectory, plainName);
		string rawPath = Path.Join(tool.RawDirectory, rawName);

		try {
			ImmutableArray<StringTableEntry> plainEntries = await ParsePlainStringTable(tool, plainPath);

			List<StringTableEntry> rawEntries = [];
			for (int i = 0; i < plainEntries.Length; i++) {
				StringTableEntry plainEntry = plainEntries[i];
				ImmutableArray<StringToken> rawTokens = tool.StringGlyphSyntax.Parse(plainEntry.Tokens);
				rawEntries.Add(new(plainEntry.Index, rawTokens));
			}

			Directory.CreateDirectory(Path.GetDirectoryName(rawPath)!);

			using FileStream file = File.Open(rawPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
			tool.RawStringTableEncoding.Encode(file, [..rawEntries]);
		} catch (Exception e) {
			Console.Error.WriteLine($"\nError while compiling {plainPath}: {e}");
			return false;
		}
		return true;
	}

	static async Task<bool> DecompileScriptPackage(Tool tool, string rawScriptPackageName) {
		string plainScriptName = Path.ChangeExtension(rawScriptPackageName, ".scs");
		string plainStringTableName = Path.ChangeExtension(rawScriptPackageName, ".sct");
		string sdbName = Path.ChangeExtension(rawScriptPackageName, ".sdb");

		string rawScriptPackagePath = Path.Join(tool.RawDirectory, rawScriptPackageName);
		string plainScriptPath = Path.Join(tool.PlainDirectory, plainScriptName);
		string plainStringTablePath = Path.Join(tool.PlainDirectory, plainStringTableName);
		string sdbPath = Path.Join(tool.PlainDirectory, sdbName);

		try {
			RawScriptPackage rawScriptPackage = await DecodeRawScriptPackage(tool, rawScriptPackagePath);

			ScriptDecompiler decompiler = new(tool.InstructionEncoding, rawScriptPackage.Script);
			(ImmutableArray<PlainScriptElement> plainScriptElements, ImmutableDictionary<PlainScriptElementInstruction, int> instructionPositions) = decompiler.Decompile();

			ImmutableArray<ImmutableArray<StringToken>> rawStrings = rawScriptPackage.Strings;
			List<StringTableEntry> plainStringEntries = [];
			for (int i = 0; i < rawStrings.Length; i++) {
				ImmutableArray<StringToken> plainTokens = tool.StringGlyphSyntax.Format(rawStrings[i]);
				plainStringEntries.Add(new(i, plainTokens));
			}

			Directory.CreateDirectory(Path.GetDirectoryName(plainScriptPath)!);

			List<Exception> exceptions = [];
			try {
				StringBuilder builder = new();
				tool.PlainStringTableSyntax.Format(builder, [..plainStringEntries]);
				await File.WriteAllTextAsync(plainStringTablePath, builder.ToString(), new UTF8Encoding(false, true));
			} catch (Exception e) {
				exceptions.Add(e);
			}
			try {
				StringBuilder builder = new();
				StringBuilder? sdbBuilder = null;
				if (tool.GenerateSdb) {
					sdbBuilder = new();
				}
				PlainScriptSyntax.Format(builder, sdbBuilder, instructionPositions, plainScriptElements);
				await File.WriteAllTextAsync(plainScriptPath, builder.ToString(), new UTF8Encoding(false, true));
				if (tool.GenerateSdb) {
					await File.WriteAllTextAsync(sdbPath, sdbBuilder!.ToString(), new UTF8Encoding(false, true));
				}
			} catch (Exception e) {
				exceptions.Add(e);
			}
			if (exceptions.Count > 0) {
				throw new AggregateException(exceptions);
			}
		} catch (Exception e) {
			Console.Error.WriteLine($"\nError while decompiling {rawScriptPackagePath}: {e}");
			return false;
		}
		return true;
	}

	static async Task<bool> DecompileStringTable(Tool tool, string plainName) {
		string rawName = Path.ChangeExtension(plainName, ".mst");

		string plainPath = Path.Join(tool.RawDirectory, plainName);
		string rawPath = Path.Join(tool.PlainDirectory, rawName);

		try {
			ImmutableArray<StringTableEntry> rawEntries = await DecodeRawStringTable(tool, plainPath);

			List<StringTableEntry> plainEntries = [];
			for (int i = 0; i < rawEntries.Length; i++) {
				StringTableEntry rawEntry = rawEntries[i];
				ImmutableArray<StringToken> plainTokens = tool.StringGlyphSyntax.Format(rawEntry.Tokens);
				plainEntries.Add(new(rawEntry.Index, plainTokens));
			}

			Directory.CreateDirectory(Path.GetDirectoryName(rawPath)!);

			StringBuilder builder = new();
			tool.PlainStringTableSyntax.Format(builder, [..plainEntries]);
			await File.WriteAllTextAsync(rawPath, builder.ToString(), new UTF8Encoding(false, true));
		} catch (Exception e) {
			Console.Error.WriteLine($"\nError while decompiling {plainPath}: {e}");
			return false;
		}
		return true;
	}

	static async Task<ImmutableArray<PlainScriptElement>> ParsePlainScript(Tool tool, string path) {
		TextStream stream = await ReadFileText(path);
		return PlainScriptSyntax.Parse(stream);
	}

	static async Task<ImmutableArray<StringTableEntry>> ParsePlainStringTable(Tool tool, string path) {
		TextStream stream = await ReadFileText(path);
		return tool.PlainStringTableSyntax.Parse(stream);
	}

	static async Task<RawScriptPackage> DecodeRawScriptPackage(Tool tool, string path) {
		MemoryStream stream = await ReadFileBytes(path);
		return tool.RawScriptPackageEncoding.Decode(stream);
	}

	static async Task<ImmutableArray<StringTableEntry>> DecodeRawStringTable(Tool tool, string path) {
		MemoryStream stream = await ReadFileBytes(path);
		return tool.RawStringTableEncoding.Decode(stream);
	}

	static async Task<TextStream> ReadFileText(string path) {
		string data = await File.ReadAllTextAsync(path, new UTF8Encoding(false, true));
		return new(data);
	}

	static async Task<MemoryStream> ReadFileBytes(string path) {
		byte[] data = await File.ReadAllBytesAsync(path);
		return new(data);
	}
}
