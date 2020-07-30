using GammaJul.ForTea.Core.Psi.Cache;
using GammaJul.ForTea.Core.TemplateProcessing;
using GammaJul.ForTea.Core.Tree;
using JetBrains.Annotations;
using JetBrains.DocumentManagers;
using JetBrains.Lifetimes;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.Files;

namespace JetBrains.ForTea.RiderPlugin.ProtocolAware.Highlighting.Impl
{
	[SolutionComponent]
	public sealed class T4OutputExtensionFrontendNotifier
	{
		[NotNull]
		private DocumentManager DocumentManager { get; }

		[NotNull]
		private IT4FileDependencyGraph Graph { get; }

		public T4OutputExtensionFrontendNotifier(
			Lifetime lifetime,
			[NotNull] IT4FileGraphNotifier notifier,
			[NotNull] DocumentManager documentManager,
			[NotNull] IT4FileDependencyGraph graph
		)
		{
			DocumentManager = documentManager;
			Graph = graph;
			notifier.OnFilesIndirectlyAffected.Advise(lifetime, NotifyFrontend);
		}

		private void NotifyFrontend(T4FileInvalidationData data)
		{
			NotifyFrontend(data.DirectlyAffectedFile);
			foreach (var file in data.IndirectlyAffectedFiles)
			{
				NotifyFrontend(file);
			}
		}

		public void NotifyFrontend([NotNull] IPsiSourceFile file)
		{
			var listener = CreateListener(file);
			if (listener == null) return;
			string extension = GetTargetExtension(file);
			if (extension == null) return;
			listener.ExtensionChanged(extension);
		}

		[CanBeNull]
		private string GetTargetExtension([NotNull] IPsiSourceFile file)
		{
			var root = Graph.FindBestRoot(file);
			var psi = root.GetPrimaryPsiFile();
			if (!(psi is IT4File t4File)) return null;
			string extension = t4File.GetTargetExtension();
			return extension;
		}

		[CanBeNull]
		private IT4OutputExtensionChangeListener CreateListener([NotNull] IPsiSourceFile file)
		{
			var projectFile = file.ToProjectFile();
			if (projectFile == null) return null;
			var document = DocumentManager.TryGetDocument(projectFile);
			return document?.GetListener();
		}
	}
}
