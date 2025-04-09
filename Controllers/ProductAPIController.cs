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
                               DonGia = p.DonGia
                           }).ToList();
            return sanPham;
        }

        [HttpGet("{maloai}")]
        public IEnumerable<Product> GetProductsByCategory(string maloai)
        {
            var sanPham = db.TDanhMucSps
                        .Where(p => p.MaLoai == maloai)
                        .Select(p => new Product
                        {
                            MaSp = p.MaSp,
                            TenSp = p.TenSp,
                            MaLoai = p.MaLoai,
                            AnhDaiDien = p.AnhDaiDien,
                            DonGia = p.DonGia
                        }).ToList();
            return sanPham;
        }

        //// Updated endpoint to support count parameter
        //[HttpGet("budget/{budgetUSD}/{count=5}")]
        //public IEnumerable<Product> GetProductsUnderBudget(decimal budgetUSD, int count = 5)
        //{
        //    // Set a reasonable maximum to prevent performance issues
        //    if (count > 20) count = 20;

        //    var sanPham = db.TDanhMucSps
        //        .Where(p => p.DonGia.HasValue && p.DonGia <= budgetUSD)
        //        .OrderByDescending(p => p.DonGia)
        //        .Take(count)
        //        .Select(p => new Product
        //        {
        //            MaSp = p.MaSp,
        //            TenSp = p.TenSp,
        //            MaLoai = p.MaLoai,
        //            AnhDaiDien = p.AnhDaiDien,
        //            DonGia = p.DonGia
        //        }).ToList();

        //    return sanPham;
        //}
    }
}