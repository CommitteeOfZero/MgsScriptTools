namespace MgsScriptTools;

class MesBakery {
	Tree<char, GlyphSpec> _regularTree = new();
	Tree<char, GlyphSpec> _italicTree = new();
	Dictionary<int, GlyphSpec> _table = new();

	public MesBakery(Tree<char, GlyphSpec> regularTree, Tree<char, GlyphSpec> italicTree, Dictionary<int, GlyphSpec> table) {
		_regularTree = regularTree;
		_italicTree = italicTree;
		_table = table;
	}

	public MesToken[] Bake(MesToken[] plain) {
		List<MesToken> baked = new();
		foreach (var token in plain)
			BakeToken(baked, token);
		return baked.ToArray();
	}

	public MesToken[] Unbake(MesToken[] tokens) {
		List<MesToken> parts = new();
		foreach (var token in tokens)
			parts.Add(UnbakeToken(token));
		return parts.ToArray();
	}

	void BakeToken(List<MesToken> tokens, MesToken token) {
		switch (token) {
			case MesChunk { Value: string chunk, Italic: bool italic }: {
				BakeChunk(tokens, chunk, italic);
				break;
			}
			case MesGlyph glyph: {
				tokens.Add(glyph);
				break;
			}
			case MesTag tag: {
				tokens.Add(tag);
				break;
			}
			default: {
				throw new NotImplementedException(token.GetType().Name);
			}
		}
	}

	void BakeChunk(List<MesToken> tokens, string chunk, bool italic) {
		while (chunk.Length > 0) {
			GlyphSpec? longestMatch = null;
			int maxLength = 0;

			int index = 0;
			Tree<char, GlyphSpec> cursor = italic ? _italicTree : _regularTree;
			while (index < chunk.Length && cursor.HasBranches) {
				var next = cursor[chunk[index++]];
				if (next is null)
					break;
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
				throw new Exception($"No {style} glyph available for {ch} (U+{(int)ch:X04})");
			}
			tokens.Add(new MesGlyph(longestMatch.Index));
			chunk = chunk[maxLength..];
		}
	}

	MesToken UnbakeToken(MesToken token) {
		switch (token) {
			case MesGlyph glyph: {
				var spec = _table.GetValueOrDefault(glyph.Value);
				if (spec is null)
					return glyph;
				// TODO: combine multiple glyphs into one chunk
				return new MesChunk(spec.Text, !spec.Regular && spec.Italic);
			}
			case MesTag tag: {
				return tag;
			}
			default: {
				throw new NotImplementedException(token.GetType().Name);
			}
		}
	}

	public static MesBakery BuildFrom(GlyphSpec[] glyphSpecs) {
		Tree<char, GlyphSpec> regularTree = BuildGlyphTree(glyphSpecs, italic: false);
		Tree<char, GlyphSpec> italicTree = BuildGlyphTree(glyphSpecs, italic: true);
		Dictionary<int, GlyphSpec> table = new();
		foreach (var spec in glyphSpecs) {
			if (table.ContainsKey(spec.Index))
				throw new Exception($"Duplicate glyph index: {spec.Index:X04}");
			table[spec.Index] = spec;
		}
		return new(regularTree, italicTree, table);
	}

	static Tree<char, GlyphSpec> BuildGlyphTree(GlyphSpec[] specs, bool italic) {
		Tree<char, GlyphSpec> tree = new();
		foreach (var spec in specs) {
			if ((!italic && !spec.Regular) || (italic && !spec.Italic))
				continue;
			string text = spec.Text;
			if (text.Length == 0)
				throw new Exception($"Empty glyph text for index {spec.Index:X04}");
			var cursor = tree;
			for (int i = 0; i < text.Length; i++)
				cursor = cursor.CreateBranch(text[i]);
			if (cursor.HasValue) {
				string style = italic ? "italic" : "regular";
				throw new Exception($"Duplicate {style} glyph text: {text}");
			}
			cursor.Value = spec;
		}
		return tree;
	}
}
