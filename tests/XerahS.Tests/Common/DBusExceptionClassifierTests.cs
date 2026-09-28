#region License Information (GPL v3)

/*
    XerahS - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2026 ShareX Team

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

    Optionally you can also view the license at <http://www.gnu.org/licenses/>.
*/

#endregion License Information (GPL v3)

using NUnit.Framework;
using XerahS.Common;

namespace XerahS.Tests.Common;

[TestFixture]
public class DBusExceptionClassifierTests
{
    private sealed class DBusConnectionClosedException : Exception
    {
        public DBusConnectionClosedException() : base("Connection closed by peer") { }
    }

    [Test]
    public void EisFailure_IsRecognised()
    {
        var ex = new InvalidOperationException("org.freedesktop.DBus.Error.Failed: Not connected to EIS");
        Assert.That(DBusExceptionClassifier.IsEisUnavailable(ex), Is.True);
        Assert.That(DBusExceptionClassifier.IsEisUnavailable(new Exception("wrapper", ex)), Is.True);
    }

    [Test]
    public void ConnectionClosed_InsideAggregate_IsRecognised()
    {
        var aggregate = new AggregateException(new DBusConnectionClosedException());
        Assert.That(DBusExceptionClassifier.IsConnectionGone(aggregate), Is.True);
    }

    [Test]
    public void UnrelatedErrors_AreNotSuppressed()
    {
        Assert.That(DBusExceptionClassifier.IsEisUnavailable(new IOException("disk full")), Is.False);
        Assert.That(DBusExceptionClassifier.IsConnectionGone(new IOException("Connection closed")), Is.False);
        Assert.That(DBusExceptionClassifier.IsConnectionGone(
            new AggregateException(new DBusConnectionClosedException(), new IOException("x"))), Is.False);
    }

    [Test]
    public void Describe_UsesInnermostException()
    {
        var ex = new Exception("outer", new InvalidOperationException("inner"));
        Assert.That(DBusExceptionClassifier.Describe(ex), Is.EqualTo("InvalidOperationException: inner"));
    }
}
