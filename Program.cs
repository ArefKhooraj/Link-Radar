using Hangfire;
using LinkRadar.Api.Configuration;
using LinkRadar.Api.Statics;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.AddSqlServerConfiguration();

builder.Configuration.GetSection("DataForSeo").Get<DataForSeo>();
builder.Services.AddDataForSeoConfiguration();
builder.Services.AddServiceConfiguration();

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



app.Run();

