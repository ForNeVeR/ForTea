using System;
using System.Collections.Generic;
using System.Linq;
using GammaJul.ForTea.Core.Parsing;
using GammaJul.ForTea.Core.Psi.Directives;
using GammaJul.ForTea.Core.Psi.FileType;
using GammaJul.ForTea.Core.Tree;
using JetBrains.Annotations;
using JetBrains.Application.BuildScript.Application.Zones;
using JetBrains.Application.Settings;
using JetBrains.Diagnostics;
using JetBrains.DocumentModel;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.CSharp;
using JetBrains.ReSharper.Psi.CSharp.CodeStyle;
using JetBrains.ReSharper.Psi.CSharp.CodeStyle.Settings;
using JetBrains.ReSharper.Psi.CSharp.Impl.CustomHandlers;
using JetBrains.ReSharper.Psi.CSharp.Parsing;
using JetBrains.ReSharper.Psi.CSharp.Tree;
using JetBrains.ReSharper.Psi.ExtensionsAPI.Resolve;
using JetBrains.ReSharper.Psi.ExtensionsAPI.Tree;
using JetBrains.ReSharper.Psi.Impl.Shared;
using JetBrains.ReSharper.Psi.Modules;
using JetBrains.ReSharper.Psi.Resolve;
using JetBrains.ReSharper.Psi.Transactions;
using JetBrains.ReSharper.Psi.Tree;
using JetBrains.ReSharper.Psi.Util;
using JetBrains.ReSharper.Psi.Web.CodeBehindSupport;
using JetBrains.ReSharper.Resources.Shell;
using JetBrains.Util;

