using MeetSpace.Api.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMeetSpaceOptions(builder.Configuration);

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.Run();
