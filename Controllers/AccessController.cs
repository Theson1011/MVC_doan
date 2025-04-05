using Microsoft.AspNetCore.Mvc;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.EntityFrameworkCore;
using ThuchanhMVC.Models;

namespace ThuchanhMVC.Controllers
{
    public class AccessController : Controller
    {
        QlbanVaLiContext db=new QlbanVaLiContext();
        [HttpGet]
        public IActionResult Login()
        {
            if (HttpContext.Session.GetString("UserName") == null)
            {
                return View();
            }
            else
            {
                return RedirectToAction("Index", "Home");
            }
        }

        [HttpPost]
        public IActionResult Login(TUser user)
        {
            var u = db.TUsers
                .AsNoTracking()
                .Where(x => x.Username == user.Username && x.Password == user.Password)
                .FirstOrDefault();

            if (u != null)
            {
                Response.Cookies.Append("UserName", u.Username, new CookieOptions
                {
                    Expires = DateTimeOffset.Now.AddMinutes(30),
                    HttpOnly = true // Tăng bảo mật
                });
                if (u.LoaiUser == 0)
                {
                    return RedirectToAction("Index", "HomeAdmin");
                }
                else if (u.LoaiUser == 1)
                {
                    return RedirectToAction("Index", "Home");
                }
                return RedirectToAction("Index", "Home");
            }
            return View();
        }

        [HttpGet]
        public IActionResult SignUp()
        {
            return View();
        }

        [HttpPost]
        public IActionResult SignUp(TUser newUser)
        {
            if (!ModelState.IsValid)
            {
                return View(newUser); // Hiển thị lại form với thông báo lỗi
            }
            // Kiểm tra username đã tồn tại chưa
            var existingUser = db.TUsers.FirstOrDefault(x => x.Username == newUser.Username);
            if (existingUser != null)
            {
                TempData["Error"] = "Sign Up fail! Because Username is exist.";
                return RedirectToAction("SignUp");
            }
            newUser.LoaiUser = 1;
            // Thêm người dùng mới vào database
            db.TUsers.Add(newUser);
            db.SaveChanges();

            TempData["Success"] = "Sign Up successfully!";


            return RedirectToAction("SignUp");
        }





        [HttpGet] // Dùng GET cho đơn giản
        public IActionResult Logout()
        {
            Response.Cookies.Delete("UserName");
            return RedirectToAction("Login", "Access");
        }
    }
}
