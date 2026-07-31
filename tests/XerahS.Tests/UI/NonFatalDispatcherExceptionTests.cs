using NUnit.Framework;

namespace Tmds.DBus.Protocol
{
    /// <summary>
    /// Test double living in the real Tmds.DBus namespace so the namespace-based matcher in
    /// <see cref="XerahS.UI.App.IsNonFatalDispatcherException"/> can be exercised without
    /// constructing internal Tmds.DBus exception types.
    /// </summary>
    public sealed class FakeDBusErrorReplyException : Exception
    {
        public FakeDBusErrorReplyException(string message) : base(message)
        {
        }
    }
}

namespace XerahS.Tests.UI
{
    [TestFixture]
    public class NonFatalDispatcherExceptionTests
    {
        private sealed class FreeDesktopStackTraceException : Exception
        {
            public override string StackTrace =>
                "   at Avalonia.FreeDesktop.DBusTrayIconImpl.CreateTrayIcon()\n" +
                "   at Avalonia.Threading.DispatcherOperation.Execute()";
        }

        [Test]
        public void TaskCanceledException_IsNonFatal()
        {
            Assert.That(XerahS.UI.App.IsNonFatalDispatcherException(new TaskCanceledException()), Is.True);
        }

        [Test]
        public void TmdsDBusException_IsNonFatal()
        {
            var ex = new Tmds.DBus.Protocol.FakeDBusErrorReplyException(
                "org.freedesktop.DBus.Error.ServiceUnknown: org.freedesktop.DBus.Error.ServiceUnknown");

            Assert.That(XerahS.UI.App.IsNonFatalDispatcherException(ex), Is.True);
        }

        [Test]
        public void TmdsDBusException_WrappedAsInner_IsNonFatal()
        {
            var ex = new InvalidOperationException(
                "Tray registration failed",
                new Tmds.DBus.Protocol.FakeDBusErrorReplyException("denied"));

            Assert.That(XerahS.UI.App.IsNonFatalDispatcherException(ex), Is.True);
        }

        [Test]
        public void ExceptionFromAvaloniaFreeDesktopFrames_IsNonFatal()
        {
            Assert.That(XerahS.UI.App.IsNonFatalDispatcherException(new FreeDesktopStackTraceException()), Is.True);
        }

        [Test]
        public void AggregateOfNonFatalExceptions_IsNonFatal()
        {
            var aggregate = new AggregateException(
                new TaskCanceledException(),
                new Tmds.DBus.Protocol.FakeDBusErrorReplyException("denied"));

            Assert.That(XerahS.UI.App.IsNonFatalDispatcherException(aggregate), Is.True);
        }

        [Test]
        public void OrdinaryExceptions_RemainFatal()
        {
            Assert.Multiple(() =>
            {
                Assert.That(XerahS.UI.App.IsNonFatalDispatcherException(null), Is.False);
                Assert.That(XerahS.UI.App.IsNonFatalDispatcherException(new InvalidOperationException("boom")), Is.False);
                Assert.That(XerahS.UI.App.IsNonFatalDispatcherException(new NullReferenceException()), Is.False);
                Assert.That(XerahS.UI.App.IsNonFatalDispatcherException(new AggregateException()), Is.False);
                Assert.That(
                    XerahS.UI.App.IsNonFatalDispatcherException(
                        new AggregateException(new TaskCanceledException(), new InvalidOperationException("boom"))),
                    Is.False);
            });
        }
    }
}
