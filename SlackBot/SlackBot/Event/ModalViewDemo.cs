using System.Text.Json;
using SlackNet;
using SlackNet.Blocks;
using SlackNet.Events;
using SlackNet.Interaction;
using SlackNet.WebApi;
using Button = SlackNet.Blocks.Button;
using ILogger = Microsoft.Extensions.Logging.ILogger;
using Option = SlackNet.Blocks.Option;

namespace SlackBot.Event;

/// <summary>
///     Opens a modal view with a range of different inputs.
/// </summary>
public class ModalViewDemo : IEventHandler<MessageEvent>, IBlockActionHandler<ButtonAction>, IViewSubmissionHandler
{
    public const string Trigger = "modal demo";
    public const string OpenModal = "open_modal";
    private const string InputBlockId = "input_block";
    private const string InputActionId = "text_input";
    private const string SingleSelectActionId = "single_select";
    private const string MultiSelectActionId = "multi_select";
    private const string DatePickerActionId = "date_picker";
    private const string TimePickerActionId = "time_picker";
    private const string RadioActionId = "radio";
    private const string CheckboxActionId = "checkbox";
    private const string SingleUserActionId = "single_user";
    public const string ModalCallbackId = "modal_demo";
    private readonly ILogger _log;

    private readonly ISlackApiClient _slack;

    public ModalViewDemo(ISlackApiClient slack, ILogger<ModalViewDemo> log)
    {
        _slack = slack;
        _log = log;
    }

    public async Task Handle(ButtonAction action, BlockActionRequest request)
    {
        _log.LogInformation("{UserName} clicked the Open modal button in the {ChannelName} channel", request.User.Name,
            request.Channel.Name);

        await _slack.Views.Open(request.TriggerId, new ModalViewDefinition
        {
            Title = "Example Modal",
            CallbackId = ModalCallbackId,
            Blocks =
            {
                new InputBlock
                {
                    Label = "Input",
                    BlockId = InputBlockId,
                    Optional = true,
                    Element = new PlainTextInput
                    {
                        ActionId = InputActionId,
                        Placeholder = "Enter some text",
                    },
                },
                new InputBlock
                {
                    Label = "Single-select",
                    BlockId = "single_select_block",
                    Optional = true,
                    Element = new StaticSelectMenu
                    {
                        ActionId = SingleSelectActionId,
                        Options = ExampleOptions(),
                    },
                },
                new InputBlock
                {
                    Label = "Multi-select",
                    BlockId = "multi_select_block",
                    Optional = true,
                    Element = new StaticMultiSelectMenu
                    {
                        ActionId = MultiSelectActionId,
                        Options = ExampleOptions(),
                    },
                },
                new InputBlock
                {
                    Label = "Date",
                    BlockId = "date_block",
                    Optional = true,
                    Element = new DatePicker { ActionId = DatePickerActionId },
                },
                new InputBlock
                {
                    Label = "Time",
                    BlockId = "time_block",
                    Optional = true,
                    Element = new TimePicker { ActionId = TimePickerActionId },
                },
                new InputBlock
                {
                    Label = "Radio options",
                    BlockId = "radio_block",
                    Optional = true,
                    Element = new RadioButtonGroup
                    {
                        ActionId = RadioActionId,
                        Options = ExampleOptions(),
                    },
                },
                new InputBlock
                {
                    Label = "Checkbox options",
                    BlockId = "checkbox_block",
                    Optional = true,
                    Element = new CheckboxGroup
                    {
                        ActionId = CheckboxActionId,
                        Options = ExampleOptions(),
                    },
                },
                new InputBlock
                {
                    Label = "Single user select",
                    BlockId = "single_user_block",
                    Optional = true,
                    Element = new UserSelectMenu
                    {
                        ActionId = SingleUserActionId,
                    },
                },
            },
            Submit = "Submit",
            NotifyOnClose = true,
            PrivateMetadata =
                JsonSerializer.Serialize(new ModalMetadata(request.Channel.Id,
                    request.Channel.Name)), // Holding onto this info for later
        });
    }

    public async Task Handle(MessageEvent slackEvent)
    {
        if (slackEvent.Text?.Contains(Trigger, StringComparison.OrdinalIgnoreCase) == true)
        {
            _log.LogInformation("{Name} asked for a modal view demo in the {Channel} channel",
                (await _slack.Users.Info(slackEvent.User)).Name,
                (await _slack.Conversations.Info(slackEvent.Channel)).Name);

            await _slack.Chat.PostMessage(new Message
            {
                Channel = slackEvent.Channel,
                Blocks =
                {
                    new SectionBlock { Text = "Here's the modal view demo" },
                    new ActionsBlock
                    {
                        Elements =
                        {
                            new Button
                            {
                                ActionId = OpenModal,
                                Text = "Open modal",
                            },
                        },
                    },
                },
            });
        }
    }

    public async Task<ViewSubmissionResponse> Handle(ViewSubmission viewSubmission)
    {
        var metadata = JsonSerializer.Deserialize<ModalMetadata>(viewSubmission.View.PrivateMetadata)!;
        _log.LogInformation("{UserName} submitted the demo modal view in the {MetadataChannelName} channel",
            viewSubmission.User.Name, metadata.ChannelName);

        var state = viewSubmission.View.State;
        var values = new Dictionary<string, string>
        {
            { "Input", state.GetValue<PlainTextInputValue>(InputActionId).Value ?? "none" },
            {
                "Single-select",
                state.GetValue<StaticSelectValue>(SingleSelectActionId).SelectedOption?.Text.Text ?? "none"
            },
            {
                "Multi-select",
                string.Join(", ",
                    state.GetValue<StaticMultiSelectValue>(MultiSelectActionId).SelectedOptions.Select(o => o.Text)
                        .DefaultIfEmpty("none"))
            },
            {
                "Date",
                state.GetValue<DatePickerValue>(DatePickerActionId).SelectedDate?.ToString("yyyy-MM-dd") ?? "none"
            },
            { "Time", state.GetValue<TimePickerValue>(TimePickerActionId).SelectedTime?.ToString("hh\\:mm") ?? "none" },
            {
                "Radio options",
                state.GetValue<RadioButtonGroupValue>(RadioActionId).SelectedOption?.Text.Text ?? "none"
            },
            {
                "Checkbox options",
                string.Join(", ",
                    state.GetValue<CheckboxGroupValue>(CheckboxActionId).SelectedOptions.Select(o => o.Text)
                        .DefaultIfEmpty("none"))
            },
            {
                "Single user select",
                state.GetValue<UserSelectValue>(SingleUserActionId).SelectedUser is string userId
                    ? Link.User(userId).ToString()
                    : "none"
            },
        };

        await _slack.Chat.PostMessage(new Message
        {
            Channel = metadata.ChannelId,
            Text = $"You entered: {state.GetValue<PlainTextInputValue>(InputActionId).Value}",
            Blocks =
            {
                new SectionBlock
                {
                    Text = new Markdown("You entered:\n"
                                        + string.Join("\n", values.Select(kv => $"*{kv.Key}:* {kv.Value}"))),
                },
            },
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

    private static IList<Option> ExampleOptions()
    {
        return new List<Option>
        {
            new() { Text = "One", Value = "1" },
            new() { Text = "Two", Value = "2" },
            new() { Text = "Three", Value = "3" },
        };
    }

    private record ModalMetadata(string ChannelId, string ChannelName);
}
