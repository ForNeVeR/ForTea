using JetBrains.ReSharper.Psi.ExtensionsAPI.Tree;

namespace GammaJul.ForTea.Core.Tree.Impl
{
	/// <summary>
	/// The base class that contains mostly boilerplate code.
	/// I'll try to find a way to remove it
	/// </summary>
	public abstract class T4FileBase : FileElementBase
	{
		/// TODO: remove and use manual range translation instead
		/// The problem is, I need to access document ranges during construction of secondary PSI
		/// and some other points in time when the file is considered invalid
		/// because it has not registered in some caches yet.
		/// There is an assert somewhere very deep in the platform that checks file validity
		/// before converting tree ranges to document ranges.
		/// <summary>
		/// The most plausible correctness condition that still allows to work with document ranges
		/// </summary>
		public override bool IsValid()
		{
			// The part borrowed from FileElementBase because it needs to be overridden
			if (parent == null) return true;
			// The part borrowed from TreeElement because it cannot be accessed directly
			TreeElement tmp = parent;
			while (tmp.parent != null)
			{
				tmp = tmp.parent;
			}

			return tmp.IsValid();
		}

		public abstract void Accept(TreeNodeVisitor visitor);
		public abstract void Accept<TContext>(TreeNodeVisitor<TContext> visitor, TContext context);
		public abstract TReturn Accept<TContext, TReturn>(TreeNodeVisitor<TContext, TReturn> visitor, TContext context);
	}
}
