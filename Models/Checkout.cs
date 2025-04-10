using System;
using System.Collections.Generic;

namespace ThuchanhMVC.Models;

public partial class Checkout
{
    public int Id { get; set; }

    public string Username { get; set; } = null!;

    public string Address { get; set; } = null!;

    public string Email { get; set; } = null!;
    public string PhoneNumber { get; set; } = null!;

    public decimal Total { get; set; }

    public DateTime? PaymentDate { get; set; }
}
