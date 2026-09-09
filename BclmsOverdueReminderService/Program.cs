using BclmsOverdueReminderService;
var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddWindowsService(options => options.ServiceName = "BCLMS Overdue Reminder Service");
builder.Services.AddSingleton<EmailTemplateRenderer>();
builder.Services.AddHostedService<OverdueReminderWorker>();
await builder.Build().RunAsync();
