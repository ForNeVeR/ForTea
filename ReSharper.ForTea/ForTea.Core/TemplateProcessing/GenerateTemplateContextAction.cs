using System;
using System.IO;
using GammaJul.ForTea.Core.Psi.Directives;
using GammaJul.ForTea.Core.Tree;
using JetBrains.Annotations;
using JetBrains.Application.Progress;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.Feature.Services.ContextActions;
using JetBrains.ReSharper.Psi;
using JetBrains.TextControl;
using JetBrains.Util;
using static GammaJul.ForTea.Core.TemplateProcessing.T4CSharpCodeGenerationUtils;

namespace GammaJul.ForTea.Core.TemplateProcessing
{
	[ContextAction(
		Name = "ExecuteTemplate",
		Description = Message,
		Group = "T4",
		Disabled = false,
		Priority = 1)]
	public class GenerateTemplateContextAction : ContextActionBase
	{
		[NotNull] private const string Message = "Execute T4 design-time template";
		private IT4File File { get; }

		public GenerateTemplateContextAction([NotNull] LanguageIndependentContextActionDataProvider dataProvider) =>
			File = dataProvider.PsiFile as IT4File;

		[NotNull]
		private string GetDestinationFilePath()
		{
			IPsiSourceFile initialPsiSourceFile = File.GetSourceFile();
			IProjectFile initialProjectFile = initialPsiSourceFile.ToProjectFile();
			string newFileName = initialPsiSourceFile?.Name.WithExtension(DefaultTargetExtension);
			return initialProjectFile?.Location.Parent.Combine(newFileName).FullPath
			       ?? throw new NullReferenceException();
		}

		private static void WriteData(string newFilePath, string message)
		{
			using (var stream = System.IO.File.Create(newFilePath))
			using (var writer = new StreamWriter(stream))
			{
				writer.Write(message);
			}
		}

		protected override Action<ITextControl> ExecutePsiTransaction(
			ISolution solution,
			IProgressIndicator progress
		) => textControl =>
		{
			var manager = solution.GetComponent<T4DirectiveInfoManager>();
			var provider = solution.GetComponent<T4TemplateBaseProvider>();

			string newFilePath = GetDestinationFilePath();
			var generator = new T4CSharpCodeGenerator(File, manager, provider, false);
			string message = generator.Generate().Builder.ToString();

			WriteData(newFilePath, message);
		};

		public override string Text => Message;
		public override bool IsAvailable(IUserDataHolder cache) => File != null;
	}
}
