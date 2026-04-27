using Arrr.Core.Data.Config;
using Arrr.Core.Data.Notifications;
using Arrr.Core.Types;
using Arrr.Service.Internal;

namespace Arrr.Tests.Services;

[TestFixture]
public class NotificationRouterTests
{
    private static readonly string[] AllSinks = ["dbus", "websocket", "smtp"];

    private static Notification MakeNotification(
        string source = "com.arrr.plugin.test",
        string title = "Hello",
        string body = "World",
        int priority = 0,
        IReadOnlyDictionary<string, string>? extras = null)
        => new(
            Guid.NewGuid(),
            source,
            title,
            body,
            DateTimeOffset.UtcNow,
            null,
            (NotificationPriority)priority,
            Extras: extras
        );

    // ── routing disabled → all sinks ─────────────────────────────────────────

    [Test]
    public void Route_RoutingDisabled_ReturnsAllSinks()
    {
        var config = new RoutingConfig { Enabled = false };
        var result = NotificationRouter.Route(MakeNotification(), config, AllSinks);
        Assert.That(result, Is.EquivalentTo(AllSinks));
    }

    // ── no rules → all sinks ──────────────────────────────────────────────────

    [Test]
    public void Route_NoRules_ReturnsAllSinks()
    {
        var config = new RoutingConfig { Enabled = true, Rules = [] };
        var result = NotificationRouter.Route(MakeNotification(), config, AllSinks);
        Assert.That(result, Is.EquivalentTo(AllSinks));
    }

    // ── disabled rule → skipped ───────────────────────────────────────────────

    [Test]
    public void Route_DisabledRule_RuleIsSkipped()
    {
        var config = new RoutingConfig
        {
            Enabled = true,
            Rules = [new RoutingRule { Enabled = false, Block = true }]
        };
        var result = NotificationRouter.Route(MakeNotification(), config, AllSinks);
        Assert.That(result, Is.EquivalentTo(AllSinks));
    }

    // ── block rule → empty list ───────────────────────────────────────────────

    [Test]
    public void Route_BlockRule_ReturnsEmpty()
    {
        var config = new RoutingConfig
        {
            Enabled = true,
            Rules = [new RoutingRule { Enabled = true, Block = true }]
        };
        var result = NotificationRouter.Route(MakeNotification(), config, AllSinks);
        Assert.That(result, Is.Empty);
    }

    // ── allow sinks → restricted list ────────────────────────────────────────

    [Test]
    public void Route_AllowSinks_ReturnsIntersectionWithRunning()
    {
        var config = new RoutingConfig
        {
            Enabled = true,
            Rules =
            [
                new RoutingRule
                {
                    Enabled = true,
                    AllowSinks = ["dbus", "missing-sink"]
                }
            ]
        };
        var result = NotificationRouter.Route(MakeNotification(), config, AllSinks);
        Assert.That(result, Is.EquivalentTo(new[] { "dbus" }));
    }

    // ── source exact match ────────────────────────────────────────────────────

    [Test]
    public void Route_SourceExactMatch_RuleApplies()
    {
        var config = new RoutingConfig
        {
            Enabled = true,
            Rules =
            [
                new RoutingRule
                {
                    Enabled = true,
                    SourcePattern = "com.arrr.plugin.test",
                    Block = true
                }
            ]
        };
        var result = NotificationRouter.Route(MakeNotification(source: "com.arrr.plugin.test"), config, AllSinks);
        Assert.That(result, Is.Empty);
    }

    // ── source does not match → rule skipped ─────────────────────────────────

    [Test]
    public void Route_SourceNoMatch_RuleSkipped()
    {
        var config = new RoutingConfig
        {
            Enabled = true,
            Rules =
            [
                new RoutingRule
                {
                    Enabled = true,
                    SourcePattern = "com.other.plugin",
                    Block = true
                }
            ]
        };
        var result = NotificationRouter.Route(MakeNotification(source: "com.arrr.plugin.test"), config, AllSinks);
        Assert.That(result, Is.EquivalentTo(AllSinks));
    }

    // ── source wildcard match ─────────────────────────────────────────────────

    [Test]
    public void Route_SourceWildcard_RuleApplies()
    {
        var config = new RoutingConfig
        {
            Enabled = true,
            Rules =
            [
                new RoutingRule
                {
                    Enabled = true,
                    SourcePattern = "com.arrr.plugin.*",
                    Block = true
                }
            ]
        };
        var result = NotificationRouter.Route(MakeNotification(source: "com.arrr.plugin.todoist"), config, AllSinks);
        Assert.That(result, Is.Empty);
    }

