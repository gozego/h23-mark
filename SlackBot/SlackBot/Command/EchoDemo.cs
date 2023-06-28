using SlackNet.Interaction;
using SlackNet.WebApi;

namespace SlackBot.Command;

/// <summary>
///     A slash command handler that just echos back whatever you sent it
/// </summary>
public class EchoDemo : ISlashCommandHandler
{
    public const string SlashCommand = "/echo";
    private readonly ILogger _log;

    public EchoDemo(ILogger<EchoDemo> log)
    {
        _log = log;
    }

    public async Task<SlashCommandResponse> Handle(SlashCommand command)
    {
        _log.LogInformation(
            "{CommandUserName} used the {SlashCommand} slash command in the {CommandChannelName} channel",
            command.UserName, SlashCommand, command.ChannelName);

        return new SlashCommandResponse
        {
            Message = new Message
            {
                Text = command.Text,
            },
        };
    }
}
