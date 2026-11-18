namespace OccasionKit
{
    /// <summary>What an occasion's targeting is matched against.</summary>
    public sealed class OccasionContext
    {
        /// <summary>Matches every occasion regardless of targeting. Used by editor previews.</summary>
        public static readonly OccasionContext Any = new OccasionContext(null, null, true);

        public string Store { get; }

        /// <summary>ISO 639-1 code such as "fa" or "en-US"; empty when unknown.</summary>
        public string Language { get; }

        public bool IgnoreTargeting { get; }

        public OccasionContext(string store, string language) : this(store, language, false) { }

        private OccasionContext(string store, string language, bool ignoreTargeting)
        {
            Store = store;
            Language = language ?? string.Empty;
            IgnoreTargeting = ignoreTargeting;
        }

        public override string ToString() => IgnoreTargeting
            ? "any store and language"
            : $"store: {(string.IsNullOrEmpty(Store) ? "-" : Store)}, language: {(string.IsNullOrEmpty(Language) ? "-" : Language)}";
    }
}
