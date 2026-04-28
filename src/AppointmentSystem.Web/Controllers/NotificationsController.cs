using System.Security.Claims;
using AppointmentSystem.Bll.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AppointmentSystem.Web.Controllers;

[Authorize]
public class NotificationsController : Controller
{
    private readonly INotificationService _notifications;
    public NotificationsController(INotificationService n) => _notifications = n;
    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public async Task<IActionResult> Index()
    {
        var list = await _notifications.GetForUserAsync(CurrentUserId);
        await _notifications.MarkAllReadAsync(CurrentUserId);
        return View(list);
    }
}
