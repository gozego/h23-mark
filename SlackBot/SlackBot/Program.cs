using SlackBot.Command;
using SlackBot.Event;
using SlackNet.AspNetCore;
using SlackNet.Blocks;
using SlackNet.Events;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

var slackSettings = builder.Configuration.GetSection("Slack").Get<SlackSettings>();
builder.Services.Configure<JiraSettings>(builder.Configuration.GetSection("Jira"));
builder.Services.AddSlackNet(c =>
{
    c.UseApiToken(slackSettings?.ApiToken);
    c.UseAppLevelToken(slackSettings?.AppLevelToken);

    c.RegisterEventHandler<MessageEvent, JiraHandler>();
    c.RegisterBlockActionHandler<ButtonAction, JiraHandler>(JiraHandler.OpenModal);
    c.RegisterViewSubmissionHandler<JiraHandler>(JiraHandler.ModalCallbackId);

    c.RegisterEventHandler<MessageEvent, PingDemo>();

    c.RegisterEventHandler<AppHomeOpened, AppHome>();

    c.RegisterEventHandler<MessageEvent, CounterDemo>();
    c.RegisterBlockActionHandler<ButtonAction, CounterDemo>(CounterDemo.Add1);
    c.RegisterBlockActionHandler<ButtonAction, CounterDemo>(CounterDemo.Add5);
    c.RegisterBlockActionHandler<ButtonAction, CounterDemo>(CounterDemo.Add10);

    c.RegisterEventHandler<MessageEvent, ModalViewDemo>();
    c.RegisterBlockActionHandler<ButtonAction, ModalViewDemo>(ModalViewDemo.OpenModal);
    c.RegisterViewSubmissionHandler<ModalViewDemo>(ModalViewDemo.ModalCallbackId);

    c.RegisterSlashCommandHandler<EchoDemo>(EchoDemo.SlashCommand);
});
builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseSlackNet(c =>
{
    c.UseSigningSecret(slackSettings?.SigningSecret);
    c.UseSocketMode(app.Environment.IsDevelopment());
});

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

internal record SlackSettings
{
    public string ApiToken { get; init; } = string.Empty;
    public string AppLevelToken { get; init; } = string.Empty;
    public string SigningSecret { get; init; } = string.Empty;
}

public record JiraSettings
{
    public string ApiToken { get; init; } = string.Empty;
    public string Login { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
}
