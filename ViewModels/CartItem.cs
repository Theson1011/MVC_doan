namespace ThuchanhMVC.ViewModels
{
    public class CartItem
    {
        public string MaSp { get; set; }
        public string anhSps { get; set; }
        public string TenSp { get; set; }
        public decimal DonGia { get; set; } // decimal
        public int SoLuong { get; set; }
        public decimal ThanhTien => SoLuong * DonGia; 

    }
}
