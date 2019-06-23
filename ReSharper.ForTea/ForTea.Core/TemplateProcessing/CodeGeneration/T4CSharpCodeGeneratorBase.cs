using System;
using System.Collections.Generic;
using System.Text;
using GammaJul.ForTea.Core.Psi;
using GammaJul.ForTea.Core.TemplateProcessing.CodeCollecting;
using GammaJul.ForTea.Core.Tree;
using JetBrains.Annotations;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.CSharp.Parsing;
using JetBrains.Util;

namespace GammaJul.ForTea.Core.TemplateProcessing.CodeGeneration
{
	internal abstract class T4CSharpCodeGeneratorBase
	{
		internal const string TransformTextMethodName = "TransformText";

		[NotNull]
		protected IT4File File { get; }

		[Obsolete("We should move most logic from code generator to intermediate result")]
		private T4CSharpCodeGenerationIntermediateResult Result { get; set; }

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
			Result = Collector.Collect();
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
			result.Append(Result.CollectedImports);
			AppendClass(result);
			AppendBaseClass(result.Builder);
		}

		private void AppendClass(T4CSharpCodeGenerationResult result)
		{
			var builder = result.Builder;
			AppendSyntheticAttribute(builder);
			builder.Append($"    public class {GeneratedClassName} : ");
			AppendBaseClassName(result);
			builder.AppendLine();
			builder.AppendLine("    {");
			if (Result.HasHost)
				builder.AppendLine(
					"        public virtual Microsoft.VisualStudio.TextTemplating.ITextTemplatingEngineHost Host { get; set; }");
			AppendTransformMethod(result);
			AppendParameterDeclarations(result, Collector.ParameterDescriptions);
			AppendInitialization(result, Collector.ParameterDescriptions);
			result.Append(Result.CollectedFeatures);
			builder.AppendLine("    }");
		}

		private void AppendParameterDeclarations(
			[NotNull] T4CSharpCodeGenerationResult result,
			[NotNull, ItemNotNull] IEnumerable<T4ParameterDescription> descriptions
		)
		{
			var builder = result.Builder;
			foreach (var description in descriptions)
			{
				builder.AppendLine("        [System.CodeDom.Compiler.GeneratedCodeAttribute]");
				builder.Append("        private ");
				var type = description.TypeToken;
				if (CSharpLexer.IsKeyword(type.GetText())) builder.Append('@');
				result.AppendMapped(type);
				builder.Append(' ');
				var name = description.NameToken;
				if (CSharpLexer.IsKeyword(name.GetText())) builder.Append('@');
				result.AppendMapped(name);
				builder.AppendLine(" { get; }");
			}
		}

		protected void AppendInitialization(
			[NotNull] T4CSharpCodeGenerationResult result,
			[NotNull, ItemNotNull] IReadOnlyCollection<T4ParameterDescription> descriptions
		)
		{
			if (descriptions.IsEmpty()) return;
			var builder = result.Builder;
			builder.AppendLine("        public void Initialize()");
			builder.AppendLine("        {");
			AppendParameterInitialization(descriptions, builder);
			builder.AppendLine("        }");
		}

		private void AppendTransformMethod(T4CSharpCodeGenerationResult result)
		{
			var builder = result.Builder;
			builder.Append("        public ");
			builder.Append(Result.HasBaseClass ? "override" : "virtual");
			builder.AppendLine($" string {TransformTextMethodName}()");
			builder.AppendLine("        {");
			result.Append(Result.CollectedTransformation);
			builder.AppendLine();
			builder.AppendLine("            return GenerationEnvironment.ToString();");
			builder.AppendLine("        }");
		}

		private void AppendBaseClassName(T4CSharpCodeGenerationResult result)
		{
			if (Result.HasBaseClass) result.Append(Result.CollectedBaseClass);
			else result.Builder.Append(GeneratedBaseClassName);
		}

		private void AppendBaseClass(StringBuilder builder)
		{
			if (Result.HasBaseClass) return;
			var provider = new T4TemplateBaseProvider(ResourceName);
			builder.AppendLine(provider.CreateTemplateBase(GeneratedBaseClassName));
		}

		[NotNull]
		protected abstract string ResourceName { get; }

		[NotNull]
		protected abstract string GeneratedClassName { get; }

		[NotNull]
		protected abstract string GeneratedBaseClassName { get; }

		protected abstract void AppendSyntheticAttribute([NotNull] StringBuilder builder);

		[NotNull]
		protected abstract T4CSharpCodeGenerationInfoCollectorBase Collector { get; }

		protected abstract void AppendParameterInitialization(
			[NotNull, ItemNotNull] IReadOnlyCollection<T4ParameterDescription> descriptions,
			[NotNull] StringBuilder builder);

		/// <summary>Initializes a new instance of the <see cref="T4CSharpCodeGeneratorBase"/> class.</summary>
		/// <param name="file">The associated T4 file whose C# code behind will be generated.</param>
		protected T4CSharpCodeGeneratorBase([NotNull] IT4File file) => File = file;
	}
}
