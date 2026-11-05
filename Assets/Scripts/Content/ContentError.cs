using System;

namespace Equilibrium.Content
{
    /// <summary>
    /// Content load and validation error codes. Shared by the ScriptableObject and JSON paths
    /// (ADR-0003) and referenced by GDD acceptance criteria. Values are stable.
    /// </summary>
    public enum ContentError
    {
        None = 0,
        InvalidItemId = 1,
        DuplicateItemId = 2,
        InvalidMaxStack = 3,
        MissingDisplayName = 4,      // warning-level
        TagCycle = 10,
        TagDepthExceeded = 11,
        InvalidTagName = 12,
        NamespaceMissing = 20,
        NamespaceReserved = 21,
        DependencyRejected = 22,
        UndeclaredModification = 23,
        AddonCycle = 24,
        UnlockWithoutDepth = 30,
        NoNonTaskPath = 31,
        ForbiddenRewardType = 32,
        RecipeInvalid = 40,
        MarketEntryInvalid = 41,
    }

    public sealed class ContentException : Exception
    {
        public ContentError Error { get; }
        public string Subject { get; }
        public ContentException(ContentError error, string subject)
            : base($"{error}: {subject}") { Error = error; Subject = subject; }
    }

    /// <summary>One diagnostic produced during load; errors reject the owning pack, warnings do not.</summary>
    public readonly struct ContentDiagnostic
    {
        public readonly ContentError Error;
        public readonly string Namespace;
        public readonly string Subject;
        public readonly bool IsWarning;

        public ContentDiagnostic(ContentError error, string ns, string subject, bool isWarning = false)
        {
            Error = error; Namespace = ns; Subject = subject; IsWarning = isWarning;
        }

        public override string ToString() => $"[{(IsWarning ? "warn" : "error")}] {Namespace}: {Error} ({Subject})";
    }
}
