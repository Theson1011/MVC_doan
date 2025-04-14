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
using System.Net;
using Microsoft.EntityFrameworkCore;
using ThuchanhMVC.Helpers;
using System.Globalization;

namespace ThuchanhMVC.Controllers
{
    public class MessageRequest
    {
        public string Message { get; set; }
    }

    public class AnalysisResult
    {
        public string requestType { get; set; }
        public string productName { get; set; }
        public string categoryName { get; set; }
    }

    public static class ProductNameNormalizer
    {
        public static string Normalize(string input)
        {
            // Loại bỏ từ thừa và ký tự đặc biệt
            var cleaned = Regex.Replace(input, @"\b(cái|chiếc|sản phẩm|tìm|kiếm)\b", "", RegexOptions.IgnoreCase);

            // Chuẩn hóa khoảng trắng
            cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim();

            // Xử lý viết hoa chữ đầu
            return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(cleaned.ToLower());
        }
    }

    public class ChatController : Controller
    {
        private readonly string _apiKey;
        private readonly string _apiUrl = "https://api.openai.com/v1/chat/completions";
        private readonly QlbanVaLiContext db;
        private static string _lastResponse = null;

        public ChatController(QlbanVaLiContext context, IConfiguration configuration)
        {
            db = context;
            _apiKey = configuration["OpenAI:ApiKey"];
        }

        [HttpPost]
        public async Task<JsonResult> GetChatbotResponse([FromBody] MessageRequest request)
        {
            Console.WriteLine("Message Taken: " + request.Message);
            var analysisResult = await AnalyzeMessageWithChatApi(request.Message);

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
                case "store_address":
                    return GetStoreAddress();
                case "contact":
                    return GetContact();
                case "price_range_search":
                    return await GetProductsByPriceAndCategory(analysisResult.TenSp, analysisResult.Loai);
                case "product_search": // Thêm case mới
                    return await GetProductsByName(analysisResult.TenSp);
                default:
                    return await GetGptResponse(request.Message);
            }
        }


