using d60.Cirqus.Ntfs.Events;
using NUnit.Framework;


namespace d60.Cirqus.Tests.Events.Ntfs
{
    [TestFixture]
    public class TestCommitLog : FixtureBase
    {
	    CommitLog _log;

        [SetUp]
        public void TestCommitLog2()
        {
            _log = RegisterForDisposal(new CommitLog("testdata", dropEvents: true));
        }

        [TearDown]
        protected override void DoTearDown() {
            base.DoTearDown();
            DisposeStuff();
        }

        [Test]
        public void GetLastComittedGlobalSequenceNumberFromEmptyFile()
        {
            bool corrupted;
            var global = _log.Read(out corrupted);
            Assert.AreEqual(-1, global);
            Assert.AreEqual(false, corrupted);
        }

        [Test]
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

        [Test]
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

        [Test]
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

        [Test]
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

        [Test]
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

        [Test]
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
        [Test]
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

        [Test]
        public void GetLastCommittedGlobalSequenceNumberFromFileWithCorruptCommit()
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

        [Test]
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

        [Test]
        public void GetLastCommittedGlobalSequenceNumberFromFileWithCorruptChecksum()
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

        [Test]
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

            var global = _log.Read(out var corrupted);
            Assert.AreEqual(10, global);
            Assert.AreEqual(false, corrupted);
        }

        [Test]
        public void GetLastCommittedGlobalSequenceNumberFromFileWithMissingChecksum()
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

        [Test]
        public void RecoverFileWithMissingChecksum()
        {
            // a good one
            _log.Writer.Write(10L);
            _log.Writer.Write(10L);

            // a commit without checksum
            _log.Writer.Write(11L);
            _log.Writer.Flush();

            _log.Recover();

            var global = _log.Read(out var corrupted);
            Assert.AreEqual(10, global);
            Assert.AreEqual(false, corrupted);
        }
    }
}