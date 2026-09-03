using BCLMSApi.Data;
using BCLMSApi.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddScoped<Datalayer>();
builder.Services.AddScoped<IAuthRepository, AuthRepository>();
builder.Services.AddScoped<IFormalBusinessRepository, FormalBusinessRepository>();
builder.Services.AddScoped<IComplaintRepository, ComplaintRepository>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IFormalBusinessService, FormalBusinessService>();
builder.Services.AddScoped<IComplaintService, ComplaintService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<ISmsService, SmsService>();
builder.Services.AddScoped<ISystemSettingsService, SystemSettingsService>();
builder.Services.AddSingleton<IUserTokenService, UserTokenService>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("BclmsDevelopmentCors", policy =>
    {
        var allowedOrigins = builder.Configuration
            .GetSection("Portal:AllowedOrigins")
            .Get<string[]>()
            ?? ["http://localhost:4200", "http://localhost:4201", "http://ce19viis071/", "http://ce19viis071/BCLMS/"];

        policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});
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

if (app.Environment.IsProduction())
{
    app.UseHttpsRedirection();
}

app.UseCors("BclmsDevelopmentCors");

app.UseAuthorization();

app.MapControllers();

app.Run();