        private async Task<(string requestType, string TenSp, string Loai)> AnalyzeMessageWithChatApi(string message)
        {
            try
            {
                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");

                    var prompt = @"PHÂN TÍCH CÂU HỎI VÀ TRẢ VỀ JSON THEO MẪU SAU CHÍNH XÁC:
                    {
                        ""requestType"": ['product_search','product_info','category_search','price_compare','similar_products', 'store_address', 'contact', 'price_range_search'],
                        ""productName"": ""Tên sản phẩm (viết hoa chữ đầu, chuẩn hóa cách gọi)"",
                        ""categoryName"": ""Tên danh mục (viết thường không dấu)""
                    }

                    QUY TẮC PHÂN LOẠI CHẶT CHẼ:

                    Ⅰ. LOẠI YÊU CẦU (requestType):
                    1. 'product_search' - Khi tìm kiếm chung theo tên sản phẩm:
                       • Từ khóa: 'tìm', 'kiếm', 'có', 'hiện có', 'nào không'
                       • KHÔNG có điều kiện giá/thông tin chi tiết
                       • Ví dụ: 
                         - 'Tìm vali du lịch' → product_search
                         - 'Có balo chống nước không?' → product_search

                    2. 'product_info' - Khi yêu cầu THÔNG TIN CHI TIẾT sản phẩm:
                       • Từ khóa: 'thông tin', 'chi tiết', 'mô tả', 'công dụng', 'thông số'
                       • Ví dụ:
                         - 'Cho xem thông tin vali Samsonite' → product_info

                    3. 'category_search' - Khi tìm theo DANH MỤC CHUNG:
                       • Từ khóa: 'loại', 'dòng', 'hạng mục', 'danh mục', 'kiểu'
                       • KHÔNG có tên sản phẩm cụ thể
                       • Ví dụ:
                         - 'Hiện có những loại balo nào?' → category_search

                    4. 'price_range_search' - Khi có ĐIỀU KIỆN GIÁ + DANH MỤC:
                       • Từ khóa: 'giá', 'dưới', 'trên', 'khoảng', 'từ X đến Y'
                       • Ví dụ: 
                         - 'Tìm túi xách dưới 500k' → price_range_search

                    5. 'similar_products' - Khi yêu cầu SẢN PHẨM TƯƠNG TỰ:
                       • Từ khóa: 'tương tự', 'giống', 'tương đương', 'gần giống', 'khác'
                       • Phải có tên sản phẩm gốc
                       • Ví dụ:
                         - 'Cho xem các vali tương tự' → similar_products

                    6. 'contact' - Khi cần thông tin liên hệ:
                       • Từ khóa: 'số điện thoại', 'zalo', 'hotline', 'email', 'liên hệ'

                    Ⅱ. QUY TẮC CHUẨN HÓA DỮ LIỆU:
                    1. productName:
                       - Viết hoa chữ đầu mỗi từ
                       - Bỏ từ thừa: 'cái', 'chiếc', 'sản phẩm', 'tìm', 'kiếm'
                       - Giữ nguyên thương hiệu: 'Vali Samsonite 20in'
                       - Ví dụ: 
                         Input: 'tìm cái balo du lịch' → 'Balo Du Lịch'

                    2. categoryName:
                       - Chuyển về danh mục gốc trong DB
                       - Dùng tên không dấu, viết thường
                       - Ví dụ:
                         Input: 'dòng Vali kéo' → 'vali'

                    Ⅲ. ƯU TIÊN PHÂN LOẠI:
                        1. Luôn ưu tiên price_range_search nếu có điều kiện giá
                        2. Ưu tiên similar_products nếu có từ chỉ sự so sánh
                        3. Phân biệt product_search vs category_search:
                           - Có tên SP cụ thể → product_search
                           - Chỉ có loại sản phẩm → category_search

                    IV. XỬ LÝ CÂU PHỨC TẠP:
                    - Ưu tiên price_range_search nếu có cả điều kiện giá và danh mục
                    - Kết hợp product_info + category_search khi có cả tên SP và loại
                    - Bỏ qua từ cảm thán: 'ơi', 'nhé', 'giúp tôi'

                    V. VÍ DỤ MẪU:
                    1. 'Tìm giúp tôi vali du lịch giá dưới 1.5 triệu' →
                    {
                        ""requestType"": ""price_range_search"",
                        ""productName"": ""Dưới 1500000"",
                        ""categoryName"": ""vali""
                    }

                    2. 'Cửa hàng có những loại túi xách nào đẹp?' →
                    {
                        ""requestType"": ""category_search"",
                        ""productName"": """",
                        ""categoryName"": ""tui-xach""
                    }

                    3. 'Cho xem số điện thoại liên hệ' →
                    {
                        ""requestType"": ""contact"",
                        ""productName"": """",
                        ""categoryName"": """"
                    }

                    4. 'Sản phẩm tương tự balo thể thao này?' →
                    {
                        ""requestType"": ""similar_products"",
                        ""productName"": ""Balo Thể Thao"",
                        ""categoryName"": """"
                    }

                    5. 'Chỗ các bạn ở quận mấy?' →
                    {
                        ""requestType"": ""store_address"",
                        ""productName"": """",
                        ""categoryName"": """"
                    }
                    6. 'Tìm sản phẩm vali du lịch' →
                    {
                        ""requestType"": ""product_info"",
                        ""productName"": ""Vali Du Lịch"",
                        ""categoryName"": """"
                    }
                    7. 'Tôi cần mua balo' → 
                    {
                        ""requestType"": ""product_search"",
                        ""productName"": ""Balo"",
                        ""categoryName"": """"
                    }

                    8. 'Cho xem các loại vali' → 
                    {
                        ""requestType"": ""product_search"",
                        ""productName"": ""Vali"",
                        ""categoryName"": """"
                    }

                    9. 'Thông số kỹ thuật vali du lịch' → 
                    {
                        ""requestType"": ""product_info"",
                        ""productName"": ""Vali Du Lịch"",
                        ""categoryName"": """"
                    }

                    10. 'Còn vali nào khác không?' → 
                    {
                        ""requestType"": ""similar_products"",
                        ""productName"": ""Vali"",
                        ""categoryName"": """"
                    }

                    11. 'Tìm túi xách da giá từ 1-2 triệu' → 
                    {
                        ""requestType"": ""price_range_search"",
                        ""productName"": ""Từ 1000000 Đến 2000000"",
                        ""categoryName"": ""tui-xach""
                    }
                    12. 'danh mục wallet' → 
                    {
                        ""requestType"": ""category_search"",
                        ""productName"": """",
                        ""categoryName"": ""Wallet""
                    }
                    13. 'Giới thiệu sản phẩm suitcase' →
                    {
                        ""requestType"": ""category_search"",
                        ""productName"": """",
                         ""categoryName"": ""suitcase""
                    }

                    CÂU HỎI CẦN PHÂN TÍCH: " + message;

                    var requestBody = new
                    {
                        model = "gpt-3.5-turbo",
                        messages = new[] { new { role = "user", content = prompt } },
                        max_tokens = 150,
                        response_format = new { type = "json_object" } // Thêm yêu cầu trả về JSON
                    };

                    var jsonContent = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");
                    var response = await client.PostAsync(_apiUrl, jsonContent);

                    if (response.IsSuccessStatusCode)
                    {
                        var responseData = await response.Content.ReadAsStringAsync();
                        Console.WriteLine("Raw OpenAI response: " + responseData); // Debug

                        try
                        {
                            var result = JsonConvert.DeserializeObject<dynamic>(responseData);
                            string analysisJson = result.choices[0].message.content.ToString().Trim();

                            // Thêm validation
                            if (string.IsNullOrWhiteSpace(analysisJson))
                                return ("general", null, null);

                            var analysis = JsonConvert.DeserializeObject<AnalysisResult>(analysisJson);

                            // Chuẩn hóa tên sản phẩm
                            var tenSp = Regex.Replace(analysis.productName ?? "", @"\s+", " ").Trim();

                            return (analysis.requestType, tenSp, analysis.categoryName);

                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"JSON Parse Error: {ex.Message}");
                            return ("general", null, null);
                        }
                    }
                    return ("general", null, null);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"API Error: {ex.Message}");
                return ("general", null, null);
            }
        }

        private async Task<JsonResult> GetProductInfo(string TenSp)
        {
            if (string.IsNullOrWhiteSpace(TenSp))
                return Json(new { reply = "Vui lòng cung cấp tên sản phẩm cụ thể hơn!" });

            try
            {
                var products = await db.TDanhMucSps
                    .Where(p =>
                        p.TenSp.ToLower().Contains(TenSp.ToLower()) ||
                        EF.Functions.Like(p.TenSp, $"%{TenSp}%")
                    )
                    .Take(3)
                    .ToListAsync();

                if (products.Any())
                {
                    var baseUrl = $"{Request.Scheme}://{Request.Host}";
                    var response = new StringBuilder("<p><strong>Kết quả tìm kiếm:</strong></p><ul>");

                    foreach (var p in products)
                    {
                        var productUrl = Url.Action("ProductDetail", "Home", new { MaSp = p.MaSp });
                        response.AppendLine($"<li><a href='{baseUrl}{productUrl}' target='_blank'>{p.TenSp}</a> - ${p.DonGia:N0} </li>");
                    }

                    response.Append("</ul>");

                    return Json(new { reply = response.ToString() });
                }

                return Json(new { reply = $"<p>Không tìm thấy sản phẩm '<strong>{TenSp}</strong>'</p>" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Database Error: {ex.Message}");
                return Json(new { reply = "<p><strong>Lỗi hệ thống khi tìm sản phẩm.</strong></p>" });
            }
        }


        private async Task<JsonResult> GetProductsByCategory(string Loai)
        {
            if (string.IsNullOrEmpty(Loai))
                return Json(new { reply = "Vui lòng cung cấp danh mục!" });

            // Chuẩn hóa tên danh mục
            var normalizedCategory = Loai.Trim().ToLower();

            // Tạo session key unique
            string sessionKey = $"CatProducts_{normalizedCategory}";

            // Lấy danh sách ID đã hiển thị từ session
            List<string> shownProductIds = HttpContext.Session.Get<List<string>>(sessionKey) ?? new List<string>();

            // Truy vấn cơ sở dữ liệu
            var query = db.TDanhMucSps
                .Where(p => p.MaLoaiNavigation != null
                         && p.MaLoaiNavigation.Loai.ToLower() == normalizedCategory
                         && !shownProductIds.Contains(p.MaSp)) // Loại trừ sản phẩm đã hiển thị
                .OrderBy(p => p.MaSp); // Sắp xếp cố định

            var products = await query
                .Take(3)
                .Select(p => new {
                    Id = p.MaSp,
                    Info = $"{p.TenSp} - {p.DonGia.ToString("N0")} (/Product/Detail/{p.MaSp})"
                })
                .ToListAsync();

            if (products.Any())
            {
                // Cập nhật session
                shownProductIds.AddRange(products.Select(p => p.Id));
                HttpContext.Session.Set(sessionKey, shownProductIds);

                // Xây dựng response
                var response = new StringBuilder();
                response.AppendLine($"Sản phẩm trong danh mục '{Loai}':");
                response.AppendJoin("\n", products.Select(p => $"• {p.Info}"));

                return Json(new
                {
                    reply = response.ToString(),
                    hasMore = await query.Skip(shownProductIds.Count).AnyAsync() // Kiểm tra còn sản phẩm
                });
            }
            else
            {
                // Reset session nếu không còn sản phẩm
                if (shownProductIds.Count > 0)
                {
                    HttpContext.Session.Remove(sessionKey);
                    return Json(new
                    {
                        reply = $"Đã hiển thị tất cả sản phẩm trong danh mục '{Loai}'. Nhập lại để xem từ đầu!"
                    });
                }

                return Json(new
                {
                    reply = $"Không tìm thấy sản phẩm nào trong danh mục '{Loai}'!"
                });
            }
        }

        private async Task<JsonResult> CompareProductPrices(string TenSp)
        {
            if (string.IsNullOrEmpty(TenSp))
                return Json(new { reply = "Vui lòng cung cấp tên sản phẩm!" });

            try
            {
                // Tìm tất cả sản phẩm cùng loại
                var baseProduct = await db.TDanhMucSps
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.TenSp.ToLower().Contains(TenSp.ToLower()));

                if (baseProduct == null)
                    return Json(new { reply = $"Không tìm thấy sản phẩm '{TenSp}' để so sánh!" });

                // Tạo session key
                string sessionKey = $"PriceCompare_{baseProduct.MaSp}";
                List<string> excludedIds = HttpContext.Session.Get<List<string>>(sessionKey) ?? new List<string>();

                // Truy vấn so sánh
                var query = db.TDanhMucSps
                    .AsNoTracking()
                    .Where(p => p.MaLoai == baseProduct.MaLoai &&
                              p.MaSp != baseProduct.MaSp &&
                              !excludedIds.Contains(p.MaSp))
                    .OrderBy(p => p.DonGia);

                // Lấy phân khúc giá
                var priceSegments = await query
                    .GroupBy(p => Math.Floor(p.DonGia / 1000000)) // Nhóm theo triệu đồng
                    .Take(3)
                    .Select(g => new {
                        PriceRange = $"{g.Key * 1000000:N0} - {(g.Key + 1) * 1000000:N0}₫",
                        Products = g.Take(3).ToList()
                    })
                    .ToListAsync();

                if (priceSegments.Any())
                {
                    var response = new StringBuilder();
                    response.AppendLine($"Phân tích giá cho '{baseProduct.TenSp}':");
                    response.AppendLine($"• Giá hiện tại: ${baseProduct.DonGia:N0}");

                    foreach (var segment in priceSegments)
                    {
                        response.AppendLine($"\nMức giá {segment.PriceRange}:");
                        foreach (var p in segment.Products)
                        {
                            response.AppendLine($"  - {p.TenSp} (${p.DonGia:N0})");
                            excludedIds.Add(p.MaSp);
                        }
                    }

                    HttpContext.Session.Set(sessionKey, excludedIds);

                    return Json(new
                    {
                        reply = response.ToString(),
                        basePrice = baseProduct.DonGia,
                        hasMore = await query.AnyAsync(p => !excludedIds.Contains(p.MaSp))
                    });
                }

                return Json(new { reply = $"Không có sản phẩm nào khác để so sánh với '{TenSp}'" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi so sánh giá: {ex.Message}");
                return Json(new { reply = "Lỗi hệ thống khi so sánh giá" });
            }
        }

        private async Task<JsonResult> GetSimilarProducts(string TenSp)
        {
            if (string.IsNullOrEmpty(TenSp))
                return Json(new { reply = "Vui lòng cung cấp tên sản phẩm!" });

            try
            {
                // Tìm sản phẩm gốc
                var product = await db.TDanhMucSps
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.TenSp.ToLower().Contains(TenSp.ToLower()));

                if (product == null)
                    return Json(new { reply = $"Không tìm thấy sản phẩm '{TenSp}' để gợi ý tương tự!" });

                // Tạo session key dựa trên mã sản phẩm gốc
                string sessionKey = $"SimilarProducts_{product.MaSp}";
                List<string> excludedIds = HttpContext.Session.Get<List<string>>(sessionKey) ?? new List<string>();

                // Thêm cả ID sản phẩm gốc vào danh sách loại trừ
                excludedIds.Add(product.MaSp);

                // Truy vấn sản phẩm tương tự
                var similarProducts = await db.TDanhMucSps
                    .AsNoTracking()
                    .Where(p => p.MaLoai == product.MaLoai && !excludedIds.Contains(p.MaSp))
                    .OrderBy(p => p.MaSp) // Giữ thứ tự cố định
                    .Take(3)
                    .Select(p => new {
                        MaSp = p.MaSp,
                        Info = $"{p.TenSp} - ${p.DonGia.ToString("N0")} (/Product/Detail/{p.MaSp})"
                    })
                    .ToListAsync();

                if (similarProducts.Any())
                {
                    // Cập nhật session
                    excludedIds.AddRange(similarProducts.Select(p => p.MaSp));
                    HttpContext.Session.Set(sessionKey, excludedIds);

                    var response = new StringBuilder();
                    response.AppendLine($"Sản phẩm tương tự '{product.TenSp}':");
                    response.AppendJoin("\n", similarProducts.Select(p => $"• {p.Info}"));

                    // Kiểm tra còn sản phẩm
                    var remainingCount = await db.TDanhMucSps
                        .CountAsync(p => p.MaLoai == product.MaLoai
                                      && !excludedIds.Contains(p.MaSp));

                    return Json(new
                    {
                        reply = response.ToString(),
                        hasMore = remainingCount > 0
                    });
                }
                else
                {
                    // Reset nếu đã hiển thị hết
                    if (excludedIds.Count > 1) // >1 vì đã thêm ID gốc
                    {
                        HttpContext.Session.Remove(sessionKey);
                        return Json(new
                        {
                            reply = $"Đã hiển thị tất cả sản phẩm tương tự '{product.TenSp}'. Nhập lại để xem từ đầu!"
                        });
                    }

                    return Json(new
                    {
                        reply = $"Không tìm thấy sản phẩm tương tự cho '{TenSp}'!"
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi tìm sản phẩm tương tự: {ex.Message}");
                return Json(new { reply = "Lỗi hệ thống khi tìm sản phẩm tương tự" });
            }
        }

        private async Task<JsonResult> GetGptResponse(string message)
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
            var requestBody = new { model = "gpt-4", messages = new[] { new { role = "user", content = message } }, max_tokens = 20 };
            var jsonContent = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");
            var response = await client.PostAsync(_apiUrl, jsonContent);
            var raw = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                dynamic err = JsonConvert.DeserializeObject<dynamic>(raw);
                string code = err.error.code;
                if (code == "insufficient_quota")
                {
                    return Json(new { reply = "Hiện tại bạn đã vượt hạn mức sử dụng. Vui lòng kiểm tra lại gói cước hoặc nạp thêm tài khoản OpenAI của bạn." });
                }
                return Json(new { reply = $"Lỗi từ OpenAI ({(int)response.StatusCode}): {err.error.message}" });
            }

            var result = JsonConvert.DeserializeObject<dynamic>(raw);
            string chatbotReply = result.choices[0].message.content;
            return Json(new { reply = chatbotReply });
        }

        private JsonResult GetStoreAddress()
        {
            const string address = "Số 10 ngõ 42 Sài Đồng, Long Biên, Hà Nội";
            const string mapUrl = "https://maps.app.goo.gl/GArEFXtFHY8dQzoFA";

            return Json(new
            {
                reply = $"Địa chỉ cửa hàng của chúng tôi:\n{address}\nXem trên bản đồ: {mapUrl}",
                address = address,
                map = mapUrl
            });
        }

        private JsonResult GetContact()
        {
            const string phoneNumber = "+88 88.888.888";
            const string email = "support@company.com";
            const string hotlineHours = "8:00 - 22:00 hàng ngày";

            return Json(new
            {
                reply = $"Thông tin liên hệ:\n" +
                        $"📞 Hotline: {phoneNumber} ({hotlineHours})\n" +
                        $"📧 Email: {email}",
                phone = phoneNumber,
                email = email,
                workingHours = hotlineHours
            });
        }

        private async Task<JsonResult> GetProductsByPriceAndCategory(string priceCondition, string category)
        {
            try
            {
                // Parse điều kiện giá
                var (comparison, priceValue) = ParsePriceCondition(priceCondition);
                if (priceValue == -1) return Json(new { reply = "Định dạng giá không hợp lệ" });

                // Tìm danh mục
                var loai = await db.TLoaiSps
                    .FirstOrDefaultAsync(l => l.Loai.ToLower() == category.ToLower());

                if (loai == null) return Json(new { reply = $"Không tìm thấy danh mục '{category}'" });

                // Tạo session key unique cho từng loại truy vấn
                string sessionKey = $"ExcludedProducts_{category}_{priceCondition}";
                List<string> excludedIds = HttpContext.Session.Get<List<string>>(sessionKey) ?? new List<string>();

                // Truy vấn sản phẩm loại trừ các ID đã hiển thị
                IQueryable<TDanhMucSp> query = db.TDanhMucSps
                    .Where(p => p.MaLoai == loai.MaLoai && !excludedIds.Contains(p.MaSp));

                query = comparison switch
                {
                    "<" => query.Where(p => p.DonGia < priceValue),
                    ">" => query.Where(p => p.DonGia > priceValue),
                    _ => query.Where(p => p.DonGia == priceValue)
                };

                var products = await query
                    .OrderBy(p => p.DonGia)
                    .Take(3)
                    .Select(p => new
                    {
                        MaSp = p.MaSp,
                        Name = p.TenSp,
                        Price = p.DonGia.ToString("N0") + "VNĐ",
                        Url = $"/Product/Detail/{p.MaSp}"
                    })
                    .ToListAsync();

                var response = new StringBuilder();
                if (products.Any())
                {
                    // Cập nhật session với các ID mới
                    excludedIds.AddRange(products.Select(p => p.MaSp));
                    HttpContext.Session.Set(sessionKey, excludedIds);

                    response.AppendLine($"Tìm thấy {products.Count} sản phẩm {category} giá {priceCondition.Replace(" ", "")}:");
                    foreach (var p in products)
                    {
                        response.AppendLine($"• {p.Name} - {p.Price} ({p.Url})");
                    }
                }
                else
                {
                    if (excludedIds.Count > 0)
                    {
                        // Reset lại session nếu không còn sản phẩm
                        HttpContext.Session.Remove(sessionKey);
                        response.AppendLine("Đã hiển thị tất cả sản phẩm phù hợp. Bắt đầu lại từ đầu!");
                    }
                    else
                    {
                        response.AppendLine($"Không có sản phẩm nào trong danh mục {category} với giá {priceCondition}");
                    }
                }

                return Json(new
                {
                    reply = response.ToString(),
                    products
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi tìm kiếm theo giá: {ex.Message}");
                return Json(new { reply = "Lỗi hệ thống khi tìm kiếm" });
            }
        }

        private (string comparison, decimal priceValue) ParsePriceCondition(string condition)
        {
            try
            {
                var match = Regex.Match(condition.ToLower(),
                    @"(dưới|trên|khoảng giá|từ|đến|under|over|between|price range)\s*(\d+[\.,]?\d*)(?:\s*(?:đến|to)\s*(\d+[\.,]?\d*))?");

                if (!match.Success) return (null, -1);

                decimal priceFrom = 0, priceTo = 0;
                var comparison = match.Groups[1].Value.ToLower() switch
                {
                    "dưới" or "under" => "<",
                    "trên" or "over" => ">",
                    "khoảng giá" or "từ" or "đến" or "between" => "between",
                    _ => "="
                };

                // Xử lý trường hợp khoảng giá
                if (comparison == "between" && match.Groups.Count >= 4)
                {
                    if (!decimal.TryParse(match.Groups[2].Value.Replace(",", ""), out priceFrom) ||
                        !decimal.TryParse(match.Groups[3].Value.Replace(",", ""), out priceTo))
                        return (null, -1);

                    return ("between", priceFrom * 1000); // Giả sử giá được nhập theo nghìn
                }

                var priceStr = match.Groups[2].Value.Replace(",", "");
                if (!decimal.TryParse(priceStr, out var price)) return (null, -1);

                return (comparison, price);
            }
            catch
            {
                return (null, -1);
            }
        }

        private async Task<JsonResult> GetProductsByName(string productName)
        {
            if (string.IsNullOrWhiteSpace(productName))
                return Json(new { reply = "Vui lòng nhập tên sản phẩm cần tìm!" });

            try
            {
                var normalizedName = productName.Trim().ToLower();
                string sessionKey = $"ProductSearch_{normalizedName}";
                List<string> excludedIds = HttpContext.Session.Get<List<string>>(sessionKey) ?? new List<string>();

                var query = db.TDanhMucSps
                    .AsNoTracking()
                    .Where(p =>
                        p.TenSp.ToLower().Contains(normalizedName) &&
                        !excludedIds.Contains(p.MaSp));

                var products = await query
                    .Take(3)
                    .Select(p => new {
                        MaSp = p.MaSp,
                        Info = $"{p.TenSp} - ${p.DonGia.ToString("N0")} (/Product/Detail/{p.MaSp})"
                    })
                    .ToListAsync();

                if (products.Any())
                {
                    // Cập nhật session
                    excludedIds.AddRange(products.Select(p => p.MaSp));
                    HttpContext.Session.Set(sessionKey, excludedIds);

                    var response = new StringBuilder();
                    response.AppendLine($"Kết quả tìm kiếm cho '{productName}':");
                    response.AppendJoin("\n", products.Select(p => $"• {p.Info}"));

                    return Json(new
                    {
                        reply = response.ToString(),
                        hasMore = await query.Skip(excludedIds.Count).AnyAsync()
                    });
                }
                else
                {
                    if (excludedIds.Count > 0)
                    {
                        HttpContext.Session.Remove(sessionKey);
                        return Json(new
                        {
                            reply = $"Đã hiển thị tất cả kết quả cho '{productName}'. Nhập lại để xem từ đầu!"
                        });
                    }

                    return Json(new
                    {
                        reply = $"Không tìm thấy sản phẩm nào phù hợp với '{productName}'"
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi tìm kiếm sản phẩm: {ex.Message}");
                return Json(new { reply = "Lỗi hệ thống khi tìm kiếm" });
            }
        }
    }
}