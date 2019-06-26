using GammaJul.ForTea.Core.Psi.Directives;
using GammaJul.ForTea.Core.TemplateProcessing.CodeCollecting;
using GammaJul.ForTea.Core.TemplateProcessing.CodeGeneration.Converters;
using GammaJul.ForTea.Core.Tree;
using JetBrains.Annotations;
using JetBrains.ForTea.RdSupport.TemplateProcessing.CodeGeneration.Converters;

namespace JetBrains.ForTea.RdSupport.TemplateProcessing.CodeGeneration.Generators
{
	public sealed class T4CSharpInteractiveCodeGenerator : T4CSharpCodeGenerator
	{
		public T4CSharpInteractiveCodeGenerator(
			[NotNull] IT4File file,
			[NotNull] T4DirectiveInfoManager manager
		) : base(file, manager)
		{
		}

		protected override T4CSharpIntermediateConverterBase CreateConverter(
			T4CSharpCodeGenerationIntermediateResult intermediateResult
		) => new T4CSharpInteractiveIntermediateConverter(intermediateResult, File);
	}
}
