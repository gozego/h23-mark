using SlackNet;
using SlackNet.Blocks;
using SlackNet.Events;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace SlackBot.Event;

public class AppHome : IEventHandler<AppHomeOpened>
{
    private readonly ILogger _log;
    private readonly ISlackApiClient _slack;

    public AppHome(ISlackApiClient slack, ILogger<AppHome> log)
    {
        _slack = slack;
        _log = log;
    }

    public async Task Handle(AppHomeOpened slackEvent)
    {
        if (slackEvent.Tab == AppHomeTab.Home)
        {
            _log.LogInformation("{Name} opened the app's home view", (await _slack.Users.Info(slackEvent.User)).Name);

            await _slack.Views.Publish(slackEvent.User, new HomeViewDefinition
            {
                Blocks =
                {
                    new SectionBlock
                    {
                        Text = new Markdown($@"Welcome to the SlackNet example. Here's what you can do:
• Say ""{PingDemo.Trigger}"" to get back a pong
• Say ""{CounterDemo.Trigger}"" to get the counter demo
• Say ""{ModalViewDemo.Trigger}"" to open then modal view demo
• Use the `{EchoDemo.SlashCommand}` slash command to see an echo
• Set up a {Link.Url("https://api.slack.com/workflows", "workflow")} to automate sending messages to people"),
                    },
                },
            }, slackEvent.View?.Hash);
        }
    }
}
