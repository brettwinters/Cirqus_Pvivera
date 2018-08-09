using d60.Cirqus.Ntfs.Events;
using Xunit;
using Assert = NUnit.Framework.Assert;

namespace d60.Cirqus.Tests.Events.Ntfs
{
    public class TestCommitLog : FixtureBase
    {
        readonly CommitLog _log;

        public TestCommitLog()
        {
            _log = RegisterForDisposal(new CommitLog("testdata", dropEvents: true));
        }
        
        [Fact]
        public void GetLastComittedGlobalSequenceNumberFromEmptyFile()
        {
            bool corrupted;
            var global = _log.Read(out corrupted);
            Assert.AreEqual(-1, global);
            Assert.AreEqual(false, corrupted);
        }

        [Fact]
        public void GetLastComittedGlobalSequenceNumberFromOkFile()
        {
            _log.Writer.Write(10L);
            _log.Writer.Write(10L);
            _log.Writer.Flush();

            bool corrupted;
            var global = _log.Read(out corrupted);
            Assert.AreEqual(10, global);
            Assert.AreEqual(false, corrupted);
        }

        [Fact]
        public void GetLastComittedGlobalSequenceNumberFromFileWithCorruptFirstCommit()
        {
            // a corrupted one
            _log.Writer.Write((byte)0);
            _log.Writer.Flush();

            bool corrupted;
            var global = _log.Read(out corrupted);
            Assert.AreEqual(-1, global);
            Assert.AreEqual(true, corrupted);
        }

        [Fact]
        public void RecoverFileWithCorruptFirstCommit()
        {
            // a corrupted one
            _log.Writer.Write((byte)0);
            _log.Writer.Flush();

            _log.Recover();

            bool corrupted;
            var global = _log.Read(out corrupted);
            Assert.AreEqual(-1, global);
            Assert.AreEqual(false, corrupted);
        }

        [Fact]
        public void GetLastComittedGlobalSequenceNumberFromFileWithCorruptFirstChecksum()
        {
            // a corrupted one
            _log.Writer.Write(0L);
            _log.Writer.Write((byte)0);
            _log.Writer.Flush();

            bool corrupted;
            var global = _log.Read(out corrupted);
            Assert.AreEqual(-1, global);
            Assert.AreEqual(true, corrupted);
        }

        [Fact]
        public void RecoverFileWithCorruptFirstChecksum()
        {
            // a corrupted one
            _log.Writer.Write(0L);
            _log.Writer.Write((byte)0);
            _log.Writer.Flush();

            _log.Recover();

            bool corrupted;
            var global = _log.Read(out corrupted);
            Assert.AreEqual(-1, global);
            Assert.AreEqual(false, corrupted);
        }

        [Fact]
        public void GetLastComittedGlobalSequenceNumberFromFileWithMissingFirstChecksum()
        {
            // a commit without checksum
            _log.Writer.Write(0L);
            _log.Writer.Flush();

            bool corrupted;
            var global = _log.Read(out corrupted);
            Assert.AreEqual(-1, global);
            Assert.AreEqual(true, corrupted);
        }
        [Fact]
        public void RecoverFileWithMissingFirstChecksum()
        {
            // a commit without checksum
            _log.Writer.Write(0L);
            _log.Writer.Flush();

            _log.Recover();

            bool corrupted;
            var global = _log.Read(out corrupted);
            Assert.AreEqual(-1, global);
            Assert.AreEqual(false, corrupted);
        }

        [Fact]
        public void GetLastComittedGlobalSequenceNumberFromFileWithCorruptCommit()
        {
            // a good one
            _log.Writer.Write(10L);
            _log.Writer.Write(10L);

            // a corrupted one
            _log.Writer.Write((byte)11);
            _log.Writer.Flush();

            bool corrupted;
            var global = _log.Read(out corrupted);
            Assert.AreEqual(10, global);
            Assert.AreEqual(true, corrupted);
        }

        [Fact]
        public void RecoverFileWithCorruptCommit()
        {
            // a good one
            _log.Writer.Write(10L);
            _log.Writer.Write(10L);

            // a corrupted one
            _log.Writer.Write((byte)11);
            _log.Writer.Flush();

            _log.Recover();

            bool corrupted;
            var global = _log.Read(out corrupted);
            Assert.AreEqual(10, global);
            Assert.AreEqual(false, corrupted);
        }

        [Fact]
        public void GetLastComittedGlobalSequenceNumberFromFileWithCorruptChecksum()
        {
            // a good one
            _log.Writer.Write(10L);
            _log.Writer.Write(10L);

            // a corrupted one
            _log.Writer.Write(11L);
            _log.Writer.Write((byte)11);
            _log.Writer.Flush();

            bool corrupted;
            var global = _log.Read(out corrupted);
            Assert.AreEqual(10, global);
            Assert.AreEqual(true, corrupted);
        }

        [Fact]
        public void RecoverFileWithCorruptChecksum()
        {
            // a good one
            _log.Writer.Write(10L);
            _log.Writer.Write(10L);

            // a corrupted one
            _log.Writer.Write(11L);
            _log.Writer.Write((byte)11);
            _log.Writer.Flush();

            _log.Recover();

            bool corrupted;
            var global = _log.Read(out corrupted);
            Assert.AreEqual(10, global);
            Assert.AreEqual(false, corrupted);
        }

        [Fact]
        public void GetLastComittedGlobalSequenceNumberFromFileWithMissingChecksum()
        {
            // a good one
            _log.Writer.Write(10L);
            _log.Writer.Write(10L);

            // a commit without checksum
            _log.Writer.Write(11L);
            _log.Writer.Flush();

            bool corrupted;
            var global = _log.Read(out corrupted);
            Assert.AreEqual(10, global);
            Assert.AreEqual(true, corrupted);
        }

        [Fact]
        public void RecoverFileWithMissingChecksum()
        {
            // a good one
            _log.Writer.Write(10L);
            _log.Writer.Write(10L);

            // a commit without checksum
            _log.Writer.Write(11L);
            _log.Writer.Flush();

            _log.Recover();

            bool corrupted;
            var global = _log.Read(out corrupted);
            Assert.AreEqual(10, global);
            Assert.AreEqual(false, corrupted);
        }
    }
}