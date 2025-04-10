using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using ThuchanhMVC.Models;
using ThuchanhMVC.Models.Authentication;
using ThuchanhMVC.ViewModels;
using X.PagedList;

namespace ThuchanhMVC.Controllers
{
	public class HomeController : Controller
	{
		QlbanVaLiContext db = new QlbanVaLiContext();
		private readonly ILogger<HomeController> _logger;

		public HomeController(ILogger<HomeController> logger)
		{
			_logger = logger;
		}
        //[Authentication]

        public IActionResult Index(int? page)
        {
            int pageSize = 8;
            int pageNumber = page == null || page < 0 ? 1 : page.Value;

            db.Database.SetCommandTimeout(180); 

            var lstsanpham = db.TDanhMucSps.AsNoTracking().OrderBy(x => x.TenSp);
            PagedList<TDanhMucSp> lst = new PagedList<TDanhMucSp>(lstsanpham, pageNumber, pageSize);
            return View(lst);
        }

        //[Authentication]

        public IActionResult SanPhamTheoLoai(String maloai, int? page=1)
		{
            int pageSize = 8;
            int pageNumber = page == null || page < 0 ? 1 : page.Value;
            var lstsanpham = db.TDanhMucSps.AsNoTracking().Where
                               (x => x.MaLoai == maloai).OrderBy(x => x.TenSp);

            PagedList<TDanhMucSp> lst = new PagedList<TDanhMucSp>(lstsanpham,
                               pageNumber, pageSize);
			ViewBag.maloai=maloai;
            return View(lst);
        }

		public IActionResult ChiTietSanPham (String maSp)
		{
            var sanPham = db.TDanhMucSps.SingleOrDefault(x => x.MaSp == maSp);
            if (sanPham == null)
            {
                return NotFound(); 
            }

            var anhSanPham = db.TAnhSps.Where(x => x.MaSp == maSp).ToList();
            ViewBag.anhSanPham = anhSanPham;

            return View(sanPham); 
        }

      
        public IActionResult ProductDetail(string MaSp)
        {
            var product = db.TDanhMucSps
                .Include(p => p.TAnhSps)
                .FirstOrDefault(p => p.MaSp == MaSp);

            if (product == null) return NotFound();

            var viewModel = new HomeProductDetailViewModel
            {
                danhMucSp = product,
                anhSps = product.TAnhSps.ToList()
            };

            return View(viewModel);
        }


        public IActionResult Privacy()
		{
			return View();
		}

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
		public IActionResult Error()
		{
			return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
		}

        [HttpGet]
        public IActionResult Profile()
        {
            var model = new TUser
            {
                Username = Request.Cookies["UserName"],
                Email = Request.Cookies["Email"],
                PhoneNumber = Request.Cookies["PhoneNumber"],
                Address = Request.Cookies["Address"]
            };

            return View(model);
        }

        [HttpPost]
        public IActionResult Profile(TUser updatedUser)
        {
            var userInDb = db.TUsers.FirstOrDefault(x => x.Username == updatedUser.Username);
            if (userInDb != null)
            {
                userInDb.Email = updatedUser.Email;
                userInDb.PhoneNumber = updatedUser.PhoneNumber;
                userInDb.Address = updatedUser.Address;

                db.SaveChanges();

                // Cập nhật lại cookies
                Response.Cookies.Append("Email", updatedUser.Email);
                Response.Cookies.Append("PhoneNumber", updatedUser.PhoneNumber);
                Response.Cookies.Append("Address", updatedUser.Address);

                ViewBag.Message = "Update successfull!";
            }
            return RedirectToAction("Profile", updatedUser);
        }

    }
}