namespace GammaJul.ForTea.Core.Psi
{
  /// <summary>
  /// C# custom modification handler that allows the T4 files to be modified in response to C# actions or quickfixes.
  /// (eg: adding a using statement translates to an import directive).
  /// </summary>
  [ProjectFileType(typeof(T4ProjectFileType))]
  [ZoneMarker(typeof(IWebPsiLanguageZone))]
  public class T4CSharpCustomModificationHandler : CustomModificationHandler<IT4CodeBlock, IT4Directive>,
    ICSharpCustomModificationHandler
  {
    /// <summary>Determines whether namespace aliases can be used.</summary>
    /// <returns>Always <c>false</c> since T4 files does not support aliases.</returns>
    public bool CanUseAliases => false;

    /// <summary>Determines whether static imports can be used.</summary>
    /// <returns>Always <c>false</c> since T4 files does not support static imports.</returns>
    public bool CanUseStaticImport => false;

    public bool CanOmitBraces => false;

    /// <summary>Creates a new T4 code block.</summary>
    /// <param name="text">The C# code.</param>
    /// <param name="anchor">Where to insert the code.</param>
    /// <returns>A new instance of <see cref="IT4CodeBlock"/>.</returns>
    protected override IT4CodeBlock CreateInlineCodeBlock(string text, ITreeNode anchor)
    {
      var existingFeatureNode = anchor.FindPreviousNode(node =>
        node is IT4FeatureBlock ? TreeNodeActionType.ACCEPT : TreeNodeActionType.CONTINUE);
      return existingFeatureNode != null
        ? T4ElementFactory.CreateFeatureBlock(text)
        : T4ElementFactory.CreateStatementBlock(text);
    }

    /// <summary>Gets the code tree text range of a code block.</summary>
    /// <param name="codeBlock">The code block.</param>
    /// <returns>A <see cref="TreeTextRange"/> representing the code range in <paramref name="codeBlock"/>.</returns>
    protected override TreeTextRange GetCodeTreeTextRange(IT4CodeBlock codeBlock) =>
      codeBlock.Code?.GetTreeTextRange() ?? TreeTextRange.InvalidRange;

    /// <summary>Creates a T4 import directive instead of a C# using directive.</summary>
    /// <param name="before"><c>true</c> to create the directive before <paramref name="anchor"/>; <c>false</c> to create it after.</param>
    /// <param name="anchor">An existing directive serving as an anchor for the new directive.</param>
    /// <param name="usingDirective">The C# using directive.</param>
    /// <param name="originalFile">The original T4 file where the directive must be created.</param>
    /// <returns>A <see cref="TreeTextRange"/> corresponding to the namespace in the newly created directive.</returns>
    protected override bool CreateAndMapUsingNode(bool before, IT4Directive anchor, ITreeNode usingDirective,
      IFile originalFile)
    {
      var t4File = (IT4File)originalFile;
      string ns = GetNamespaceFromUsingDirective(usingDirective);
      var directive = T4DirectiveInfoManager.Import.CreateDirective(ns);

      if (anchor != null && anchor.GetContainingNode<IT4IncludeDirective>() == null)
        directive = before
          ? t4File.AddDirectiveBefore(directive, anchor)
          : t4File.AddDirectiveAfter(directive, anchor);
      else
        directive = t4File.AddDirective(directive);

      var csharpFile = usingDirective.GetContainingFile();
      if (csharpFile == null) return true;
      var csharpUsingRange = GetNameRange(usingDirective);
      if (!csharpUsingRange.IsValid()) return false;

      var t4AttributeValueRange = directive
        .GetAttributeValueToken(T4DirectiveInfoManager.Import.NamespaceAttribute.Name).GetTreeTextRange();
      if (!t4AttributeValueRange.IsValid()) return false;

      csharpFile.GetRangeTranslator().AddProjectionItem(
        new TreeTextRange<Generated>(csharpUsingRange),
        new TreeTextRange<Original>(t4AttributeValueRange)
      );

      return true;
    }

    /// <summary>Gets the text range of a C# using directive namespace.</summary>
    /// <param name="usingDirective">The using directive.</param>
    /// <returns>A <see cref="TreeTextRange"/> corresponding to the namespace in <paramref name="usingDirective"/>.</returns>
    protected override TreeTextRange GetNameRange(ITreeNode usingDirective) =>
      GetUsedNamespaceNode(usingDirective as IUsingDirective).GetTreeTextRange();

    /// <summary>Removes an import directive.</summary>
    /// <param name="originalFile">The original T4 file where the directive must be removed.</param>
    /// <param name="directiveInOriginalFile">The import directive in the file.</param>
    protected override void RemoveUsingNode(IFile originalFile, IT4Directive directiveInOriginalFile) =>
      ((IT4File)originalFile).RemoveDirective(directiveInOriginalFile);

    /// <summary>Creates a new feature block with new type members.</summary>
    /// <param name="originalFile">The original T4 file where the feature block must be created.</param>
    /// <param name="text">The code representing new C# type members.</param>
    /// <param name="first">The first node.</param>
    /// <param name="last">The last node.</param>
    /// <returns>A <see cref="TreeTextRange"/> representing the code range in the newly created feature block.</returns>
    protected override TreeTextRange CreateTypeMemberNode(IFile originalFile, string text, ITreeNode first,
      ITreeNode last)
    {
      var featureBlock = T4ElementFactory.CreateFeatureBlock(text);
      featureBlock = ((IT4File)originalFile).AddFeatureBlock(featureBlock);
      return featureBlock.Code.GetTreeTextRange();
    }

    /// <summary>Creates a new line token.</summary>
    /// <param name="psiModule">The associated PSI module.</param>
    /// <returns>A T4 new line token.</returns>
    protected override ITreeNode CreateNewLineToken(IPsiModule psiModule) =>
      CSharpTokenType.NEW_LINE.CreateLeafElement();

    /// <summary>Gets an existing feature block that can contains type members.</summary>
    /// <param name="originalFile">The original T4 file.</param>
    /// <returns>A valid <see cref="TreeTextRange"/> if a feature block existed, <see cref="TreeTextRange.InvalidRange"/> otherwise.</returns>
    protected override TreeTextRange GetExistingTypeMembersRange(IFile originalFile)
    {
      var lastFeatureBlock = ((IT4File)originalFile).Blocks.OfType<IT4FeatureBlock>().LastOrDefault();
      return lastFeatureBlock?.Code.GetTreeTextRange() ?? TreeTextRange.InvalidRange;
    }


    protected override void AddSuperClassDirectiveToOriginalFile(IFile originalFile, ITreeNode anchor,
      ITreeNode superClassGeneratedNode)
    {
      var t4File = (IT4File)originalFile;
      var directive = t4File.GetDirectives(T4DirectiveInfoManager.Template).FirstOrDefault();
      IT4DirectiveAttribute attribute;
      string superClassName = superClassGeneratedNode.GetText();

      if (directive == null)
      {
        directive = T4DirectiveInfoManager.Template.CreateDirective(
          Pair.Of(T4DirectiveInfoManager.Template.InheritsAttribute.Name, superClassName));
        directive = t4File.AddDirective(directive);
        attribute = directive.Attributes.First();
      }
      else
      {
        attribute = directive.AddAttribute(
          T4DirectiveInfoManager.Template.InheritsAttribute.CreateDirectiveAttribute(superClassName));
      }

      superClassGeneratedNode.GetRangeTranslator().AddProjectionItem(
        new TreeTextRange<Generated>(superClassGeneratedNode.GetTreeTextRange()),
        new TreeTextRange<Original>(attribute.Value.GetTreeTextRange()));
    }

    protected override ITreeNode GetSuperClassNodeFromOriginalFile(IFile originalFile)
    {
      var t4File = (IT4File)originalFile;
      foreach (var templateDirective in t4File.GetDirectives(T4DirectiveInfoManager.Template))
      {
        var inheritsToken
          = templateDirective.GetAttributeValueToken(T4DirectiveInfoManager.Template.InheritsAttribute.Name);
        if (inheritsToken != null) return inheritsToken;
      }

      return null;
    }

    public bool IsQualifiedUsingAtNestedScope(ITreeNode context, IContextBoundSettingsStore settingsStore) =>
      settingsStore.GetValue(CSharpUsingSettingsAccessor.QualifiedUsingAtNestedScope);

    /// <summary>Determines whether a specified C# using directive can be removed.</summary>
    /// <param name="document">The document.</param>
    /// <param name="usingDirective">The using directive.</param>
    /// <returns><c>true</c> if the specified using directive can be removed; otherwise, <c>false</c>.</returns>
    /// <remarks>As long as the using is represented as a T4 import directive in the root file, it can be removed.</remarks>
    public bool CanRemoveUsing(IDocument document, IUsingDirective usingDirective)
    {
      var nameRange = GetNameRange(usingDirective);
      if (!nameRange.IsValid())
        return false;

      var containingFile = usingDirective.GetContainingFile();
      if (containingFile == null)
        return false;

      var documentRange = containingFile.GetDocumentRange(nameRange);
      return documentRange.IsValid() && documentRange.Document == document;
    }

    public ICSharpStatementsRange HandleAddStatementsRange(
      IPsiServices psiServices,
      Func<ITreeNode, ICSharpStatementsRange> addAction,
      IStatementsOwner block,
      ITreeNode anchor,
      bool before,
      bool strict
    )
    {
      using (CustomGeneratedChangePromotionCookie.Create(block))
      {
        var range = addAction(anchor);
        FinishAddStatementsRange(range.TreeRange, before);
        return range;
      }
    }

    public void HandleRemoveStatementsRange(IPsiServices psiServices, ITreeRange treeRange, Action action)
      => action();

    public ITreeRange HandleChangeStatements(IPsiServices psiServices, ITreeRange rangeBeforeChange,
      Func<ITreeRange> changeAction, bool strict)
      => changeAction();

    public void HandleChangeExpressionInStatement(IPsiServices psiServices, IStatement statement,
      Action changeAction)
      => changeAction();

    /// <summary>
    /// Handles the removal of an import directive.
    /// </summary>
    /// <param name="psiServices">The PSI services.</param>
    /// <param name="scope">The namespace scope.</param>
    /// <param name="usingDirective">The using directive to remove.</param>
    /// <param name="action">The action to perform to remove the directive.</param>
    public void HandleRemoveImport(
      IPsiServices psiServices,
      ICSharpTypeAndNamespaceHolderDeclaration scope,
      IUsingDirective usingDirective,
      Action action
    )
    {
      ICSharpTreeNode namespaceNode = GetUsedNamespaceNode(usingDirective);
      if (namespaceNode == null)
        Assertion.Fail("Only a namespace using can be removed.");
      else
      {
        TreeTextRange range = namespaceNode.GetTreeTextRange();
        HandleRemoveImportInternal(psiServices, scope, usingDirective, action, CSharpLanguage.Instance, range);
      }
    }

    /// <summary>Handles the removal of a type member from a code block.</summary>
    /// <param name="psiServices">The PSI services.</param>
    /// <param name="node">The node that must be removed.</param>
    /// <param name="action">The action to execute to remove the node.</param>
    public void HandleRemoveTypeMember(IPsiServices psiServices, ITreeNode node, Action action)
    {
      action();
      RemoveContainingBlockIfEmpty(node);
    }

    private static void RemoveContainingBlockIfEmpty([CanBeNull] ITreeNode node)
    {
      var block = node.GetT4ContainerFromCSharpNode<IT4CodeBlock>();
      string code = block?.Code.GetText();
      if (code == null || code.Trim().Length == 0)
        return;

      if (!(block.GetContainingFile() is IT4File file) || node == null) return;
      using (WriteLockCookie.Create(file.IsPhysical()))
      {
        ModificationUtil.DeleteChild(node);
      }
    }

    /// <summary>Gets the body of a method that is visible for user.</summary>
    /// <param name="method">The method.</param>
    /// <returns>Always the body of <paramref name="method"/>.</returns>
    public IBlock GetMethodBodyVisibleForUser(ICSharpFunctionDeclaration method)
      => method.Body;

    public bool PreferQualifiedReference(IQualifiableReference reference, IDeclaredElement targetElement) =>
      false;

    public bool IsToAddImportsToDeepestScope(ITreeNode context, IContextBoundSettingsStore settingsStore)
      => false;

    /// <summary>Retrieves the namespace from a C# using directive.</summary>
    /// <param name="usingDirective">The using directive.</param>
    /// <returns>The namespace contained in <paramref name="usingDirective"/>.</returns>
    [NotNull]
    private static string GetNamespaceFromUsingDirective([NotNull] ITreeNode usingDirective)
    {
      IReferenceName namespaceNode = GetUsedNamespaceNode(usingDirective as IUsingDirective);
      if (namespaceNode == null)
        throw new FailPsiTransactionException("Cannot create namespace alias.");
      return namespaceNode.QualifiedName;
    }

    /// <summary>Handles the addition of an import directive.</summary>
    /// <param name="psiServices">The PSI services.</param>
    /// <param name="action">The action to perform to add the directive.</param>
    /// <param name="generatedAnchor">The existing using anchor.</param>
    /// <param name="before">Whether to add the statements before of after <paramref name="generatedAnchor"/>.</param>
    /// <param name="generatedFile">The generated file.</param>
    /// <returns>An instance of <see cref="IUsingDirective"/>.</returns>
    public IUsingDirective HandleAddImport(IPsiServices psiServices, Func<IUsingDirective> action,
      ITreeNode generatedAnchor, bool before, IFile generatedFile) =>
      HandleAddImportInternal(psiServices, action, generatedAnchor, before, CSharpLanguage.Instance, generatedFile);

    public bool PreferQualifiedReference(IQualifiableReference reference) => reference
      .GetTreeNode()
      .GetSettingsStore()
      .GetValue(CSharpUsingSettingsAccessor.PreferQualifiedReference);

    public string GetSpecialMethodType(DeclaredElementPresenterStyle presenter, IMethod method,
      ISubstitution substitution) =>
      null;

    public ThisQualifierSettingsKey
      GetThisQualifierStyle(ITreeNode context, IContextBoundSettingsStore settingsStore) =>
      context.GetSettingsStore().GetKey<ThisQualifierSettingsKey>(SettingsOptimization.OptimizeDefault);

    public IList<ITreeRange> GetHolderBlockRanges(ITreeNode treeNode) =>
      new ITreeRange[] { new TreeRange(treeNode.FirstChild, treeNode.LastChild) };

    /// <summary>Initializes a new instance of the <see cref="T4CSharpCustomModificationHandler"/> class.</summary>
    /// <param name="languageManager">The language manager.</param>
    public T4CSharpCustomModificationHandler([NotNull] ILanguageManager languageManager) : base(languageManager)
    {
    }

    [CanBeNull]
    private static IReferenceName GetUsedNamespaceNode([CanBeNull] IUsingDirective directive) =>
      directive is IUsingSymbolDirective usingSymbolDirective && usingSymbolDirective.StaticKeyword == null
        ? usingSymbolDirective.ImportedSymbolName
        : null;
  }
}