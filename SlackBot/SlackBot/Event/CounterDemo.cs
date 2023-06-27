using System.Text.RegularExpressions;
using SlackNet;
using SlackNet.Blocks;
using SlackNet.Events;
using SlackNet.Interaction;
using SlackNet.WebApi;
using Button = SlackNet.Blocks.Button;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace SlackBot.Event;

/// <summary>
///     Displays an interactive message that updates itself.
/// </summary>
public class CounterDemo : IEventHandler<MessageEvent>, IBlockActionHandler<ButtonAction>
{
    private const string ActionPrefix = "add";
    public const string Add1 = ActionPrefix + "1";
    public const string Add5 = ActionPrefix + "5";
    public const string Add10 = ActionPrefix + "10";
    public const string Trigger = "counter demo";
    private static readonly Regex CounterPattern = new("Counter: (\\d+)");
    private readonly ILogger _log;

    private readonly ISlackApiClient _slack;

    public CounterDemo(ISlackApiClient slack, ILogger<CounterDemo> log)
    {
        _slack = slack;
        _log = log;
    }

    private static List<Block> Blocks => new()
    {
        new SectionBlock { Text = "Counter: 0" },
        new ActionsBlock
        {
            Elements =
            {
                new Button
                {
                    ActionId = Add1,
                    Value = "1",
                    Text = new PlainText("Add 1"),
                },
                new Button
                {
                    ActionId = Add5,
                    Value = "5",
                    Text = new PlainText("Add 5"),
                },
                new Button
                {
                    ActionId = Add10,
                    Value = "10",
                    Text = new PlainText("Add 10"),
                },
            },
        },
    };

    public async Task Handle(ButtonAction button, BlockActionRequest request)
    {
        _log.LogInformation("{UserName} clicked on the Add {ButtonValue} button in the {ChannelName} channel",
            request.User.Name, button.Value, request.Channel.Name);

        var counter = SectionBeforeAddButtons(button, request);
        if (counter != null)
        {
            var counterText = CounterPattern.Match(counter.Text.Text ?? string.Empty);
            if (counterText.Success)
            {
                var count = int.Parse(counterText.Groups[1].Value);
                var increment = int.Parse(((ButtonAction)request.Action).Value);
                counter.Text = $"Counter: {count + increment}";
                await _slack.Chat.Update(new MessageUpdate
                {
                    Ts = request.Message.Ts,
                    Text = request.Message.Text,
                    Blocks = request.Message.Blocks,
                    ChannelId = request.Channel.Id,
                });
            }
        }
    }

    public async Task Handle(MessageEvent slackEvent)
    {
        if (slackEvent.Text?.Contains(Trigger, StringComparison.OrdinalIgnoreCase) == true)
        {
            _log.LogInformation("{Name} asked for a counter demo in the {Channel} channel",
                (await _slack.Users.Info(slackEvent.User)).Name,
                (await _slack.Conversations.Info(slackEvent.Channel)).Name);

            await _slack.Chat.PostMessage(new Message
            {
                Channel = slackEvent.Channel,
                Blocks = Blocks,
            });
        }
    }

    private static SectionBlock? SectionBeforeAddButtons(ButtonAction button, BlockActionRequest request)
    {
        return request.Message.Blocks
            .TakeWhile(b => b.BlockId != button.BlockId)
            .LastOrDefault() as SectionBlock;
    }
}
