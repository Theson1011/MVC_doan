using Microsoft.AspNetCore.Mvc;
using ThuchanhMVC.Helpers;
using ThuchanhMVC.Models;
using ThuchanhMVC.ViewModels;
using System.Linq;

namespace ThuchanhMVC.Controllers
{
    public class CartController : Controller
    {
        private QlbanVaLiContext db = new QlbanVaLiContext();

        const string CART_KEY = "MYCART";

        // Lấy danh sách Cart từ session, nếu chưa có thì trả về danh sách trống
        public List<CartItem> Cart => HttpContext.Session.Get<List<CartItem>>(CART_KEY) ?? new List<CartItem>();

        [HttpGet]
        public IActionResult Index()
        {
            // Tính tổng giá trị giỏ hàng
            var total = Cart.Sum(item => item.ThanhTien);

            // Truyền giỏ hàng và tổng vào View 
            ViewBag.CartTotal = total;

            // Truyền giỏ hàng vào View 
            return View(Cart);
        }

        [HttpPost]
        public IActionResult AddToCart(string id, int quantity = 1)
        {
            // Lấy giỏ hàng hiện tại
            var gioHang = Cart;

            // Kiểm tra nếu sản phẩm đã có trong giỏ hàng chưa
            var item = gioHang.SingleOrDefault(p => p.MaSp == id);
            if (item == null)
            {
                // Nếu chưa có thì lấy thông tin sản phẩm từ cơ sở dữ liệu
                var hangHoa = db.TDanhMucSps.SingleOrDefault(p => p.MaSp == id);
                if (hangHoa == null)
                {
                    // Nếu không tìm thấy sản phẩm, thông báo lỗi và chuyển đến trang 404
                    TempData["Message"] = $"Không tìm thấy hàng hóa có mã {id}";
                    return Redirect("/404");
                }
                // Tạo mới một đối tượng CartItem và thêm vào giỏ hàng
                item = new CartItem
                {
                    MaSp = hangHoa.MaSp,
                    TenSp = hangHoa.TenSp,
                    DonGia = hangHoa.DonGia,
                    //DonGia = hangHoa.DonGia ?? 0,
                    anhSps = hangHoa.AnhDaiDien ?? string.Empty,
                    SoLuong = quantity
                };
                gioHang.Add(item);
            }
            else
            {
                // Nếu sản phẩm đã có trong giỏ hàng, chỉ cần tăng số lượng
                item.SoLuong += quantity;
            }

            // Lưu giỏ hàng vào session
            HttpContext.Session.Set(CART_KEY, gioHang);

            // Trả về View giỏ hàng sau khi thêm
            return RedirectToAction("Index", gioHang);
        }
        public IActionResult RemoveCart(string id)
        {
            var gioHang = Cart;
            var item = gioHang.SingleOrDefault(p => p.MaSp == id);
            if (item != null)
            {
                gioHang.Remove(item);
                HttpContext.Session.Set(CART_KEY, gioHang);
            }
            return RedirectToAction("Index", gioHang);
        }

        [HttpPost]
        public IActionResult UpdateCart(string id, int quantity)
        {
            // Lấy giỏ hàng hiện tại
            var gioHang = Cart;

            // Tìm sản phẩm trong giỏ hàng theo mã sản phẩm
            var item = gioHang.SingleOrDefault(p => p.MaSp == id);
            if (item != null)
            {
                // Cập nhật số lượng sản phẩm
                item.SoLuong = quantity;

                // Cập nhật lại giỏ hàng trong session
                HttpContext.Session.Set(CART_KEY, gioHang);
            }

            // Trả về View giỏ hàng sau khi cập nhật
            return RedirectToAction("Index", gioHang);
        }

        [HttpGet]
        public IActionResult Blog() { return View(); }  
        
        [HttpGet]
        public IActionResult Contact() { return View(); }


        [HttpGet]
        
        public IActionResult Checkout()
        {
            var cart = Cart;
            // Tính tổng tiền giỏ hàng
            var total = cart.Sum(item => item.ThanhTien);
            ViewBag.CartTotal = total;
            // Truyền giỏ hàng vào View thông qua ViewBag để hiển thị danh sách sản phẩm
            ViewBag.CartItems = cart;

            // Chuẩn bị model Checkout, lấy thông tin user từ Cookies
            var checkoutModel = new Checkout
            {
                Username = Request.Cookies["UserName"] ?? "",
                PhoneNumber = Request.Cookies["PhoneNumber"] ?? "",
                Address = Request.Cookies["Address"] ?? "",
                Email = Request.Cookies["Email"] ?? ""
            };

            // Trả về view với model Checkout (dùng để hiển thị form thanh toán)
            return View(checkoutModel);
        }

        //public IActionResult Checkout()
        //{
        //    var cart = Cart;
        //    // Tính tổng tiền giỏ hàng
        //    var total = cart.Sum(item => item.ThanhTien);
        //    ViewBag.CartTotal = total;
        //    // Truyền giỏ hàng vào View thông qua ViewBag để hiển thị danh sách sản phẩm
        //    ViewBag.CartItems = cart;

        //    // Chuẩn bị model Checkout, lấy thông tin user từ Cookies
        //    var checkoutModel = new Checkout
        //    {
        //        Username = Request.Cookies["UserName"] ?? "",
        //        PhoneNumber = Request.Cookies["PhoneNumber"] ?? "",
        //        Address = Request.Cookies["Address"] ?? "",
        //        Email = Request.Cookies["Email"] ?? ""
        //    };

        //    // Trả về view với model Checkout (dùng để hiển thị form thanh toán)
        //    return View(checkoutModel);
        //}




        [HttpPost]
        public IActionResult Checkout(Checkout model)
        {
            var cart = Cart;
            if (cart == null || !cart.Any())
            {
                TempData["Message"] = "Giỏ hàng rỗng!";
                return RedirectToAction("Index");
            }

            // Nếu người dùng thay đổi thông tin trên form thì dùng thông tin từ form,
            // nếu trống thì cập nhật lại từ cookie (mặc định)
            model.Username = string.IsNullOrEmpty(model.Username) ? (Request.Cookies["UserName"] ?? "Guest") : model.Username;
            model.Address = string.IsNullOrEmpty(model.Address) ? Request.Cookies["Address"] : model.Address;
            model.Email = string.IsNullOrEmpty(model.Email) ? Request.Cookies["Email"] : model.Email;
            model.PhoneNumber = string.IsNullOrEmpty(model.PhoneNumber) ? Request.Cookies["PhoneNumber"] : model.PhoneNumber;

            // Tính tổng tiền giỏ hàng
            model.Total = cart.Sum(x => x.ThanhTien);
            model.PaymentDate = DateTime.Now;

            // Lưu đơn hàng vào CSDL
            db.Checkouts.Add(model);
            db.SaveChanges();

            // Xóa giỏ hàng trong session sau khi thanh toán thành công
            HttpContext.Session.Remove(CART_KEY);
            TempData["Message"] = "Thanh toán thành công!";

            return RedirectToAction("Index", "Home");
        }


    }
}
