using System.Text.Json;
using Atlassian.Jira;
using Microsoft.Extensions.Options;
using SlackNet;
using SlackNet.Blocks;
using SlackNet.Events;
using SlackNet.Interaction;
using SlackNet.WebApi;
using Button = SlackNet.Blocks.Button;
using ILogger = Microsoft.Extensions.Logging.ILogger;
using Option = SlackNet.Blocks.Option;

namespace SlackBot.Event;

public class JiraHandler : IEventHandler<MessageEvent>, IBlockActionHandler<ButtonAction>, IViewSubmissionHandler
{
    public const string Trigger = "jira";
    public const string OpenModal = "open_jira_modal";
    public const string ModalCallbackId = "modal_jira_timelog";
    private const string DatePickerActionId = "date_picker";
    private const string CheckboxActionId = "checkbox";
    private const string SC_Search = "search";
    private const string SC_Autolog = "autolog";
    private const string SC_Log = "log";
    private readonly JiraSettings _jiraSettings;
    private readonly ILogger _log;
    private readonly ISlackApiClient _slack;
    private readonly int totalMinutesToLog = 360;

    public JiraHandler(ISlackApiClient slack, IOptions<JiraSettings> options, ILogger<JiraHandler> log)
    {
        _slack = slack;
        _log = log;
        _jiraSettings = options.Value;
    }

    public async Task Handle(ButtonAction action, BlockActionRequest request)
    {
        _log.LogInformation("{UserName} clicked the open Jira modal button in the {ChannelName} channel",
            request.User.Name,
            request.Channel.Name);
        var standardIssues = await IssuesToOptions(GetStandardIssues);
        var extraIssues = await IssuesToOptions(GetExtraIssues);
        var allOptions = standardIssues.Concat(extraIssues).DistinctBy(o => o.Value).ToList();

        await _slack.Views.Open(request.TriggerId, new ModalViewDefinition
        {
            Title = "Selected Issues Worklog",
            CallbackId = ModalCallbackId,
            Submit = "Approve",
            NotifyOnClose = true,
            PrivateMetadata =
                JsonSerializer.Serialize(new ModalMetadata(request.Channel.Id,
                    request.Channel.Name)), // Holding onto this info for later
            Blocks =
            {
                new InputBlock
                {
                    Label = "Probable Jira Issues",
                    BlockId = "checkbox_block",
                    Optional = false,
                    Element = new CheckboxGroup
                    {
                        ActionId = CheckboxActionId,
                        Options = allOptions,
                        InitialOptions = standardIssues,
                    },
                },
                new InputBlock
                {
                    Label = "Log On Date",
                    BlockId = "date_block",
                    Optional = false,
                    Element = new DatePicker { ActionId = DatePickerActionId, InitialDate = DateTime.Today },
                },
            },
        });
    }

