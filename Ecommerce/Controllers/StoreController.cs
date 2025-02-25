using ecommerce.Models;
using ecommerce.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using sib_api_v3_sdk.Client;

namespace ecommerce.Controllers
{
    public class StoreController : Controller
    {
        private readonly ApplicationDbContext context;
        private readonly int pageSize = 12;
        private readonly decimal shippingFee;
        public StoreController(ApplicationDbContext context, IConfiguration configuration)
        {
            this.context = context;
            shippingFee = configuration.GetValue<decimal>("CartSettings:ShippingFee");
        }
        public IActionResult Index(int pageIndex, string? search, string? brand, string? category, string? sort)
        {
            IQueryable<Product> query = context.Products;

            // Search functionality
            if (search != null && search.Length >0)
            {
                query = query.Where(p => p.Name.Contains(search));
            }

            // Filter functionality
            if (brand != null && brand.Length > 0)
            {
                query = query.Where(p => p.Brand.Contains(brand));
            }

            if (category != null && category.Length > 0)
            {
                query = query.Where(p => p.Category.Contains(category));
            }

            // Sort functionality
            if (sort == "price_asc")
            {
                query = query.OrderBy(p => p.Price);
            }
            else if (sort == "price_desc")
            {
                query = query.OrderByDescending(p => p.Price);
            }
            else
            {
                // Latest Product
                query = query.OrderByDescending(p => p.Id);
            }

            // Pagination functionality
            if (pageIndex < 1)
            {
                pageIndex = 1;
            }

            decimal count = query.Count();
            int totalPages = (int)Math.Ceiling(count / pageSize);
            query = query.Skip((pageIndex - 1) * pageSize).Take(pageSize);

            var products = query.ToList();

            ViewBag.Products = products;
            ViewBag.PageIndex = pageIndex;
            ViewBag.TotalPages = totalPages;

            var storeSearchModel = new StoreSearchModel()
            {
                Search = search,
                Brand = brand,
                Category = category,
                Sort = sort
            };

            List<OrderItem> cartItems = CartHelper.GetCartItems(Request, Response, context);
            decimal subtotal = CartHelper.GetSubtotal(cartItems);
            ViewBag.CartItems = cartItems;
            ViewBag.ShippingFee = shippingFee;
            ViewBag.Subtotal = subtotal;
            ViewBag.Total = subtotal + shippingFee;

            return View(storeSearchModel);
        }
        public IActionResult Details(int id)
        {
            var product = context.Products.Find(id);
            if (product == null)
            {
                return RedirectToAction("Index", "Store");
            }

            List<OrderItem> cartItems = CartHelper.GetCartItems(Request, Response, context);
            var cartItem = cartItems.FirstOrDefault(item => item.Product.Id == id);
            ViewBag.CartQuantity = cartItem != null ? cartItem.Quantity : 0;

            decimal subtotal = CartHelper.GetSubtotal(cartItems);
            ViewBag.CartItems = cartItems;
            ViewBag.ShippingFee = shippingFee;
            ViewBag.Subtotal = subtotal;
            ViewBag.Total = subtotal + shippingFee;

            return View(product);
        }

        public IActionResult Category(string category, int pageIndex = 1)
        {
            if (string.IsNullOrEmpty(category))
            {
                return RedirectToAction("Index");
            }

            // ดึงสินค้าจากฐานข้อมูลตามหมวดหมู่
            IQueryable<Product> query = context.Products.Where(p => p.Category == category);

            // คำนวณจำนวนสินค้าทั้งหมดในหมวดหมู่
            int totalProducts = query.Count();
            int totalPages = (int)Math.Ceiling(totalProducts / (double)pageSize);

            // ตรวจสอบค่าของ pageIndex ให้อยู่ในช่วงที่เหมาะสม
            if (pageIndex < 1)
            {
                pageIndex = 1;
            }
            if (pageIndex > totalPages && totalPages > 0)
            {
                pageIndex = totalPages; // ไม่ให้ pageIndex เกินจำนวนหน้าที่มีอยู่
            }

            // ดึงข้อมูลสินค้าเฉพาะในหน้านั้น
            var products = query
                .OrderByDescending(p => p.Id)
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            // ส่งข้อมูลไปยัง View
            ViewBag.Category = category;
            ViewBag.Products = products;
            ViewBag.PageIndex = pageIndex;
            ViewBag.TotalPages = totalPages;

            return View("Category");
        }
    }
}
