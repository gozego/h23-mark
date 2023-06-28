using Atlassian.Jira;
using Microsoft.Extensions.Options;
using SlackNet;
using SlackNet.Events;
using SlackNet.WebApi;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace SlackBot.Event;

public class JiraHandler : IEventHandler<MessageEvent>
{
    public const string Trigger = "jira";
    private const string SC_Search = "search";
    private const string SC_Autolog = "autolog";
    private readonly int totalMinutesToLog = 360;
    private readonly ILogger _log;
    private readonly ISlackApiClient _slack;
    private readonly JiraSettings _jiraSettings;

    public JiraHandler(ISlackApiClient slack, IOptions<JiraSettings> options, ILogger<JiraHandler> log)
    {
        _slack = slack;
        _log = log;
        _jiraSettings = options.Value;
    }

    public async Task Handle(MessageEvent slackEvent)
    {
        if (slackEvent.Text?.StartsWith(Trigger) == false) return;
        var commands = slackEvent.Text?.Split(' ').Skip(1).ToList();
        switch (commands?.FirstOrDefault())
        {
            case SC_Search:
                await HandleSearch(slackEvent, commands.Skip(1).ToList());
                break; 
            case SC_Autolog:
                await HandleAutoLog(slackEvent, commands.Skip(1).ToList());
                break;
            default:
                return;
        }
    }

    private async Task HandleSearch(MessageEvent slackEvent, List<string>? input)
    {
        _log.LogInformation("Received jira search command from {User} in the {Channel} channel",
            (await _slack.Users.Info(slackEvent.User)).Name,
            (await _slack.Conversations.Info(slackEvent.Channel)).Name);
        await _slack.Chat.PostMessage(new Message
        {
            Text = "pong",
            Channel = slackEvent.Channel,
        });
    }
    
    private async Task HandleAutoLog(MessageEvent slackEvent, List<string>? input)
    {
        _log.LogInformation("Received jira autolog command from {User} in the {Channel} channel",
            (await _slack.Users.Info(slackEvent.User)).Name,
            (await _slack.Conversations.Info(slackEvent.Channel)).Name);
        var jira = GetJiraClient();
        var issues = await jira.Issues.GetIssuesFromJqlAsync(
            $"assignee={_jiraSettings.Username} AND type in standardIssueTypes() AND (Status was in (\"In Development\",\"In Progress\") ON {DateTime.Now:yyyy-MM-dd})");
        var minutesPerIssue = issues.Any() ? totalMinutesToLog/issues.Count(): 0;
        foreach (var issue in issues)
        {
            await issue.AddWorklogAsync($"{minutesPerIssue}m");
        }
    }

    private Jira GetJiraClient()
    {
        return Jira.CreateRestClient(_jiraSettings.Url, _jiraSettings.Login, _jiraSettings.ApiToken);
    }
}
