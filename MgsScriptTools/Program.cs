using System.Text;

using System.CommandLine;
using System.CommandLine.Parsing;

namespace MgsScriptTools;

class Program {
	enum StringSyntax {
		Sc3Tools,
		ScsStrict,
	}

	class CommandContext {
		public string CompiledDirectory = null!;
		public string DecompiledDirectory = null!;
		public MstSyntax MstSyntax = null!;
		public MstStringEncoding MstStringEncoding = null!;
		public MesStringEncoding MesStringEncoding = null!;
		public InstructionEncoding InstructionEncoding = null!;
	}

	static async Task<int> Main(string[] args) {
		var compiledDirectoryOption = new Option<string>(
			name: "--compiled-directory",
			description: "The path to the directory for the scripts in compiled form."
		) {
			IsRequired = true,
		};

		var decompiledDirectoryOption = new Option<string>(
			name: "--decompiled-directory",
			description: "The path to the directory for the scripts in decompiled form."
		) {
			IsRequired = true,
		};

		var bankDirectoryOption = new Option<string>(
			name: "--bank-directory",
			description: "The path to the specifications bank directory."
		) {
			IsRequired = true,
		};

		var flagSetOption = new Option<string>(
			name: "--flag-set",
			description: "The flag set to be used for selecting data to be loaded from the specifications bank."
		) {
			IsRequired = true,
		};

		var instructionSetsOption = new Option<string>(
			name: "--instruction-sets",
			description: "The instruction sets (comma-delimited) to be loaded from the specifications bank."
		) {
			IsRequired = true,
		};

		var charsetOption = new Option<string>(
			name: "--charset",
			description: "The charset to be loaded from the specifications bank."
		) {
			IsRequired = true,
		};

		var stringSyntaxOption = new Option<StringSyntax>(
			name: "--string-syntax",
			description: "The string syntax to be used for parsing and building string list files."
		) {
			IsRequired = true,
		};

		var rootCommand = new RootCommand("A tool for compilation and decompilation of MAGES. engine scripts");
		rootCommand.AddGlobalOption(compiledDirectoryOption);
		rootCommand.AddGlobalOption(decompiledDirectoryOption);
		rootCommand.AddGlobalOption(bankDirectoryOption);
		rootCommand.AddGlobalOption(flagSetOption);
		rootCommand.AddGlobalOption(instructionSetsOption);
		rootCommand.AddGlobalOption(charsetOption);
		rootCommand.AddGlobalOption(stringSyntaxOption);

		CommandContext ParseOptions(ParseResult result) {
			var compiledDirectory = result.GetValueForOption(compiledDirectoryOption)!;
			var decompiledDirectory = result.GetValueForOption(decompiledDirectoryOption)!;
			var bankDirectory = result.GetValueForOption(bankDirectoryOption)!;
			var flagSet = result.GetValueForOption(flagSetOption)!;
			var instructionSets = result.GetValueForOption(instructionSetsOption)!.Split(",");
			var charsetName = result.GetValueForOption(charsetOption)!;
			var stringSyntax = result.GetValueForOption(stringSyntaxOption)!;

			var bank = SpecBank.Load(bankDirectory);
			var flags = bank.GetFlags(flagSet);

			var opcodeSpecs = bank.GetOpcodeSpecs(instructionSets, flags);
			var instructionEncoding = InstructionEncoding.BuildFrom(opcodeSpecs);

			MstStringSyntax mstStringSyntax = stringSyntax switch {
				StringSyntax.Sc3Tools => new Sc3ToolsSyntax(),
				StringSyntax.ScsStrict => new ScsStrictSyntax(),
				_ => throw new NotImplementedException(stringSyntax.ToString()),
			};
			MstSyntax mstSyntax = new(mstStringSyntax);

			var glyphSpecs = bank.GetGlyphSpecs(charsetName);
			var mstStringEncoding = MstStringEncoding.BuildFrom(glyphSpecs);

			MesStringSpec mesStringSpec = new();
			MesStringEncoding mesStringEncoding = new(mesStringSpec);

			return new CommandContext {
				CompiledDirectory = compiledDirectory,
				DecompiledDirectory = decompiledDirectory,
				InstructionEncoding = instructionEncoding,
				MstSyntax = mstSyntax,
				MstStringEncoding = mstStringEncoding,
				MesStringEncoding = mesStringEncoding,
			};
		}

		var compileCommand = new Command("compile", "Compile scripts from <decompiled-directory> to <compiled-directory>");
		compileCommand.SetHandler(async context => {
			try {
				var config = ParseOptions(context.ParseResult);
				context.ExitCode = await DoCompileCommand(config);
			} catch (Exception e) {
				context.ExitCode = 1;
				Console.Error.WriteLine(e);
			}
		});
		rootCommand.AddCommand(compileCommand);

		var decompileCommand = new Command("decompile", "Decompile scripts from <compiled-directory> to <decompiled-directory>");
		decompileCommand.SetHandler(async context => {
			try {
				var config = ParseOptions(context.ParseResult);
				context.ExitCode = await DoDecompileCommand(config);
			} catch (Exception e) {
				context.ExitCode = 1;
				Console.Error.WriteLine(e);
			}
		});
		rootCommand.AddCommand(decompileCommand);

		return await rootCommand.InvokeAsync(args);
	}

