using System.Collections.Immutable;

namespace MagesScriptTool;

class GlyphSpec {
	public readonly ImmutableArray<int> Units;
	public readonly string Text;
	public readonly GlyphStyle? Style;

	public GlyphSpec(ImmutableArray<int> units, string text, GlyphStyle? style) {
		if (units.Length == 0) {
			throw new ArgumentException("Unit sequence must not be empty.", nameof(units));
		}
		if (text.Length == 0) {
			throw new ArgumentException("Text must not be empty.", nameof(text));
		}
		Units = units;
		Text = text;
		Style = style;
	}
}
