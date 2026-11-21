using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using EduPlatform.Data;

namespace EduPlatform.Controllers
{
    public class BaseController : Controller
    {
        protected readonly ApplicationDbContext _context;

        public BaseController(ApplicationDbContext context)
        {
            _context = context;
        }

        public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId != null)
            {
                var user = await _context.Students.FindAsync(userId);
                if (user != null)
                {
                    ViewBag.UserFullName = string.IsNullOrWhiteSpace(user.LastName)
                        ? user.Name
                        : $"{user.Name} {user.LastName}";

                    ViewBag.UserAvatarPath = user.AvatarPath;
                }
            }
            await base.OnActionExecutionAsync(context, next);
        }
    }
}