	static async Task<int> DoCompileCommand(CommandContext config) {
		var inputDir = config.DecompiledDirectory;
		IEnumerable<string> paths;
		try {
			paths = Directory.EnumerateFiles(inputDir, "*", SearchOption.AllDirectories);
		} catch (Exception e) {
			Console.Error.WriteLine($"Error: {e}");
			return 1;
		}
		bool errorOccurred = false;
		foreach (var inputFile in paths) {
			string path = Path.GetRelativePath(inputDir, inputFile);
			if (Path.IsPathRooted(path))
				continue;
			switch (Path.GetExtension(inputFile)) {
				case ".scs": {
					errorOccurred |= await CompileSc3(config, path) != 0;
					break;
				}
				case ".mst": {
					errorOccurred |= await CompileMes(config, path) != 0;
					break;
				}
			}
		}
		return errorOccurred ? 1 : 0;
	}

	static async Task<int> DoDecompileCommand(CommandContext context) {
		var inputDir = context.CompiledDirectory;
		IEnumerable<string> paths;
		try {
			paths = Directory.EnumerateFiles(inputDir, "*", SearchOption.AllDirectories);
		} catch (Exception e) {
			Console.Error.WriteLine($"Error: {e}");
			return 1;
		}
		bool errorOccurred = false;
		foreach (var inputFile in paths) {
			string path = Path.GetRelativePath(inputDir, inputFile);
			if (Path.IsPathRooted(path))
				continue;
			switch (Path.GetExtension(inputFile)) {
				case ".scx": {
					errorOccurred |= await DecompileSc3(context, path) != 0;
					break;
				}
				case ".msb": {
					errorOccurred |= await DecompileMes(context, path) != 0;
					break;
				}
			}
		}
		return errorOccurred ? 1 : 0;
	}