    // ── TitleContains case-insensitive ────────────────────────────────────────

    [Test]
    public void Route_TitleContains_CaseInsensitive_RuleApplies()
    {
        var config = new RoutingConfig
        {
            Enabled = true,
            Rules =
            [
                new RoutingRule
                {
                    Enabled = true,
                    TitleContains = "alert",
                    Block = true
                }
            ]
        };
        var result = NotificationRouter.Route(MakeNotification(title: "CRITICAL ALERT"), config, AllSinks);
        Assert.That(result, Is.Empty);
    }

    // ── TitleContains no match → skipped ─────────────────────────────────────

    [Test]
    public void Route_TitleContains_NoMatch_RuleSkipped()
    {
        var config = new RoutingConfig
        {
            Enabled = true,
            Rules =
            [
                new RoutingRule
                {
                    Enabled = true,
                    TitleContains = "urgent",
                    Block = true
                }
            ]
        };
        var result = NotificationRouter.Route(MakeNotification(title: "Hello"), config, AllSinks);
        Assert.That(result, Is.EquivalentTo(AllSinks));
    }

    // ── MinPriority match ─────────────────────────────────────────────────────

    [Test]
    public void Route_MinPriority_RuleAppliesWhenAtOrAbove()
    {
        var config = new RoutingConfig
        {
            Enabled = true,
            Rules =
            [
                new RoutingRule
                {
                    Enabled = true,
                    MinPriority = 1,
                    AllowSinks = ["smtp"]
                }
            ]
        };
        var resultHigh = NotificationRouter.Route(MakeNotification(priority: 1), config, AllSinks);
        var resultNormal = NotificationRouter.Route(MakeNotification(priority: 0), config, AllSinks);

        Assert.That(resultHigh, Is.EquivalentTo(new[] { "smtp" }));
        Assert.That(resultNormal, Is.EquivalentTo(AllSinks));
    }

    // ── first-match-wins ──────────────────────────────────────────────────────

    [Test]
    public void Route_FirstMatchWins_SecondRuleNotApplied()
    {
        var config = new RoutingConfig
        {
            Enabled = true,
            Rules =
            [
                new RoutingRule { Enabled = true, AllowSinks = ["dbus"] },
                new RoutingRule { Enabled = true, Block = true }
            ]
        };
        var result = NotificationRouter.Route(MakeNotification(), config, AllSinks);
        Assert.That(result, Is.EquivalentTo(new[] { "dbus" }));
    }

    // ── multi-condition AND ───────────────────────────────────────────────────

    [Test]
    public void Route_MultiCondition_AllMustMatch()
    {
        var config = new RoutingConfig
        {
            Enabled = true,
            Rules =
            [
                new RoutingRule
                {
                    Enabled = true,
                    SourcePattern = "com.arrr.plugin.test",
                    TitleContains = "alert",
                    Block = true
                }
            ]
        };
        var noMatch = NotificationRouter.Route(MakeNotification(source: "com.arrr.plugin.test", title: "Hello"), config, AllSinks);
        var match = NotificationRouter.Route(MakeNotification(source: "com.arrr.plugin.test", title: "ALERT"), config, AllSinks);

        Assert.That(noMatch, Is.EquivalentTo(AllSinks));
        Assert.That(match, Is.Empty);
    }

    // ── AllowSinks filtered to running only ───────────────────────────────────

    [Test]
    public void Route_AllowSinks_NonRunningFilteredOut()
    {
        var config = new RoutingConfig
        {
            Enabled = true,
            Rules =
            [
                new RoutingRule
                {
                    Enabled = true,
                    AllowSinks = ["smtp", "nonexistent"]
                }
            ]
        };
        var result = NotificationRouter.Route(MakeNotification(), config, AllSinks);
        Assert.That(result, Is.EquivalentTo(new[] { "smtp" }));
    }

    // ── Time window: inside → rule applies ───────────────────────────────────

    [Test]
    public void Route_TimeWindow_InsideWindow_RuleApplies()
    {
        var now = new DateTimeOffset(2026, 1, 1, 23, 0, 0, TimeSpan.Zero); // 23:00
        var config = new RoutingConfig
        {
            Enabled = true,
            Rules = [new RoutingRule { Enabled = true, ActiveFrom = "22:00", ActiveTo = "08:00", Block = true }]
        };
        Assert.That(NotificationRouter.Route(MakeNotification(), config, AllSinks, now), Is.Empty);
    }

    // ── Time window: outside → rule skipped ──────────────────────────────────

