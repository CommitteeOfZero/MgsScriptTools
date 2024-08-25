using System.Text;

using System.CommandLine;
using System.CommandLine.Parsing;
using System.Collections.Immutable;

namespace MagesScriptTool;

static class Program {
	static readonly Option<ToolMode> ModeOption = new(
		name: "--mode",
		description: "The tool operation mode."
	);

	static readonly Option<string> UncompiledDirectoryOption = new(
		name: "--uncompiled-directory",
		description: "The path to the directory for the uncompiled files."
	) {
		IsRequired = true,
	};

	static readonly Option<string> CompiledDirectoryOption = new(
		name: "--compiled-directory",
		description: "The path to the directory for the compiled files."
	) {
		IsRequired = true,
	};

	static readonly Option<string> ScriptPackageExtensionOption = new(
		name: "--script-package-extension",
		getDefaultValue: () => "scx",
		description: "The file extension to be used for compiled script package files."
	);

	static readonly Option<string> StringTableExtensionOption = new(
		name: "--string-table-extension",
		getDefaultValue: () => "msb",
		description: "The file extension to be used for compiled string table files."
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
		public readonly string CompiledDirectory;
		public readonly string UncompiledDirectory;
		public readonly string CompiledScriptPackageExtension;
		public readonly string CompiledStringTableExtension;
		public readonly bool GenerateSdb;

		public readonly InstructionEncoding InstructionEncoding;
		public readonly UncompiledStringTableSyntax UncompiledStringTableSyntax;
		public readonly StringGlyphSyntax StringGlyphSyntax;
		public readonly CompiledScriptPackageEncoding CompiledScriptPackageEncoding;
		public readonly CompiledStringTableEncoding CompiledStringTableEncoding;

		public Tool(ParseResult result) {
			Mode = result.GetValueForOption(ModeOption);
			CompiledDirectory = result.GetValueForOption(CompiledDirectoryOption)!;
			UncompiledDirectory = result.GetValueForOption(UncompiledDirectoryOption)!;
			CompiledScriptPackageExtension = $".{result.GetValueForOption(ScriptPackageExtensionOption)!}";
			CompiledStringTableExtension = $".{result.GetValueForOption(StringTableExtensionOption)!}";
			GenerateSdb = result.GetValueForOption(GenerateSdbOption);

			string bankDirectory = result.GetValueForOption(BankDirectoryOption)!;
			string flagSet = result.GetValueForOption(FlagSetOption)!;
			string charsetName = result.GetValueForOption(CharsetOption)!;

			SpecBank bank = SpecBank.Load(bankDirectory);
			ImmutableDictionary<string, bool> flags = bank.GetFlags(flagSet);

			ImmutableArray<InstructionSpec> instructionSpecs = bank.GetInstructionSpecs(flags);
			InstructionEncoding = InstructionEncoding.BuildFrom(instructionSpecs);

            UncompiledStringSyntax uncompiledStringSyntax = new();
			UncompiledStringTableSyntax = new(uncompiledStringSyntax);

			ImmutableArray<GlyphSpec> glyphSpecs = bank.GetGlyphSpecs(charsetName);
			StringGlyphSyntax = StringGlyphSyntax.BuildFrom(glyphSpecs);

			ImmutableArray<StringTagSpec> stringTagSpecs = bank.GetStringTagSpecs(flags);
			StringTagsSpec stringTagsSpec = new(stringTagSpecs);
			CompiledStringEncoding compiledStringEncoding = new(stringTagsSpec);
			CompiledScriptPackageEncoding = new(compiledStringEncoding);
			CompiledStringTableEncoding = new(compiledStringEncoding);
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
		rootCommand.AddGlobalOption(UncompiledDirectoryOption);
		rootCommand.AddGlobalOption(CompiledDirectoryOption);
		rootCommand.AddGlobalOption(ScriptPackageExtensionOption);
		rootCommand.AddGlobalOption(StringTableExtensionOption);
		rootCommand.AddGlobalOption(GenerateSdbOption);
		rootCommand.AddGlobalOption(BankDirectoryOption);
		rootCommand.AddGlobalOption(FlagSetOption);
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
		string uncompiledDir = tool.UncompiledDirectory;
		IEnumerable<string> uncompiledPaths;
		try {
			uncompiledPaths = Directory.EnumerateFiles(uncompiledDir, "*", SearchOption.AllDirectories);
		} catch (Exception e) {
			Console.Error.WriteLine($"Error: {e}");
			return 1;
		}
		bool errorOccurred = false;
		foreach (string uncompiledPath in uncompiledPaths) {
			string uncompiledName = Path.GetRelativePath(uncompiledDir, uncompiledPath);
			if (Path.IsPathRooted(uncompiledName)) {
				continue;
			}
			string extension = Path.GetExtension(uncompiledPath);
			if (extension == ".scs") {
				errorOccurred |= !await CompileScriptPackage(tool, uncompiledName);
			}
			if (extension == ".mst") {
				errorOccurred |= !await CompileStringTable(tool, uncompiledName);
			}
		}
		if (errorOccurred) {
			return 1;
		}
		return 0;
	}

	static async Task<int> DoDecompile(Tool tool) {
		string compiledDir = tool.CompiledDirectory;
		IEnumerable<string> compiledPaths;
		try {
			compiledPaths = Directory.EnumerateFiles(compiledDir, "*", SearchOption.AllDirectories);
		} catch (Exception e) {
			Console.Error.WriteLine($"Error: {e}");
			return 1;
		}
		bool errorOccurred = false;
		foreach (string compiledPath in compiledPaths) {
			string compiledName = Path.GetRelativePath(compiledDir, compiledPath);
			if (Path.IsPathRooted(compiledName)) {
				continue;
			}
			string extension = Path.GetExtension(compiledPath);
			if (extension == tool.CompiledScriptPackageExtension) {
				errorOccurred |= !await DecompileScriptPackage(tool, compiledName);
			}
			if (extension == tool.CompiledStringTableExtension) {
				errorOccurred |= !await DecompileStringTable(tool, compiledName);
			}
		}
		if (errorOccurred) {
			return 1;
		}
		return 0;
	}

	static async Task<bool> CompileScriptPackage(Tool tool, string uncompiledScriptName) {
		string uncompiledStringTableName = Path.ChangeExtension(uncompiledScriptName, ".sct");
		string compiledScriptPackageName = Path.ChangeExtension(uncompiledScriptName, tool.CompiledScriptPackageExtension);

		string uncompiledScriptPath = Path.Join(tool.UncompiledDirectory, uncompiledScriptName);
		string uncompiledStringTablePath = Path.Join(tool.UncompiledDirectory, uncompiledStringTableName);
		string compiledScriptPackagePath = Path.Join(tool.CompiledDirectory, compiledScriptPackageName);

		try {
			ImmutableArray<UncompiledScriptElement> uncompiledScriptElements = await ParseUncompiledScript(tool, uncompiledScriptPath);
			ImmutableArray<StringTableEntry> uncompiledStringTableEntries = await ParseUncompiledStringTable(tool, uncompiledStringTablePath);

			ScriptCompiler compiler = new(tool.InstructionEncoding);
			CompiledScript compiledScript = compiler.Compile(uncompiledScriptElements);

			List<ImmutableArray<StringToken>> compiledStrings = [];
			for (int i = 0; i < uncompiledStringTableEntries.Length; i++) {
				StringTableEntry uncompiledEntry = uncompiledStringTableEntries[i];
				if (uncompiledEntry.Index != i) {
					throw new Exception($"Missing string with index {i}.");
				}
				compiledStrings.Add(tool.StringGlyphSyntax.Compile(uncompiledEntry.Tokens));
			}
			CompiledScriptPackage compiledScriptPackage = new(compiledScript, [..compiledStrings]);

			Directory.CreateDirectory(Path.GetDirectoryName(compiledScriptPackagePath)!);

			using FileStream file = File.Open(compiledScriptPackagePath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
			tool.CompiledScriptPackageEncoding.Encode(file, compiledScriptPackage);
		} catch (Exception e) {
			Console.Error.WriteLine($"\nError while compiling {uncompiledScriptPath}: {e}");
			return false;
		}
		return true;
	}

	static async Task<bool> CompileStringTable(Tool tool, string uncompiledName) {
		string compiledName = Path.ChangeExtension(uncompiledName, tool.CompiledStringTableExtension);

		string uncompiledPath = Path.Join(tool.UncompiledDirectory, uncompiledName);
		string compiledPath = Path.Join(tool.CompiledDirectory, compiledName);

		try {
			ImmutableArray<StringTableEntry> uncompiledEntries = await ParseUncompiledStringTable(tool, uncompiledPath);

			List<StringTableEntry> compiledEntries = [];
			for (int i = 0; i < uncompiledEntries.Length; i++) {
				StringTableEntry uncompiledEntry = uncompiledEntries[i];
				ImmutableArray<StringToken> compiledTokens = tool.StringGlyphSyntax.Compile(uncompiledEntry.Tokens);
				compiledEntries.Add(new(uncompiledEntry.Index, compiledTokens));
			}

			Directory.CreateDirectory(Path.GetDirectoryName(compiledPath)!);

			using FileStream file = File.Open(compiledPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
			tool.CompiledStringTableEncoding.Encode(file, [..compiledEntries]);
		} catch (Exception e) {
			Console.Error.WriteLine($"\nError while compiling {uncompiledPath}: {e}");
			return false;
		}
		return true;
	}

	static async Task<bool> DecompileScriptPackage(Tool tool, string compiledScriptPackageName) {
		string uncompiledScriptName = Path.ChangeExtension(compiledScriptPackageName, ".scs");
		string uncompiledStringTableName = Path.ChangeExtension(compiledScriptPackageName, ".sct");
		string sdbName = Path.ChangeExtension(compiledScriptPackageName, ".sdb");

		string compiledScriptPackagePath = Path.Join(tool.CompiledDirectory, compiledScriptPackageName);
		string uncompiledScriptPath = Path.Join(tool.UncompiledDirectory, uncompiledScriptName);
		string uncompiledStringTablePath = Path.Join(tool.UncompiledDirectory, uncompiledStringTableName);
		string sdbPath = Path.Join(tool.UncompiledDirectory, sdbName);

		try {
			CompiledScriptPackage compiledScriptPackage = await DecodeCompiledScriptPackage(tool, compiledScriptPackagePath);

			ScriptDecompiler decompiler = new(tool.InstructionEncoding, compiledScriptPackage.Script);
			(ImmutableArray<UncompiledScriptElement> uncompiledScriptElements, ImmutableDictionary<UncompiledScriptElementInstruction, int> instructionPositions) = decompiler.Decompile();

			ImmutableArray<ImmutableArray<StringToken>> compiledStrings = compiledScriptPackage.Strings;
			List<StringTableEntry> uncompiledStringEntries = [];
			for (int i = 0; i < compiledStrings.Length; i++) {
				ImmutableArray<StringToken> uncompiledTokens = tool.StringGlyphSyntax.Decompile(compiledStrings[i]);
				uncompiledStringEntries.Add(new(i, uncompiledTokens));
			}

			Directory.CreateDirectory(Path.GetDirectoryName(uncompiledScriptPath)!);

			List<Exception> exceptions = [];
			try {
				StringBuilder builder = new();
				tool.UncompiledStringTableSyntax.Format(builder, [..uncompiledStringEntries]);
				await File.WriteAllTextAsync(uncompiledStringTablePath, builder.ToString(), new UTF8Encoding(false, true));
			} catch (Exception e) {
				exceptions.Add(e);
			}
			try {
				StringBuilder builder = new();
				StringBuilder? sdbBuilder = null;
				if (tool.GenerateSdb) {
					sdbBuilder = new();
				}
				UncompiledScriptSyntax.Format(builder, sdbBuilder, instructionPositions, uncompiledScriptElements);
				await File.WriteAllTextAsync(uncompiledScriptPath, builder.ToString(), new UTF8Encoding(false, true));
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
			Console.Error.WriteLine($"\nError while decompiling {compiledScriptPackagePath}: {e}");
			return false;
		}
		return true;
	}

	static async Task<bool> DecompileStringTable(Tool tool, string uncompiledName) {
		string compiledName = Path.ChangeExtension(uncompiledName, ".mst");

		string uncompiledPath = Path.Join(tool.CompiledDirectory, uncompiledName);
		string compiledPath = Path.Join(tool.UncompiledDirectory, compiledName);

		try {
			ImmutableArray<StringTableEntry> compiledEntries = await DecodeCompiledStringTable(tool, uncompiledPath);

			List<StringTableEntry> uncompiledEntries = [];
			for (int i = 0; i < compiledEntries.Length; i++) {
				StringTableEntry compiledEntry = compiledEntries[i];
				ImmutableArray<StringToken> uncompiledTokens = tool.StringGlyphSyntax.Decompile(compiledEntry.Tokens);
				uncompiledEntries.Add(new(compiledEntry.Index, uncompiledTokens));
			}

			Directory.CreateDirectory(Path.GetDirectoryName(compiledPath)!);

			StringBuilder builder = new();
			tool.UncompiledStringTableSyntax.Format(builder, [..uncompiledEntries]);
			await File.WriteAllTextAsync(compiledPath, builder.ToString(), new UTF8Encoding(false, true));
		} catch (Exception e) {
			Console.Error.WriteLine($"\nError while decompiling {uncompiledPath}: {e}");
			return false;
		}
		return true;
	}

	static async Task<ImmutableArray<UncompiledScriptElement>> ParseUncompiledScript(Tool tool, string path) {
		TextStream stream = await ReadFileText(path);
		return UncompiledScriptSyntax.Parse(stream);
	}

	static async Task<ImmutableArray<StringTableEntry>> ParseUncompiledStringTable(Tool tool, string path) {
		TextStream stream = await ReadFileText(path);
		return tool.UncompiledStringTableSyntax.Parse(stream);
	}

	static async Task<CompiledScriptPackage> DecodeCompiledScriptPackage(Tool tool, string path) {
		MemoryStream stream = await ReadFileBytes(path);
		return tool.CompiledScriptPackageEncoding.Decode(stream);
	}

	static async Task<ImmutableArray<StringTableEntry>> DecodeCompiledStringTable(Tool tool, string path) {
		MemoryStream stream = await ReadFileBytes(path);
		return tool.CompiledStringTableEncoding.Decode(stream);
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
