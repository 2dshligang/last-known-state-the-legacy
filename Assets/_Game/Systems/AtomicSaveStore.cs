using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace LKS
{
    public sealed class AtomicSaveStore : ISaveService
    {
        private const int Magic = 0x31534B4C;
        private const int HashSize = 32;
        private const int MaxBytes = 1024 * 1024;
        private readonly string folder;
        private readonly string contentHash;
        private readonly int schema;
        private readonly Dictionary<StableId, byte[]> rejectedPrimary = new Dictionary<StableId, byte[]>();

        public AtomicSaveStore(string folder, string contentHash, int schema)
        {
            if (string.IsNullOrWhiteSpace(contentHash) || contentHash.Length > 128)
                throw new ArgumentException("A reviewed content hash is required.", nameof(contentHash));
            if (schema < 1)
                throw new ArgumentOutOfRangeException(nameof(schema));
            this.folder = Path.GetFullPath(folder);
            this.contentHash = contentHash;
            this.schema = schema;
        }

        public CommandResult Write(StableId slot, SaveSnapshot snapshot)
        {
            if ((!slot.IsValid || slot.value.Length > 64) || snapshot == null)
                return Result(ResultCode.InvalidRequest, "invalid");
            if (snapshot.SchemaVersion != schema || snapshot.ContentHash != contentHash)
                return Result(ResultCode.IncompatibleVersion, "incompatible");
            var bytes = snapshot.CopyPayload();
            if (bytes.Length == 0 || bytes.Length > MaxBytes)
                return Result(ResultCode.InvalidRequest, "invalid");

            string target = SlotPath(slot);
            string temporary = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                Directory.CreateDirectory(folder);
                using var writerLock = new FileStream(target + ".lock", FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
                var existing = ReadFile(target, out var previous);
                var backup = ReadFile(target + ".bak", out _);
                if (existing.Code == ResultCode.IncompatibleVersion || backup.Code == ResultCode.IncompatibleVersion)
                    return Result(ResultCode.IncompatibleVersion, "incompatible");

                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (var writer = new BinaryWriter(stream))
                {
                    writer.Write(Magic);
                    writer.Write(schema);
                    writer.Write(contentHash);
                    writer.Write(bytes.Length);
                    writer.Write(Checksum(bytes));
                    writer.Write(bytes);
                    writer.Flush();
                    stream.Flush(true);
                }

                bool keepBackup = rejectedPrimary.TryGetValue(slot, out var rejected) && previous != null &&
                    rejected.SequenceEqual(Checksum(previous.CopyPayload()));
                if (File.Exists(target))
                    File.Replace(temporary, target, existing.Succeeded && !keepBackup ? target + ".bak" : null);
                else
                    File.Move(temporary, target);

                rejectedPrimary.Remove(slot);
                return Result(ResultCode.Applied, "written");
            }
            catch (Exception error) when (IsStorageFailure(error))
            {
                return Result(ResultCode.StorageError, "write-failed");
            }
            finally
            {
                try { File.Delete(temporary); }
                catch (Exception error) when (IsStorageFailure(error)) { }
            }
        }

        public CommandResult Read(StableId slot, out SaveSnapshot snapshot)
        {
            return ReadValidated(slot, _ => true, out snapshot);
        }

        internal CommandResult ReadValidated(StableId slot, Func<SaveSnapshot, bool> apply, out SaveSnapshot snapshot)
        {
            snapshot = null;
            if (!slot.IsValid)
                return Result(ResultCode.InvalidRequest, "invalid");

            string path = SlotPath(slot);
            var primary = ReadFile(path, out var candidate);
            if (primary.Code == ResultCode.IncompatibleVersion)
                return primary;
            if (primary.Succeeded)
            {
                if (apply(candidate))
                {
                    snapshot = candidate;
                    return Result(ResultCode.Applied, "loaded");
                }
                rejectedPrimary[slot] = Checksum(candidate.CopyPayload());
            }

            var backup = ReadFile(path + ".bak", out candidate);
            if (backup.Code == ResultCode.IncompatibleVersion)
                return backup;
            if (backup.Succeeded && apply(candidate))
            {
                snapshot = candidate;
                return Result(ResultCode.Applied, "backup-loaded");
            }

            return primary.Code == ResultCode.UnknownId && backup.Code == ResultCode.UnknownId
                ? Result(ResultCode.UnknownId, "empty-slot") : Result(ResultCode.StorageError, "corrupt");
        }

        private CommandResult ReadFile(string path, out SaveSnapshot snapshot)
        {
            snapshot = null;
            try
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
                if (stream.Length > MaxBytes + 512)
                    return Result(ResultCode.StorageError, "corrupt");
                using var reader = new BinaryReader(stream);
                if (reader.ReadInt32() != Magic)
                    return Result(ResultCode.StorageError, "corrupt");
                int version = reader.ReadInt32();
                string storedContent = reader.ReadString();
                int length = reader.ReadInt32();
                var expected = reader.ReadBytes(HashSize);
                if (length < 1 || length > MaxBytes || stream.Length - stream.Position != length)
                    return Result(ResultCode.StorageError, "corrupt");
                var payload = reader.ReadBytes(length);
                if (!expected.SequenceEqual(Checksum(payload)))
                    return Result(ResultCode.StorageError, "corrupt");

                if (version < 1 || version > schema || !string.Equals(storedContent, contentHash, StringComparison.Ordinal))
                    return Result(ResultCode.IncompatibleVersion, "incompatible");
                snapshot = new SaveSnapshot(version, contentHash, payload);
                return Result(ResultCode.Applied, "loaded");
            }
            catch (Exception error) when (error is FileNotFoundException || error is DirectoryNotFoundException)
            {
                return Result(ResultCode.UnknownId, "empty-slot");
            }
            catch (Exception error) when (IsStorageFailure(error) || error is FormatException)
            {
                return Result(ResultCode.StorageError, "corrupt");
            }
        }

        private string SlotPath(StableId slot) => Path.Combine(folder, "slot-" + slot + ".lks");

        private static byte[] Checksum(byte[] payload)
        {
            using var sha = SHA256.Create();
            return sha.ComputeHash(payload);
        }

        private static bool IsStorageFailure(Exception error) =>
            error is IOException || error is UnauthorizedAccessException || error is NotSupportedException;

        private static CommandResult Result(ResultCode code, string key) =>
            new CommandResult(code, new StableId("save.result." + key));
    }
}
