using MeetSpace.Api.Extensions;
using MeetSpace.DataAccess;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMeetSpaceOptions(builder.Configuration);
builder.Services.AddMeetSpaceDataAccess(builder.Configuration);

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.Run();
