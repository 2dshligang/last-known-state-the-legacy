namespace LKS
{
    public interface INarrativeView
    {
        void ShowText(string englishText, StableId portrait, StableId cameraCue);
    }

    public sealed class SaveSnapshot
    {
        private readonly byte[] payload;
        public int SchemaVersion { get; }
        public string ContentHash { get; }

        public SaveSnapshot(int schemaVersion, string contentHash, byte[] payload)
        {
            SchemaVersion = schemaVersion;
            ContentHash = contentHash;
            this.payload = (byte[])payload.Clone();
        }

        public byte[] CopyPayload() => (byte[])payload.Clone();
    }

    public interface ISaveService
    {
        CommandResult Write(StableId slot, SaveSnapshot snapshot);
        CommandResult Read(StableId slot, out SaveSnapshot snapshot);
    }
}
