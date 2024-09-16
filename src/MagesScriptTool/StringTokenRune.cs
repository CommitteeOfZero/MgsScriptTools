using System.Text;

namespace MagesScriptTool;

sealed class StringTokenRune : StringToken {
	public readonly Rune Value;
	public readonly GlyphStyle? Style;

	public StringTokenRune(Rune value, GlyphStyle? style) {
		Value = value;
		Style = style;
	}
}
