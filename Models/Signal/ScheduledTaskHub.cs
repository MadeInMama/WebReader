using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace WebReader.Models.Signal;

[Authorize]
public class ScheduledTaskHub : Hub
{
}
