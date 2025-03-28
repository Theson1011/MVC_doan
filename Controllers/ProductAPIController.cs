using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ThuchanhMVC.Models;
using ThuchanhMVC.Models.ProductModels;

namespace ThuchanhMVC.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProductAPIController : ControllerBase
    {
        QlbanVaLiContext db = new QlbanVaLiContext();
        [HttpGet]
        public IEnumerable<Product> GetAllProducts()
        {
            var sanPham = (from p in db.TDanhMucSps
                           select new Product
                           {
                               MaSp = p.MaSp,
                               TenSp = p.TenSp,
                               MaLoai = p.MaLoai,
                               AnhDaiDien = p.AnhDaiDien,
                               GiaNhoNhat = p.GiaNhoNhat
                           }).ToList();
            return sanPham;
        }

        [HttpGet("{maloai}")]
        public IEnumerable<Product> GetProductsByCategory(string maloai)
        {
            var sanPham = db.TDanhMucSps  // Đổi thành bảng chứa danh sách sản phẩm
                        .Where(p => p.MaLoai == maloai)
                        .Select(p => new Product
                        {
                            MaSp = p.MaSp,
                            TenSp = p.TenSp,
                            MaLoai = p.MaLoai,
                            AnhDaiDien = p.AnhDaiDien,
                            GiaNhoNhat = p.GiaNhoNhat
                        }).ToList();
            return sanPham;
        }
    }
}