    public async Task Handle(MessageEvent slackEvent)
    {
        if (slackEvent.Text?.StartsWith(Trigger) == false)
        {
            return;
        }

        var commands = slackEvent.Text?.Split(' ').Skip(1).ToList();
        switch (commands?.FirstOrDefault())
        {
            case SC_Log:
                await HandleLog(slackEvent, commands.Skip(1).ToList());
                break;
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

    public async Task<ViewSubmissionResponse> Handle(ViewSubmission viewSubmission)
    {
        var metadata = JsonSerializer.Deserialize<ModalMetadata>(viewSubmission.View.PrivateMetadata)!;
        _log.LogInformation("{UserName} submitted the demo modal view in the {MetadataChannelName} channel",
            viewSubmission.User.Name, metadata.ChannelName);

        var state = viewSubmission.View.State;
        var date = (state.GetValue<DatePickerValue>(DatePickerActionId).SelectedDate ?? DateTime.Today).AddHours(DateTime.Now.Hour);
        var selectedIssues = state.GetValue<CheckboxGroupValue>(CheckboxActionId).SelectedOptions.Select(o => o.Value);
        await SaveWorklogs(selectedIssues.ToList(), date);
        await _slack.Chat.PostMessage(new Message
        {
            Channel = metadata.ChannelId,
            Text = $"1d of time logged for {date:yyyy-M-d dddd}",
        });
        
        return ViewSubmissionResponse.Null;
    }

    public async Task HandleClose(ViewClosed viewClosed)
    {
        var metadata = JsonSerializer.Deserialize<ModalMetadata>(viewClosed.View.PrivateMetadata)!;
        _log.LogInformation("{UserName} cancelled the demo modal view in the {MetadataChannelName} channel",
            viewClosed.User.Name, metadata.ChannelName);

        await _slack.Chat.PostMessage(new Message
        {
            Channel = metadata.ChannelId,
            Text = "You cancelled the modal",
        });
    }

    private async Task HandleLog(MessageEvent slackEvent, List<string>? input)
    {
        _log.LogInformation("{Name} asked for a modal view demo in the {Channel} channel",
            (await _slack.Users.Info(slackEvent.User)).Name,
            (await _slack.Conversations.Info(slackEvent.Channel)).Name);

        await _slack.Chat.PostMessage(new Message
        {
            Channel = slackEvent.Channel,
            Blocks =
            {
                new SectionBlock { Text = "Ready to log today's time in Jira?" },
                new ActionsBlock
                {
                    Elements =
                    {
                        new Button
                        {
                            ActionId = OpenModal,
                            Text = "Review",
                        },
                    },
                },
            },
        });
    }

    private async Task HandleSearch(MessageEvent slackEvent, List<string>? input)
    {
        _log.LogInformation("Received jira search command from {User} in the {Channel} channel",
            (await _slack.Users.Info(slackEvent.User)).Name,
            (await _slack.Conversations.Info(slackEvent.Channel)).Name);
        await _slack.Chat.ScheduleMessage(new Message
        {
            Text = "pong",
            Channel = slackEvent.Channel,
        }, DateTime.Now.AddSeconds(15));
    }

    private async Task HandleAutoLog(MessageEvent slackEvent, List<string>? input)
    {
        _log.LogInformation("Received jira autolog command from {User} in the {Channel} channel",
            (await _slack.Users.Info(slackEvent.User)).Name,
            (await _slack.Conversations.Info(slackEvent.Channel)).Name);
        var issues = await GetStandardIssues();
        await SaveWorklogs((issues ?? Enumerable.Empty<Issue>()).Select(i => i.Key.ToString()).ToList(), DateTime.Now);
        await _slack.Chat.PostMessage(new Message
        {
            Channel = slackEvent.Channel,
            Text = $"1d of time logged for {DateTime.Today:yyyy-M-d dddd}",
        });
    }

    private async Task SaveWorklogs(IList<string> issues, DateTime date)
    {
        if (!issues.Any()) return;
        var minutesPerIssue = totalMinutesToLog / issues.Count;
        var jira = GetJiraClient();
        foreach (var key in issues)
        {
            var issue = await jira.Issues.GetIssueAsync(key);
            if (issue == null) continue;
            await issue.AddWorklogAsync(new Worklog($"{minutesPerIssue}m", date, "added by slackbot"));
        }
    }

    private async Task<IPagedQueryResult<Issue>?> GetStandardIssues()
    {
        var jira = GetJiraClient();
        var issues = await jira.Issues.GetIssuesFromJqlAsync(
            $"assignee = currentUser() AND type IN standardIssueTypes() AND (status WAS IN (\"In Development\",\"In Progress\") ON {DateTime.Now:yyyy-MM-dd})");
        return issues;
    }

    private async Task<IPagedQueryResult<Issue>?> GetExtraIssues()
    {
        var jira = GetJiraClient();
        var lastWeek = DateTime.Now.AddDays(-7).ToString("yyyy-MM-dd");
        var issues = await jira.Issues.GetIssuesFromJqlAsync(
            $"type IN standardIssueTypes() AND assignee WAS currentUser() AFTER {lastWeek} AND (status WAS IN (\"In Development\", \"In Progress\") AFTER {lastWeek})");
        return issues;
    }

    private async Task<IList<Option>> IssuesToOptions(Func<Task<IPagedQueryResult<Issue>?>> action)
    {
        var issues = await action();
        var options = new List<Option>();
        if (issues == null)
        {
            return options;
        }

        foreach (var issue in issues)
        {
            options.Add(new Option { Text = issue.Summary, Value = issue.Key.ToString() });
        }

        return options;
    }

    private Jira GetJiraClient()
    {
        return Jira.CreateRestClient(_jiraSettings.Url, _jiraSettings.Login, _jiraSettings.ApiToken);
    }

    private record ModalMetadata(string ChannelId, string ChannelName);
}