    [Test]
    public void Route_TimeWindow_OutsideWindow_RuleSkipped()
    {
        var now = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero); // 12:00
        var config = new RoutingConfig
        {
            Enabled = true,
            Rules = [new RoutingRule { Enabled = true, ActiveFrom = "22:00", ActiveTo = "08:00", Block = true }]
        };
        Assert.That(NotificationRouter.Route(MakeNotification(), config, AllSinks, now), Is.EquivalentTo(AllSinks));
    }

    // ── Time window: midnight crossing (from > to) ────────────────────────────

    [Test]
    public void Route_TimeWindow_MidnightCrossing_EarlyMorning_RuleApplies()
    {
        var now = new DateTimeOffset(2026, 1, 1, 6, 0, 0, TimeSpan.Zero); // 06:00
        var config = new RoutingConfig
        {
            Enabled = true,
            Rules = [new RoutingRule { Enabled = true, ActiveFrom = "22:00", ActiveTo = "08:00", Block = true }]
        };
        Assert.That(NotificationRouter.Route(MakeNotification(), config, AllSinks, now), Is.Empty);
    }

    // ── Time window: no window → always active ────────────────────────────────

    [Test]
    public void Route_TimeWindow_NoWindow_AlwaysActive()
    {
        var now = new DateTimeOffset(2026, 1, 1, 14, 0, 0, TimeSpan.Zero);
        var config = new RoutingConfig
        {
            Enabled = true,
            Rules = [new RoutingRule { Enabled = true, ActiveFrom = "", ActiveTo = "", Block = true }]
        };
        Assert.That(NotificationRouter.Route(MakeNotification(), config, AllSinks, now), Is.Empty);
    }

    // ── ExtraConditions: key exists ───────────────────────────────────────────

    [Test]
    public void Route_ExtraCondition_KeyExists_RuleApplies()
    {
        var config = new RoutingConfig
        {
            Enabled = true,
            Rules =
            [
                new RoutingRule
                {
                    Enabled = true,
                    ExtraConditions = [new ExtraCondition { Key = "todoist.task_id", Value = "" }],
                    Block = true
                }
            ]
        };
        var withExtra = MakeNotification(extras: new Dictionary<string, string> { ["todoist.task_id"] = "123" });
        var withoutExtra = MakeNotification();

        Assert.That(NotificationRouter.Route(withExtra, config, AllSinks), Is.Empty);
        Assert.That(NotificationRouter.Route(withoutExtra, config, AllSinks), Is.EquivalentTo(AllSinks));
    }

    // ── ExtraConditions: value substring match ────────────────────────────────

    [Test]
    public void Route_ExtraCondition_ValueSubstring_CaseInsensitive()
    {
        var config = new RoutingConfig
        {
            Enabled = true,
            Rules =
            [
                new RoutingRule
                {
                    Enabled = true,
                    ExtraConditions = [new ExtraCondition { Key = "todoist.project_id", Value = "WORK" }],
                    Block = true
                }
            ]
        };
        var match = MakeNotification(extras: new Dictionary<string, string> { ["todoist.project_id"] = "proj-work-123" });
        var noMatch = MakeNotification(extras: new Dictionary<string, string> { ["todoist.project_id"] = "proj-personal" });

        Assert.That(NotificationRouter.Route(match, config, AllSinks), Is.Empty);
        Assert.That(NotificationRouter.Route(noMatch, config, AllSinks), Is.EquivalentTo(AllSinks));
    }

    // ── ExtraConditions: multiple conditions AND ──────────────────────────────

    [Test]
    public void Route_ExtraCondition_MultipleConditions_AllMustMatch()
    {
        var config = new RoutingConfig
        {
            Enabled = true,
            Rules =
            [
                new RoutingRule
                {
                    Enabled = true,
                    ExtraConditions =
                    [
                        new ExtraCondition { Key = "todoist.project_id", Value = "work" },
                        new ExtraCondition { Key = "todoist.priority", Value = "4" }
                    ],
                    Block = true
                }
            ]
        };
        var both = MakeNotification(extras: new Dictionary<string, string>
        {
            ["todoist.project_id"] = "work-project",
            ["todoist.priority"] = "4"
        });
        var onlyFirst = MakeNotification(extras: new Dictionary<string, string>
        {
            ["todoist.project_id"] = "work-project"
        });

        Assert.That(NotificationRouter.Route(both, config, AllSinks), Is.Empty);
        Assert.That(NotificationRouter.Route(onlyFirst, config, AllSinks), Is.EquivalentTo(AllSinks));
    }
}
