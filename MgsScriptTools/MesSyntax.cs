using System.Text;

namespace MgsScriptTools;

public abstract class MesSyntax {
	public abstract void Stringify(StringBuilder builder, MesToken[] parts);
	public abstract MesToken[] Parse(TextStream reader);
}
