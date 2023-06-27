using SlackNet;
using SlackNet.Events;
using SlackNet.WebApi;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace SlackBot.Event;

public class PingDemo : IEventHandler<MessageEvent>
{
    public const string Trigger = "ping";
    private readonly ILogger _log;
    private readonly ISlackApiClient _slack;

    public PingDemo(ISlackApiClient slack, ILogger<PingDemo> log)
    {
        _slack = slack;
        _log = log;
    }

    public async Task Handle(MessageEvent slackEvent)
    {
        if (slackEvent.Text?.Contains(Trigger, StringComparison.OrdinalIgnoreCase) == true)
        {
            _log.LogInformation("Received ping from {User} in the {Channel} channel",
                (await _slack.Users.Info(slackEvent.User)).Name,
                (await _slack.Conversations.Info(slackEvent.Channel)).Name);
            await _slack.Chat.PostMessage(new Message
            {
                Text = "pong",
                Channel = slackEvent.Channel,
            });
        }
    }
}
