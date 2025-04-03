using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ThuchanhMVC.Models;

public partial class TUser
{

    [RegularExpression(@"^\S+$", ErrorMessage = "Tên đăng nhập không được chứa khoảng trắng.")]
    public string Username { get; set; } = null!;
    public string Password { get; set; } = null!;
    public string Email { get; set; } = null!;

    [RegularExpression(@"^\d{10}$", ErrorMessage = "Số điện thoại phải là số và có đúng 10 chữ số.")]
    public string PhoneNumber { get; set; } = null!;
    public string Address { get; set; } = null!;

    public byte? LoaiUser { get; set; }

    public virtual ICollection<TKhachHang> TKhachHangs { get; set; } = new List<TKhachHang>();

    public virtual ICollection<TNhanVien> TNhanViens { get; set; } = new List<TNhanVien>();
}
