using System.Collections.Immutable;
using System.Text;

namespace MagesScriptTool;

sealed class StringGlyphSyntax {
	readonly ImmutableTree<Rune, GlyphSpec> _normalRuneTree;
	readonly ImmutableTree<Rune, GlyphSpec> _italicRuneTree;
	readonly ImmutableTree<int, GlyphSpec> _unitTree;

	StringGlyphSyntax(ImmutableTree<Rune, GlyphSpec> normalRuneTree, ImmutableTree<Rune, GlyphSpec> italicRuneTree, ImmutableTree<int, GlyphSpec> unitTree) {
		_normalRuneTree = normalRuneTree;
		_italicRuneTree = italicRuneTree;
		_unitTree = unitTree;
	}

	public ImmutableArray<StringToken> Compile(ImmutableArray<StringToken> uncompiled) {
		List<StringToken> compiled = [];
		int index = 0;
		while (index < uncompiled.Length) {
			StringToken token = uncompiled[index];
			switch (token) {
				case StringTokenRune rune: {
					GlyphStyle style = rune.Style ?? throw new ArgumentException("Cannot compile rune without style.");
					GlyphSpec? match = null;
					int matchEnd = default;
					ImmutableTree<Rune, GlyphSpec> cursor = style switch {
						GlyphStyle.Normal => _normalRuneTree,
						GlyphStyle.Italic => _italicRuneTree,
						_ => throw new NotImplementedException(style.ToString()),
					};
					int subIndex = index;
					while (subIndex < uncompiled.Length && cursor.HasBranches) {
						if (uncompiled[subIndex++] is not StringTokenRune subRune) {
							break;
						}
						ImmutableTree<Rune, GlyphSpec>? next = cursor[subRune.Value];
						if (next is null) {
							break;
						}
						cursor = next;
						if (cursor.Value is GlyphSpec spec) {
							match = spec;
							matchEnd = subIndex;
						}
					}
					if (match is null) {
						throw new Exception($"No {style} glyph available for '{rune.Value}' (U+{rune.Value.Value:X04}).");
					}
					index = matchEnd;
					foreach (int unit in match.Units) {
						compiled.Add(new StringTokenUnit(unit));
					}
					break;
				}
				case StringTokenUnit unit: {
					index++;
					compiled.Add(unit);
					break;
				}
				case StringTokenTag tag: {
					index++;
					compiled.Add(tag);
					break;
				}
				default: {
					throw new NotImplementedException(token.GetType().Name);
				}
			}
		}
		return [..compiled];
	}

	public ImmutableArray<StringToken> Decompile(ImmutableArray<StringToken> compiled) {
		List<StringToken> uncompiled = [];
		int index = 0;
		while (index < compiled.Length) {
			StringToken token = compiled[index];
			switch (token) {
				case StringTokenUnit unit: {
					GlyphSpec? match = null;
					int matchEnd = default;
					ImmutableTree<int, GlyphSpec> cursor = _unitTree;
					int subIndex = index;
					while (subIndex < compiled.Length && cursor.HasBranches) {
						if (compiled[subIndex++] is not StringTokenUnit subUnit) {
							break;
						}
						ImmutableTree<int, GlyphSpec>? next = cursor[subUnit.Value];
						if (next is null) {
							break;
						}
						cursor = next;
						if (cursor.Value is GlyphSpec spec) {
							match = spec;
							matchEnd = subIndex;
						}
					}
					if (match is null) {
						index++;
						uncompiled.Add(unit);
						break;
					}
					index = matchEnd;
					foreach (Rune rune in match.Text.EnumerateRunes()) {
						uncompiled.Add(new StringTokenRune(rune, match.Style));
					}
					break;
				}
				case StringTokenTag tag: {
					index++;
					uncompiled.Add(tag);
					break;
				}
				default: {
					throw new NotImplementedException(token.GetType().Name);
				}
			}
		}
		return [..uncompiled];
	}

	public static StringGlyphSyntax BuildFrom(ImmutableArray<GlyphSpec> glyphSpecs) {
		ImmutableTree<Rune, GlyphSpec> normalRuneTree = BuildRuneTree(glyphSpecs, GlyphStyle.Normal);
		ImmutableTree<Rune, GlyphSpec> italicRuneTree = BuildRuneTree(glyphSpecs, GlyphStyle.Italic);
		ImmutableTree<int, GlyphSpec> unitTree = BuildUnitTree(glyphSpecs);
		return new(normalRuneTree, italicRuneTree, unitTree);
	}

	static ImmutableTree<Rune, GlyphSpec> BuildRuneTree(ImmutableArray<GlyphSpec> specs, GlyphStyle style) {
		Tree<Rune, GlyphSpec> tree = new();
		foreach (GlyphSpec spec in specs) {
			if (spec.Style != null && spec.Style != style) {
				continue;
			}
			Tree<Rune, GlyphSpec> cursor = tree;
			foreach (Rune rune in spec.Text.EnumerateRunes()) {
				cursor = cursor.EnsureBranch(rune);
			}
			if (cursor.HasValue) {
				throw new Exception($"Duplicate {style} glyph text: '{spec.Text}'.");
			}
			cursor.Value = spec;
		}
		return tree.ToImmutableTree();
	}

	static ImmutableTree<int, GlyphSpec> BuildUnitTree(ImmutableArray<GlyphSpec> specs) {
		Tree<int, GlyphSpec> tree = new();
		foreach (GlyphSpec spec in specs) {
			Tree<int, GlyphSpec> cursor = tree;
			foreach (int unit in spec.Units) {
				cursor = cursor.EnsureBranch(unit);
			}
			if (cursor.HasValue) {
				throw new Exception($"Duplicate glyph unit sequence: [{string.Join(", ", spec.Units)}].");
			}
			cursor.Value = spec;
		}
		return tree.ToImmutableTree();
	}
}
