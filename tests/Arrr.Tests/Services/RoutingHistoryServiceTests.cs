using Arrr.Core.Data.Notifications;
using Arrr.Core.Types;
using Arrr.Service.Internal;
using Arrr.Service.Internal.Types;

namespace Arrr.Tests.Services;

[TestFixture]
public class RoutingHistoryServiceTests
{
    private static Notification MakeNotification()
        => new(Guid.NewGuid(), "com.test", "Title", "Body", DateTimeOffset.UtcNow, null, NotificationPriority.Normal);

    // ── AllowAll is NOT stored ────────────────────────────────────────────────

    [Test]
    public void Record_AllowAll_NotStored()
    {
        var svc = new RoutingHistoryService();
        svc.Record(new RoutingDecision(["dbus"], "my-rule", RoutingAction.AllowAll), MakeNotification());
        Assert.That(svc.GetRecent(10), Is.Empty);
    }

    // ── Block IS stored ───────────────────────────────────────────────────────

    [Test]
    public void Record_Block_StoredWithActionBlocked()
    {
        var svc = new RoutingHistoryService();
        svc.Record(new RoutingDecision([], "block-rule", RoutingAction.Block), MakeNotification());
        var entries = svc.GetRecent(10);
        Assert.That(entries.Count, Is.EqualTo(1));
        Assert.That(entries[0].Action, Is.EqualTo("blocked"));
        Assert.That(entries[0].RuleName, Is.EqualTo("block-rule"));
    }

    // ── Restrict IS stored ────────────────────────────────────────────────────

    [Test]
    public void Record_Restrict_StoredWithActionRestricted()
    {
        var svc = new RoutingHistoryService();
        svc.Record(new RoutingDecision(["smtp"], "restrict-rule", RoutingAction.Restrict), MakeNotification());
        var entries = svc.GetRecent(10);
        Assert.That(entries.Count, Is.EqualTo(1));
        Assert.That(entries[0].Action, Is.EqualTo("restricted"));
        Assert.That(entries[0].TargetSinks, Is.EquivalentTo(new[] { "smtp" }));
    }

    // ── No rule matched (RuleName null) → not stored ─────────────────────────

    [Test]
    public void Record_NullRuleName_NotStored()
    {
        var svc = new RoutingHistoryService();
        svc.Record(new RoutingDecision(["dbus"], null, RoutingAction.AllowAll), MakeNotification());
        Assert.That(svc.GetRecent(10), Is.Empty);
    }

    // ── GetRecent returns newest first ────────────────────────────────────────

    [Test]
    public void GetRecent_ReturnsNewestFirst()
    {
        var svc = new RoutingHistoryService();
        svc.Record(new RoutingDecision([], "first", RoutingAction.Block), MakeNotification());
        svc.Record(new RoutingDecision([], "second", RoutingAction.Block), MakeNotification());
        var entries = svc.GetRecent(10);
        Assert.That(entries[0].RuleName, Is.EqualTo("second"));
        Assert.That(entries[1].RuleName, Is.EqualTo("first"));
    }

    // ── Ring buffer caps at MaxEntries ────────────────────────────────────────

    [Test]
    public void Record_ExceedsCapacity_OldestDropped()
    {
        var svc = new RoutingHistoryService();
        for (var i = 0; i < 201; i++)
        {
            svc.Record(new RoutingDecision([], $"rule-{i}", RoutingAction.Block), MakeNotification());
        }
        var entries = svc.GetRecent(250);
        Assert.That(entries.Count, Is.EqualTo(200));
        Assert.That(entries[0].RuleName, Is.EqualTo("rule-200"));
    }
}
