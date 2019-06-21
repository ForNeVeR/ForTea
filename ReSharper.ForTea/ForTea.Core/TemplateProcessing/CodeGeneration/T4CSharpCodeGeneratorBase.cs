using System;
using System.Text;
using GammaJul.ForTea.Core.Psi;
using GammaJul.ForTea.Core.Psi.Directives;
using GammaJul.ForTea.Core.Tree;
using JetBrains.Annotations;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.Psi;
using JetBrains.Util;

namespace GammaJul.ForTea.Core.TemplateProcessing.CodeGeneration
{
	internal abstract class T4CSharpCodeGeneratorBase
	{
		internal const string CodeCommentStart = "/*_T4\x200CCodeStart_*/";
		internal const string CodeCommentEnd = "/*_T4\x200CCodeEnd_*/";
		internal const string TransformTextMethodName = "TransformText";

		[Obsolete] internal const string DefaultBaseClassFullName =
			"Microsoft.VisualStudio.TextTemplating." + T4TemplateBaseProvider.DefaultBaseClassName;

		[NotNull]
		private IT4File File { get; }

		protected bool HasBaseClass => !Collector.InheritsResult.Builder.IsEmpty();
		private bool HasHost => Collector.HasHost;

		[NotNull]
		private T4CSharpCodeGenerationInfoCollector Collector { get; }

		/// <summary>Gets the namespace of the current T4 file. This is always <c>null</c> for a standard (non-preprocessed) file.</summary>
		/// <returns>A namespace, or <c>null</c>.</returns>
		[CanBeNull]
		private string GetNamespace()
		{
			IPsiSourceFile sourceFile = File.GetSourceFile();
			IProjectFile projectFile = sourceFile?.ToProjectFile();
			if (projectFile?.IsPreprocessedT4Template() != true)
				return null;

			string ns = projectFile.GetCustomToolNamespace();
			if (!String.IsNullOrEmpty(ns))
				return ns;

			return sourceFile.Properties.GetDefaultNamespace();
		}

		/// <summary>Generates a new C# code behind.</summary>
		/// <returns>An instance of <see cref="T4CSharpCodeGenerationResult"/> containing the C# file.</returns>
		[NotNull]
		public T4CSharpCodeGenerationResult Generate()
		{
			Collector.Collect();
			var result = new T4CSharpCodeGenerationResult(File);
			string ns = GetNamespace();
			bool hasNamespace = !string.IsNullOrEmpty(ns);
			if (hasNamespace)
			{
				result.Builder.AppendLine($"namespace {ns} {{");
				AppendNamespaceContents(result);
				result.Builder.AppendLine("}");
			}
			else
			{
				AppendNamespaceContents(result);
			}

			return result;
		}

		private void AppendNamespaceContents(T4CSharpCodeGenerationResult result)
		{
			result.Builder.AppendLine("using System;");
			result.Append(Collector.UsingsResult);
			AppendClass(result);
			AppendBaseClass(result.Builder);
		}

		private void AppendClass(T4CSharpCodeGenerationResult result)
		{
			StringBuilder builder = result.Builder;
			AppendSyntheticAttribute(builder);
			builder.Append($"    public class  : ");
			AppendBaseClassName(result);
			builder.AppendLine();
			builder.AppendLine($"    {{");
			if (HasHost)
				builder.AppendLine(
					"        public virtual Microsoft.VisualStudio.TextTemplating.ITextTemplatingEngineHost Host { get; set; }");
			result.Append(Collector.ParametersResult);
			builder.AppendLine($"        [System.CodeDom.Compiler.GeneratedCodeAttribute(\"Rider\", \"whatever\")]");
			AppendTransformMethod(result);
			result.Append(Collector.FeatureResult);
			builder.AppendLine($"    }}");
		}

		private void AppendTransformMethod(T4CSharpCodeGenerationResult result)
		{
			StringBuilder builder = result.Builder;
			builder.Append("        public ");
			builder.Append(HasBaseClass ? "override" : "virtual");
			builder.AppendLine($" string {TransformTextMethodName}()");
			builder.AppendLine($"        {{");
			result.Append(Collector.TransformTextResult);
			builder.AppendLine();
			builder.AppendLine($"            return GenerationEnvironment.ToString();");
			builder.AppendLine($"        }}");
		}

		private void AppendBaseClassName(T4CSharpCodeGenerationResult result)
		{
			if (HasBaseClass)
			{
				result.Append(Collector.InheritsResult);
			}
			else
			{
				result.Builder.Append(T4TemplateBaseProvider.DefaultBaseClassName);
			}
		}

		private void AppendBaseClass(StringBuilder builder)
		{
			if (HasBaseClass) return;
			var provider = new T4TemplateBaseProvider(ResourceName);
			builder.AppendLine(provider.CreateTemplateBase(T4TemplateBaseProvider.DefaultBaseClassName));
		}
		
		[NotNull]
		protected abstract string ResourceName { get; }

		protected abstract void AppendSyntheticAttribute([NotNull] StringBuilder builder);
		
		/// <summary>Initializes a new instance of the <see cref="T4CSharpCodeGeneratorBase"/> class.</summary>
		/// <param name="file">The associated T4 file whose C# code behind will be generated.</param>
		/// <param name="manager">An instance of <see cref="T4DirectiveInfoManager"/>.</param>
		protected T4CSharpCodeGeneratorBase(
			[NotNull] IT4File file,
			[NotNull] T4DirectiveInfoManager manager
		)
		{
			File = file;
			Collector = new T4CSharpCodeGenerationInfoCollector(file, manager);
		}
	}
}
