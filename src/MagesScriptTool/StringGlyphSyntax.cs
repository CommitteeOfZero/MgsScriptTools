using System.Collections.Immutable;

namespace MagesScriptTool;

sealed class StringGlyphSyntax {
	readonly ImmutableTree<char, GlyphSpec> _regularTree;
	readonly ImmutableTree<char, GlyphSpec> _italicTree;
	readonly Dictionary<int, GlyphSpec> _table;

	public StringGlyphSyntax(ImmutableTree<char, GlyphSpec> regularTree, ImmutableTree<char, GlyphSpec> italicTree, Dictionary<int, GlyphSpec> table) {
		_regularTree = regularTree;
		_italicTree = italicTree;
		_table = table;
	}

	public ImmutableArray<StringToken> Compile(ImmutableArray<StringToken> uncompiled) {
		List<StringToken> compiled = [];
		foreach (StringToken token in uncompiled) {
			CompileToken(compiled, token);
		}
		return [..compiled];
	}

	public ImmutableArray<StringToken> Decompile(ImmutableArray<StringToken> compiled) {
		List<StringToken> uncompiled = [];
		foreach (StringToken token in compiled) {
			uncompiled.Add(DecompileToken(token));
		}
		return [..uncompiled];
	}

	void CompileToken(List<StringToken> result, StringToken token) {
		switch (token) {
			case StringTokenChunk { Value: string chunk, Italic: bool italic }: {
				CompileChunk(result, chunk, italic);
				break;
			}
			case StringTokenGlyph glyph: {
				result.Add(glyph);
				break;
			}
			case StringTokenTag tag: {
				result.Add(tag);
				break;
			}
			default: {
				throw new NotImplementedException(token.GetType().Name);
			}
		}
	}

	void CompileChunk(List<StringToken> tokens, string chunk, bool italic) {
		while (chunk.Length > 0) {
			GlyphSpec? longestMatch = null;
			int maxLength = 0;

			int index = 0;
			ImmutableTree<char, GlyphSpec> cursor = italic ? _italicTree : _regularTree;
			while (index < chunk.Length && cursor.HasBranches) {
				ImmutableTree<char, GlyphSpec>? next = cursor[chunk[index++]];
				if (next is null) {
					break;
				}
				cursor = next;
				if (cursor.Value is GlyphSpec spec) {
					if ((!italic && spec.Regular) || (italic && spec.Italic)) {
						longestMatch = spec;
						maxLength = index;
					}
				}
			}

			if (longestMatch is null) {
				string style = italic ? "italic" : "regular";
				char ch = chunk[0];
				throw new Exception($"No {style} glyph available for {ch} (U+{(int)ch:X04}).");
			}
			tokens.Add(new StringTokenGlyph(longestMatch.Index));
			chunk = chunk[maxLength..];
		}
	}

	StringToken DecompileToken(StringToken token) {
		switch (token) {
			case StringTokenGlyph glyph: {
				GlyphSpec? spec = _table.GetValueOrDefault(glyph.Value);
				if (spec is null) {
					return glyph;
				}
				// TODO: combine multiple glyphs into one chunk
				return new StringTokenChunk(spec.Text, !spec.Regular && spec.Italic);
			}
			case StringTokenTag tag: {
				return tag;
			}
			default: {
				throw new NotImplementedException(token.GetType().Name);
			}
		}
	}

	public static StringGlyphSyntax BuildFrom(ImmutableArray<GlyphSpec> glyphSpecs) {
		ImmutableTree<char, GlyphSpec> regularTree = BuildGlyphTree(glyphSpecs, italic: false);
		ImmutableTree<char, GlyphSpec> italicTree = BuildGlyphTree(glyphSpecs, italic: true);
		Dictionary<int, GlyphSpec> table = [];
		foreach (GlyphSpec spec in glyphSpecs) {
			if (table.ContainsKey(spec.Index)) {
				throw new Exception($"Duplicate glyph index: {spec.Index:X04}.");
			}
			table[spec.Index] = spec;
		}
		return new(regularTree, italicTree, table);
	}

	static ImmutableTree<char, GlyphSpec> BuildGlyphTree(ImmutableArray<GlyphSpec> specs, bool italic) {
		Tree<char, GlyphSpec> tree = new();
		foreach (GlyphSpec spec in specs) {
			if ((!italic && !spec.Regular) || (italic && !spec.Italic)) {
				continue;
			}
			string text = spec.Text;
			if (text.Length == 0) {
				throw new Exception($"Empty glyph text for index {spec.Index:X04}.");
			}
			Tree<char, GlyphSpec> cursor = tree;
			for (int i = 0; i < text.Length; i++) {
				cursor = cursor.EnsureBranch(text[i]);
			}
			if (cursor.HasValue) {
				string style = italic ? "italic" : "regular";
				throw new Exception($"Duplicate {style} glyph text: {text}.");
			}
			cursor.Value = spec;
		}
		return tree.ToImmutableTree();
	}
}
