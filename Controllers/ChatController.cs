using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Linq;
using ThuchanhMVC.Models.ProductModels;
using System.Text.RegularExpressions;
using ThuchanhMVC.Models;
using Microsoft.VisualBasic.FileIO;

namespace ThuchanhMVC.Controllers
{
    public class ChatController : Controller
    {
        private readonly string _apiKey = "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";
        private readonly string _apiUrl = "https://api.openai.com/v1/chat/completions";
        private readonly QlbanVaLiContext db;
        private static string _lastResponse = null;

        public ChatController(QlbanVaLiContext context)
        {
            db = context;
        }

        [HttpPost]
        public async Task<JsonResult> GetChatbotResponse(string message)
        {
            var analysisResult = await AnalyzeMessageWithChatApi(message);

            switch (analysisResult.requestType)
            {
                case "product_info":
                    return await GetProductInfo(analysisResult.TenSp);
                case "category_search":
                    return await GetProductsByCategory(analysisResult.Loai);
                case "price_compare":
                    return await CompareProductPrices(analysisResult.TenSp);
                case "similar_products":
                    return await GetSimilarProducts(analysisResult.TenSp);
                default:
                    return await GetGptResponse(message);
            }
        }

        private async Task<(string requestType, string TenSp, string Loai)> AnalyzeMessageWithChatApi(string message)
        {
            try
            {
                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");

                    var prompt = "Phân tích câu tiếng Việt sau và xác định loại yêu cầu. Trả về JSON với các trường: " +
                               "{ \"requestType\": string (các giá trị: 'product_info', 'category_search', 'price_compare', 'stock_check', 'similar_products', 'general'), " +
                               "\"productName\": string, \"categoryName\": string }. Đảm bảo hiểu đúng ngữ nghĩa tiếng Việt.\n\n" +
                               $"Câu: \"{message}\"";

                    var requestBody = new
                    {
                        model = "gpt-4",
                        messages = new[] { new { role = "user", content = prompt } },
                        max_tokens = 150
                    };

                    var jsonContent = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");
                    var response = await client.PostAsync(_apiUrl, jsonContent);

                    if (response.IsSuccessStatusCode)
                    {
                        var responseData = await response.Content.ReadAsStringAsync();
                        var result = JsonConvert.DeserializeObject<dynamic>(responseData);
                        string analysisJson = result.choices[0].message.content;

                        var analysis = JsonConvert.DeserializeObject<dynamic>(analysisJson);
                        return ((string)analysis.requestType, (string)analysis.TenSp, (string)analysis.Loai);
                    }

                    return ("general", null, null);
                }
            }
            catch (Exception)
            {
                return ("general", null, null);
            }
        }

        private async Task<JsonResult> GetProductInfo(string TenSp)
        {
            if (string.IsNullOrEmpty(TenSp)) return Json(new { reply = "Vui lòng cung cấp tên sản phẩm!" });

            var product = db.TDanhMucSps.FirstOrDefault(p => p.TenSp.ToLower().Contains(TenSp.ToLower()));
            if (product != null)
            {
                string response = $"Thông tin sản phẩm:\n" +
                                $"Tên: {product.TenSp}\n" +
                                $"Giá: {product.DonGia.ToString("N0")} VNĐ\n" + // Định dạng tiền tệ tiếng Việt
                                $"Mô tả: {product.GioiThieuSp ?? "Không có mô tả"}\n" +
                                $"Hình ảnh: {product.AnhDaiDien ?? "Không có hình ảnh"}\n" +
                                $"Link: /HangHoa/Detail/{product.MaSp}";
                _lastResponse = response;
                return Json(new { reply = response });
            }
            return Json(new { reply = $"Không tìm thấy sản phẩm '{TenSp}'!" });
        }

        private async Task<JsonResult> GetProductsByCategory(string Loai)
        {
            if (string.IsNullOrEmpty(Loai)) return Json(new { reply = "Vui lòng cung cấp danh mục!" });

            var products = db.TDanhMucSps
                .Where(p => p.MaLoaiNavigation != null && p.MaLoaiNavigation.Loai.ToLower().Contains(Loai.ToLower()))
                .Take(3)
                .Select(p => $"{p.TenSp} - {p.DonGia.ToString("N0")} VNĐ (/Product/Detail/{p.MaSp})")
                .ToList();

            if (products.Any())
            {
                string response = $"Sản phẩm trong danh mục '{Loai}':\n" + string.Join("\n", products);
                _lastResponse = response;
                return Json(new { reply = response });
            }
            return Json(new { reply = $"Không tìm thấy sản phẩm nào trong danh mục '{Loai}'!" });
        }

        private async Task<JsonResult> CompareProductPrices(string TenSp)
        {
            if (string.IsNullOrEmpty(TenSp)) return Json(new { reply = "Vui lòng cung cấp tên sản phẩm!" });

            var products = db.TDanhMucSps
                .Where(p => p.TenSp.ToLower().Contains(TenSp.ToLower()))
                .OrderBy(p => p.DonGia)
                .Take(3)
                .Select(p => $"{p.TenSp}  -  {p.DonGia.ToString("N0")} VNĐ (/Product/Detail/{p.MaSp})")
                .ToList();

            if (products.Any())
            {
                string response = $"So sánh giá cho '{TenSp}':\n" + string.Join("\n", products);
                _lastResponse = response;
                return Json(new { reply = response });
            }
            return Json(new { reply = $"Không tìm thấy sản phẩm nào để so sánh giá cho '{TenSp}'!" });
        }


        private async Task<JsonResult> GetSimilarProducts(string TenSp)
        {
            if (string.IsNullOrEmpty(TenSp)) return Json(new { reply = "Vui lòng cung cấp tên sản phẩm!" });

            var product = db.TDanhMucSps.FirstOrDefault(p => p.TenSp.ToLower().Contains(TenSp.ToLower()));
            if (product != null && !string.IsNullOrEmpty(product.MaLoai))
            {
                var similarProducts = db.TDanhMucSps
                    .Where(p => p.MaLoai == product.MaLoai && p.MaSp != product.MaSp)
                    .Take(3)
                    .Select(p => $"{p.TenSp} - {p.DonGia.ToString("N0")} VNĐ (/Product/Detail/{p.MaSp})")
                    .ToList();

                if (similarProducts.Any())
                {
                    string response = $"Sản phẩm tương tự '{product.TenSp}':\n" + string.Join("\n", similarProducts);
                    _lastResponse = response;
                    return Json(new { reply = response });
                }
                return Json(new { reply = $"Không tìm thấy sản phẩm tương tự cho '{TenSp}'!" });
            }
            return Json(new { reply = $"Không tìm thấy sản phẩm '{TenSp}' để gợi ý tương tự!" });
        }


        private async Task<JsonResult> GetGptResponse(string message)
        {
            try
            {
                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
                    var requestBody = new { model = "gpt-4", messages = new[] { new { role = "user", content = message } }, max_tokens = 150 };
                    var jsonContent = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");
                    var response = await client.PostAsync(_apiUrl, jsonContent);

                    if (response.IsSuccessStatusCode)
                    {
                        var responseData = await response.Content.ReadAsStringAsync();
                        var result = JsonConvert.DeserializeObject<dynamic>(responseData);
                        string chatbotReply = result.choices[0].message.content;
                        _lastResponse = chatbotReply;
                        return Json(new { reply = chatbotReply });
                    }
                    return Json(new { reply = "Có lỗi xảy ra khi kết nối với GPT!" });
                }
            }
            catch (Exception ex)
            {
                return Json(new { reply = $"Đã xảy ra lỗi: {ex.Message}" });
            }
        }
    }
}