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

using System.Collections.Concurrent;
using NUnit.Framework;
using XerahS.UI.Views.Controls;

namespace XerahS.Tests.Avalonia;

[TestFixture]
public class HotkeySelectionControlDebugLogTests
{
    [SetUp]
    public void SetUp()
    {
        HotkeySelectionControl.ResetDebugLogForTests();
    }

    [TearDown]
    public void TearDown()
    {
        HotkeySelectionControl.ResetDebugLogForTests();
    }

    [Test]
    public void ConcurrentLogAndGetDebugLog_DoesNotThrow_AndPreservesMessages()
    {
        var callbackHits = 0;
        HotkeySelectionControl.SetDebugCallback(_ => Interlocked.Increment(ref callbackHits));

        const int writers = 8;
        const int messagesPerWriter = 40;
        const int expected = writers * messagesPerWriter;
        var exceptions = new ConcurrentBag<Exception>();

        Parallel.For(0, writers, writer =>
        {
            try
            {
                for (var i = 0; i < messagesPerWriter; i++)
                {
                    HotkeySelectionControl.Log($"w{writer}-m{i}");
                    if ((i & 3) == 0)
                    {
                        _ = HotkeySelectionControl.GetDebugLog();
                    }
                }
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        });

        // Concurrent readers while writers finish draining.
        // Note: do not name the Parallel.For index `_` — `_ = expr` then assigns to long, not discard.
        Parallel.For(0, 16, _i =>
        {
            try
            {
                _ = HotkeySelectionControl.GetDebugLog();
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        });

        Assert.That(exceptions, Is.Empty, () => string.Join(Environment.NewLine, exceptions));

        var snapshot = HotkeySelectionControl.GetDebugLog();
        Assert.That(snapshot, Is.Not.Null.And.Not.Empty);
        // Each Log() appends one line via the locked sink.
        var lineCount = snapshot.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;
        Assert.That(lineCount, Is.EqualTo(expected));
        Assert.That(callbackHits, Is.EqualTo(expected));
    }

    [Test]
    public void ResetDebugLogForTests_ClearsSinkAndBuffer()
    {
        HotkeySelectionControl.SetDebugCallback(_ => { });
        HotkeySelectionControl.Log("before-reset");
        Assert.That(HotkeySelectionControl.GetDebugLog(), Does.Contain("before-reset"));

        HotkeySelectionControl.ResetDebugLogForTests();
        Assert.That(HotkeySelectionControl.GetDebugLog(), Is.Empty);

        // After reset, Log still must not throw (sink is null until SetDebugCallback/OnLoaded).
        Assert.DoesNotThrow(() => HotkeySelectionControl.Log("after-reset-no-sink"));
        Assert.That(HotkeySelectionControl.GetDebugLog(), Is.Empty);
    }

    [Test]
    public void ConcurrentSetDebugCallbackAndLog_DoesNotThrow()
    {
        var exceptions = new ConcurrentBag<Exception>();
        var hits = 0;

        Parallel.Invoke(
            () =>
            {
                for (var i = 0; i < 50; i++)
                {
                    try
                    {
                        HotkeySelectionControl.SetDebugCallback(_ => Interlocked.Increment(ref hits));
                    }
                    catch (Exception ex)
                    {
                        exceptions.Add(ex);
                    }
                }
            },
            () =>
            {
                for (var i = 0; i < 200; i++)
                {
                    try
                    {
                        HotkeySelectionControl.Log($"race-{i}");
                        _ = HotkeySelectionControl.GetDebugLog();
                    }
                    catch (Exception ex)
                    {
                        exceptions.Add(ex);
                    }
                }
            });

        Assert.That(exceptions, Is.Empty, () => string.Join(Environment.NewLine, exceptions));
        // At least some messages should have reached a live callback.
        Assert.That(hits, Is.GreaterThan(0));
        Assert.DoesNotThrow(() => HotkeySelectionControl.GetDebugLog());
    }
}
