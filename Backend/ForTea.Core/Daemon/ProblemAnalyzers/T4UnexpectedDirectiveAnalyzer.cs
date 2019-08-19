using GammaJul.ForTea.Core.Daemon.Highlightings;
using GammaJul.ForTea.Core.Psi.Directives;
using GammaJul.ForTea.Core.Tree;
using JetBrains.ReSharper.Feature.Services.Daemon;

namespace GammaJul.ForTea.Core.Daemon.ProblemAnalyzers
{
	[ElementProblemAnalyzer(typeof(IT4Directive), HighlightingTypes =
		new[] {typeof(T4UnexpectedDirectiveHighlighting)})]
	public class T4UnexpectedDirectiveAnalyzer : ElementProblemAnalyzer<IT4Directive>
	{
		protected override void Run(
			IT4Directive element,
			ElementProblemAnalyzerData data,
			IHighlightingConsumer consumer
		)
		{
			if (T4DirectiveInfoManager.GetDirectiveByName(element.Name.GetText()) != null) return;
			var nameToken = element.Name;
			if (nameToken == null) return;
			consumer.AddHighlighting(new T4UnexpectedDirectiveHighlighting(nameToken));
		}
	}
}