	static async Task<int> CompileSc3(CommandContext context, string srcName) {
		string sctName = Path.ChangeExtension(srcName, ".sct");
		string dstName = Path.ChangeExtension(srcName, ".scx");

		string srcPath = Path.Join(context.DecompiledDirectory, srcName);
		string sctPath = Path.Join(context.DecompiledDirectory, sctName);
		string dstPath = Path.Join(context.CompiledDirectory, dstName);

		string dstDir = Path.GetDirectoryName(dstPath)!;

		try {
			var srcParts = await ParseScs(context, srcPath);
			var srcStrings = await ParseMst(context, sctPath);

			ScsCompiler compiler = new(context.InstructionEncoding);
			var dstFile = compiler.Compile(srcParts);

			var dstStrings = new byte[srcStrings.Length][];
			for (int i = 0; i < srcStrings.Length; i++) {
				var entry = srcStrings[i];
				if (entry.Index != i)
					throw new Exception($"Missing string with index {i}");
				var tokens = context.MstStringEncoding.Encode(entry.Parts);
				MemoryStream stream = new();
				context.MesStringEncoding.Encode(stream, tokens);
				dstStrings[i] = stream.ToArray();
			}
			dstFile.Strings = dstStrings;

			Directory.CreateDirectory(dstDir);

			using var file = File.Open(dstPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
			Sc3File.Encode(file, dstFile);
		} catch (Exception e) {
			Console.Error.WriteLine($"\nError while compiling {srcPath}: {e}");
			return 1;
		}

		return 0;
	}

	static async Task<int> CompileMes(CommandContext context, string srcName) {
		string dstName = Path.ChangeExtension(srcName, ".msb");

		string srcPath = Path.Join(context.DecompiledDirectory, srcName);
		string dstPath = Path.Join(context.CompiledDirectory, dstName);

		string dstDir = Path.GetDirectoryName(dstPath)!;

		try {
			var srcStrings = await ParseMst(context, srcPath);

			var dstStrings = new MesFileEntry[srcStrings.Length];
			for (int i = 0; i < srcStrings.Length; i++) {
				var entry = srcStrings[i];
				var tokens = context.MstStringEncoding.Encode(entry.Parts);
				MemoryStream stream = new();
				context.MesStringEncoding.Encode(stream, tokens);
				dstStrings[i] = new MesFileEntry {
					Index = entry.Index,
					String = stream.ToArray(),
				};
			}

			Directory.CreateDirectory(dstDir);

			using var file = File.Open(dstPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
			MesFile.Encode(file, new MesFile {
				Entries = dstStrings,
			});
		} catch (Exception e) {
			Console.Error.WriteLine($"\nError while compiling {srcPath}: {e}");
			return 1;
		}

		return 0;
	}

	static async Task<int> DecompileSc3(CommandContext context, string srcName) {
		string dstName = Path.ChangeExtension(srcName, ".scs");
		string sctName = Path.ChangeExtension(srcName, ".sct");

		string srcPath = Path.Join(context.CompiledDirectory, srcName);
		string dstPath = Path.Join(context.DecompiledDirectory, dstName);
		string sctPath = Path.Join(context.DecompiledDirectory, sctName);

		string dstDir = Path.GetDirectoryName(dstPath)!;

		try {
			var srcFile = await DecodeSc3(context, srcPath);

			ScsDecompiler decompiler = new(srcFile, context.InstructionEncoding);
			var dstParts = decompiler.Decompile();

			var srcStrings = srcFile.Strings;
			var dstStrings = new MstEntry[srcStrings.Length];
			for (int i = 0; i < srcStrings.Length; i++) {
				MemoryStream stream = new(srcStrings[i]);
				var tokens = context.MesStringEncoding.Decode(stream);
				var parts = context.MstStringEncoding.Decode(tokens);
				dstStrings[i] = new MstEntry(i, parts);
			}

			Directory.CreateDirectory(dstDir);

			List<Exception> exceptions = new();
			try {
				StringBuilder builder = new();
				context.MstSyntax.Stringify(builder, dstStrings);
				await File.WriteAllTextAsync(sctPath, builder.ToString(), new UTF8Encoding(false));
			} catch (Exception e) {
				exceptions.Add(e);
			}
			try {
				StringBuilder builder = new();
				ScsSyntax.Stringify(builder, dstParts);
				await File.WriteAllTextAsync(dstPath, builder.ToString(), new UTF8Encoding(false));
			} catch (Exception e) {
				exceptions.Add(e);
			}
			if (exceptions.Count > 0)
				throw new AggregateException(exceptions);
		} catch (Exception e) {
			Console.Error.WriteLine($"\nError while decompiling {srcPath}: {e}");
			return 1;
		}

		return 0;
	}

	static async Task<int> DecompileMes(CommandContext context, string srcName) {
		string dstName = Path.ChangeExtension(srcName, ".mst");

		string srcPath = Path.Join(context.CompiledDirectory, srcName);
		string dstPath = Path.Join(context.DecompiledDirectory, dstName);

		string dstDir = Path.GetDirectoryName(dstPath)!;

		try {
			var srcFile = await DecodeMes(context, srcPath);

			var srcStrings = srcFile.Entries;
			var dstStrings = new MstEntry[srcStrings.Length];
			for (int i = 0; i < srcStrings.Length; i++) {
				var entry = srcStrings[i];
				MemoryStream stream = new(entry.String);
				var tokens = context.MesStringEncoding.Decode(stream);
				var parts = context.MstStringEncoding.Decode(tokens);
				dstStrings[i] = new MstEntry(entry.Index, parts);
			}

			Directory.CreateDirectory(dstDir);

			StringBuilder builder = new();
			context.MstSyntax.Stringify(builder, dstStrings);
			await File.WriteAllTextAsync(dstPath, builder.ToString(), new UTF8Encoding(false));
		} catch (Exception e) {
			Console.Error.WriteLine($"\nError while decompiling {srcPath}: {e}");
			return 1;
		}

		return 0;
	}

	static async Task<ScsPart[]> ParseScs(CommandContext context, string path) {
		TextStream stream = await ReadFileText(path);
		return ScsSyntax.Parse(stream);
	}

	static async Task<MstEntry[]> ParseMst(CommandContext context, string path) {
		TextStream stream = await ReadFileText(path);
		return context.MstSyntax.Parse(stream);
	}

	static async Task<Sc3File> DecodeSc3(CommandContext context, string path) {
		MemoryStream stream = await ReadFileBytes(path);
		return Sc3File.Decode(stream);
	}

	static async Task<MesFile> DecodeMes(CommandContext context, string path) {
		MemoryStream stream = await ReadFileBytes(path);
		return MesFile.Decode(stream);
	}

	static async Task<TextStream> ReadFileText(string path) {
		var data = await File.ReadAllTextAsync(path, Encoding.UTF8);
		return new(data);
	}

	static async Task<MemoryStream> ReadFileBytes(string path) {
		var data = await File.ReadAllBytesAsync(path);
		return new(data);
	}
}
