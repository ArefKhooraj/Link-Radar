using FluentValidation;
using Hangfire;
using LinkRadar.Api.Configuration;
using LinkRadar.Api.Endpoints;
using LinkRadar.Api.Statics;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.AddSqlServerConfiguration();

builder.Configuration.GetSection("DataForSeo").Get<DataForSeo>();
builder.Services.AddDataForSeoConfiguration();

builder.Configuration.GetSection("OpenAi").Get<OpenAi>();
builder.Services.AddOpenAiConfiguration();

builder.Configuration.GetSection("AvalAi").Get<AvalAi>();
builder.Services.AddAvalAiConfiguration();

builder.Services.AddServiceConfiguration();
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

builder.Configuration.GetSection("Hangfire").Get<LinkRadar.Api.Statics.Hangfire>();
builder.Services.AddHangfireConfiguration(builder.Configuration);
var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseHangfireDashboard();


app.RegisterAllEndpoints();
app.Run();