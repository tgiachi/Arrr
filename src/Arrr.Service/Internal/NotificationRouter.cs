using Arrr.Core.Data.Config;
using Arrr.Core.Data.Notifications;
using Arrr.Service.Internal.Types;
using Serilog;
using ILogger = Serilog.ILogger;

namespace Arrr.Service.Internal;

internal static class NotificationRouter
{
    internal static IReadOnlyList<string> Route(
        Notification notification,
        RoutingConfig config,
        IEnumerable<string> runningSinkIds,
        DateTimeOffset? localNow = null,
        ILogger? logger = null)
        => RouteWithDecision(notification, config, runningSinkIds, localNow, logger).SinkIds;

    internal static RoutingDecision RouteWithDecision(
        Notification notification,
        RoutingConfig config,
        IEnumerable<string> runningSinkIds,
        DateTimeOffset? localNow = null,
        ILogger? logger = null)
    {
        var sinks = runningSinkIds.ToList();

        if (!config.Enabled || config.Rules.Count == 0)
        {
            return new RoutingDecision(sinks, null, RoutingAction.AllowAll);
        }

        var now = (localNow ?? DateTimeOffset.Now).TimeOfDay;

        foreach (var rule in config.Rules)
        {
            if (!rule.Enabled)
            {
                continue;
            }

            if (!IsTimeActive(rule, now))
            {
                continue;
            }

            if (!Matches(notification, rule))
            {
                continue;
            }

            var ruleName = string.IsNullOrEmpty(rule.Name) ? "(unnamed)" : rule.Name;

            if (rule.Block)
            {
                logger?.Information(
                    "Routing rule {Rule} blocked notification [{Source}] \"{Title}\"",
                    ruleName, notification.Source, notification.Title
                );

                return new RoutingDecision([], ruleName, RoutingAction.Block);
            }

            if (rule.AllowSinks.Count > 0)
            {
                var allowed = rule.AllowSinks.Intersect(sinks).ToList();

                logger?.Information(
                    "Routing rule {Rule} restricted notification [{Source}] \"{Title}\" to sinks: {Sinks}",
                    ruleName, notification.Source, notification.Title, string.Join(", ", allowed)
                );

                return new RoutingDecision(allowed, ruleName, RoutingAction.Restrict);
            }

            logger?.Debug(
                "Routing rule {Rule} matched notification [{Source}] \"{Title}\" — allow all sinks",
                ruleName, notification.Source, notification.Title
            );

            return new RoutingDecision(sinks, ruleName, RoutingAction.AllowAll);
        }

        return new RoutingDecision(sinks, null, RoutingAction.AllowAll);
    }

    private static bool Matches(Notification notification, RoutingRule rule)
    {
        if (!string.IsNullOrEmpty(rule.SourcePattern) && !MatchesSource(notification.Source, rule.SourcePattern))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(rule.TitleContains) &&
            !notification.Title.Contains(rule.TitleContains, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(rule.BodyContains) &&
            !notification.Body.Contains(rule.BodyContains, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if ((int)notification.Priority < rule.MinPriority)
        {
            return false;
        }

        foreach (var cond in rule.ExtraConditions)
        {
            if (string.IsNullOrEmpty(cond.Key))
            {
                continue;
            }

            var extraVal = notification.GetExtra(cond.Key);

            if (extraVal is null)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(cond.Value) &&
                !extraVal.Contains(cond.Value, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsTimeActive(RoutingRule rule, TimeSpan now)
    {
        var hasFrom = TimeOnly.TryParse(rule.ActiveFrom, out var from);
        var hasTo = TimeOnly.TryParse(rule.ActiveTo, out var to);

        if (!hasFrom && !hasTo)
        {
            return true;
        }

        var current = TimeOnly.FromTimeSpan(now);

        if (hasFrom && !hasTo)
        {
            return current >= from;
        }

        if (!hasFrom && hasTo)
        {
            return current < to;
        }

        if (from <= to)
        {
            return current >= from && current < to;
        }

        return current >= from || current < to;
    }

    private static bool MatchesSource(string source, string pattern)
    {
        if (pattern.EndsWith("*"))
        {
            return source.StartsWith(pattern[..^1], StringComparison.Ordinal);
        }

        return string.Equals(source, pattern, StringComparison.Ordinal);
    }
}
