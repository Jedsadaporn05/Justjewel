using ecommerce.Models;
using ecommerce.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Stripe;
using Stripe.Checkout;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using System.Runtime.InteropServices;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Fields;
using MigraDoc.DocumentObjectModel.Shapes;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using PdfSharp.Pdf;
using System.IO;

namespace ecommerce.Controllers
{
    public class CartController : Controller
    {
        private readonly ApplicationDbContext context;
        private readonly UserManager<ApplicationUser> userManager;
        private readonly decimal shippingFee;

        public CartController(ApplicationDbContext context, IConfiguration configuration
            , UserManager<ApplicationUser> userManager)
        {
            this.context = context;
            this.userManager = userManager;
            shippingFee = configuration.GetValue<decimal>("CartSettings:ShippingFee");
            StripeConfiguration.ApiKey = configuration["Stripe:SecretKey"];
        }

        private byte[] GenerateInvoicePdf(Order order, ProfileDto profile)
        {
            var document = new Document();
            var section = document.AddSection();

            string imagePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "Logo.png");
            // เพิ่มรูปภาพใน Header ของ Section
            Image image = section.Headers.Primary.AddImage("wwwroot/images/Logo.png");
            // ปรับขนาดของรูปภาพ
            image.Height = "7cm";
            image.LockAspectRatio = true;

            // จัดตำแหน่งรูปภาพให้อยู่ตรงกลาง
            image.RelativeVertical = RelativeVertical.Page; // จัดให้อยู่ตำแหน่งสัมพันธ์กับหน้า
            image.RelativeHorizontal = RelativeHorizontal.Page; // จัดให้อยู่สัมพันธ์กับหน้า
            image.Top = ShapePosition.Top; // อยู่ด้านบนสุด
            image.Left = ShapePosition.Right;

            // ปรับการแสดงผลเพื่อไม่ให้ Header ทับพื้นที่ของเอกสาร
            image.WrapFormat.Style = WrapStyle.TopBottom; // รูปภาพไม่ซ้อนทับกับเนื้อหา


            // Add Title
            var headerParagraph = section.AddParagraph("RECEIPT / INVOICE");
            headerParagraph.Format.Font.Size = 20;
            headerParagraph.Format.Font.Bold = true;
            headerParagraph.Format.Alignment = ParagraphAlignment.Left;
            headerParagraph.Format.SpaceAfter = Unit.FromCentimeter(1);
            section.PageSetup.TopMargin = Unit.FromCentimeter(3); // เพิ่มระยะห่างด้านบน 3 ซม.

            // General Information
            var invoiceInfoTable = section.AddTable();
            invoiceInfoTable.Borders.Visible = false;
            invoiceInfoTable.AddColumn(Unit.FromCentimeter(3)); // Label column
            invoiceInfoTable.AddColumn(Unit.FromCentimeter(12)); // Value column

            var row = invoiceInfoTable.AddRow();
            row.Cells[0].MergeRight = 1; // รวมเซลล์ให้เหลือเพียงเซลล์เดียว
            row.Cells[0].AddParagraph($"Date : {order.CreatedAt:dd/MM/yyyy}").Format.Font.Bold = true;
            row.Format.SpaceBefore = Unit.FromCentimeter(0.5); // เพิ่มระยะห่างระหว่างแถวจากข้างบน

            row = invoiceInfoTable.AddRow();
            row.Cells[0].MergeRight = 1;
            row.Cells[0].AddParagraph($"Invoice No : N85060{order.Id:D4} ").Format.Font.Bold = true;
            row.Format.SpaceAfter = Unit.FromCentimeter(0.5);

            // Create a table for Seller and Customer information
            var infoTable = section.AddTable();
            infoTable.Borders.Visible = false; // ซ่อนเส้นตาราง
            infoTable.AddColumn(Unit.FromCentimeter(10)); // คอลัมน์สำหรับ Seller Information
            infoTable.AddColumn(Unit.FromCentimeter(6)); // คอลัมน์สำหรับ Customer Information

            // Add a row to the table
            var infoRow = infoTable.AddRow();
            
            // หัวข้อ STORE
            var sellerHeader = infoRow.Cells[0].AddParagraph();
            sellerHeader.AddFormattedText("JUSTJEWEL STORE", TextFormat.Bold);
            sellerHeader.Format.SpaceAfter = Unit.FromCentimeter(0.1); // ระยะห่างหลังหัวข้อ
            // Seller Information
            var sellerParagraph = infoRow.Cells[0].AddParagraph();
            sellerParagraph.Format.SpaceAfter = Unit.FromPoint(10); // เพิ่มระยะห่าง pt
            sellerParagraph.AddText("Website : www.justjewel.com\n");
            sellerParagraph.AddText("Email : contact@justjewel.com\n");
            sellerParagraph.AddText("Phone : +66 123 456 789\n");


            // หัวข้อ BILLED TO
            var customerHeader = infoRow.Cells[1].AddParagraph();
            customerHeader.AddFormattedText("BILLED TO", TextFormat.Bold);
            customerHeader.Format.SpaceAfter = Unit.FromCentimeter(0.1); // ระยะห่างหลังหัวข้อ
            // Customer Information
            var customerParagraph = infoRow.Cells[1].AddParagraph();
            customerParagraph.Format.SpaceAfter = Unit.FromPoint(10); // เพิ่มระยะห่าง pt
            customerParagraph.AddText($"Name : {profile.FirstName} {profile.LastName}\n");
            customerParagraph.AddText($"Delivery Address : {order.DeliveryAddress}\n");
            customerParagraph.AddText($"Phone : +66 {profile.PhoneNumber}\n");
            row.Format.SpaceAfter = Unit.FromCentimeter(0.5);

            // Create Table for Product Items
            var table = section.AddTable();
            table.Borders.Width = 0.75; // กำหนดความหนาของเส้นขอบ
            table.Format.Alignment = ParagraphAlignment.Center; // จัดตารางให้อยู่กลาง
            table.AddColumn(Unit.FromCentimeter(1)); // No.
            table.AddColumn(Unit.FromCentimeter(8)); // Product Name
            table.AddColumn(Unit.FromCentimeter(2)); // Quantity
            table.AddColumn(Unit.FromCentimeter(3)); // Unit Price
            table.AddColumn(Unit.FromCentimeter(3)); // Total Price

            // Add Header Row
            var headerRow = table.AddRow();
            headerRow.Shading.Color = Colors.LightGray; // สีพื้นหลังของ Header
            headerRow.Cells[0].AddParagraph("No").Format.Font.Bold = true;
            headerRow.Cells[1].AddParagraph("Items").Format.Font.Bold = true;
            headerRow.Cells[2].AddParagraph("Quantity").Format.Font.Bold = true;
            headerRow.Cells[3].AddParagraph("Unit Price").Format.Font.Bold = true;
            headerRow.Cells[4].AddParagraph("TOTAL").Format.Font.Bold = true;

            // Add Items
            int index = 1;
            foreach (var item in order.Items)
            {
                var itemRow = table.AddRow();
                itemRow.Cells[0].AddParagraph(index.ToString());
                itemRow.Cells[1].AddParagraph(item.Product.Name);
                itemRow.Cells[2].AddParagraph(item.Quantity.ToString());
                itemRow.Cells[3].AddParagraph($"${item.UnitPrice:N2}");
                itemRow.Cells[4].AddParagraph($"${item.Quantity * item.UnitPrice:N2}");
                index++;
            }

            // Add Summary Rows
            var subtotalRow = table.AddRow();
            subtotalRow.Borders.Visible = false; // ซ่อนเส้นขอบ
            subtotalRow.Cells[3].AddParagraph("Subtotal");
            subtotalRow.Cells[3].Format.Alignment = ParagraphAlignment.Center;
            subtotalRow.Cells[3].Shading.Color = Colors.LightGray;
            subtotalRow.Cells[4].AddParagraph($"${order.Items.Sum(i => i.Quantity * i.UnitPrice):N2}");
            subtotalRow.Cells[4].Shading.Color = Colors.LightGray;

            var shippingRow = table.AddRow();
            shippingRow.Borders.Visible = false; // ซ่อนเส้นขอบ
            shippingRow.Cells[3].AddParagraph("Shipping Fee");
            shippingRow.Cells[3].Shading.Color = Colors.LightGray;
            shippingRow.Cells[3].Format.Alignment = ParagraphAlignment.Center;
            shippingRow.Cells[4].AddParagraph($"${order.ShippingFee:N2}");
            shippingRow.Cells[4].Shading.Color = Colors.LightGray;

            // Add Total Row
            var totalRow = table.AddRow();
            totalRow.Borders.Visible = false; // ซ่อนเส้นขอบ
            totalRow.Cells[3].AddParagraph("TOTAL");
            totalRow.Cells[3].Format.Alignment = ParagraphAlignment.Center;
            totalRow.Cells[3].Format.Font.Bold = true;
            totalRow.Cells[3].Shading.Color = Colors.Gray;
            totalRow.Cells[3].Format.Font.Color = Colors.White;
            totalRow.Cells[4].AddParagraph($"${order.Items.Sum(i => i.Quantity * i.UnitPrice) + order.ShippingFee:N2}");
            totalRow.Cells[4].Format.Font.Bold = true;
            totalRow.Cells[4].Shading.Color = Colors.Gray;
            totalRow.Cells[4].Format.Font.Color = Colors.White; // สีตัวอักษรขาวใน Total

            // Add Footer
            var footerParagraph = section.Footers.Primary.AddParagraph();
            footerParagraph.AddText("JUSTJEWEL Store Inc | CMM Street 349 | 678 Bangkok | Thailand");
            footerParagraph.Format.Alignment = ParagraphAlignment.Center;
            footerParagraph.Format.Font.Size = 8;
            footerParagraph.Format.SpaceBefore = Unit.FromCentimeter(0.5);

            // Render PDF
            var renderer = new PdfDocumentRenderer(true) { Document = document };
            renderer.RenderDocument();
            using (var stream = new MemoryStream())
            {
                renderer.PdfDocument.Save(stream);
                return stream.ToArray();
            }
        }

        public async Task<IActionResult> DownloadInvoice(int orderId)
        {   
            // ดึงข้อมูลคำสั่งซื้อจาก orderId
            var order = await context.Orders
                .Include(o => o.Items)
                .ThenInclude(i => i.Product)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null)
                return NotFound("Order not found.");

            // ดึงข้อมูลของลูกค้ามาจาก ClientId โดยใช้ ProfileDto เพื่อเอาข้อมูลที่จำเป็น
            var profile = await context.Users
                .Where(u => u.Id == order.ClientId) // ClientId มาจากคำสั่งซื้อ
                .Select(u => new ProfileDto
                {
                    FirstName = u.FirstName,
                    LastName = u.LastName,
                    Address = u.Address,
                    PhoneNumber = u.PhoneNumber
                })
                .FirstOrDefaultAsync();

            //หากไม่พบข้อมูล Profile ให้แสดง NotFound
            if (profile == null)
                return NotFound("Customer profile not found.");

            //สร้างไฟล์ PDF ด้วยข้อมูลคำสั่งซื้อ
            var pdfData = GenerateInvoicePdf(order, profile);
            // Return File เพื่อส่งให้ผู้ใช้ดาวน์โหลด
            return File(pdfData, "application/pdf", $"Invoice_Order_{orderId}.pdf");
        }

        public async Task<IActionResult> OrderConfirmation()
        {
            // ตรวจสอบว่ามี Session จาก TempData หรือไม่
            if (TempData["Session"] == null)
            {
                ViewBag.ErrorMessage = "Session not found.";
                return RedirectToAction("Index", "Cart");
            }
            
            // Stripe API ในส่วนนี้เพื่อตรวจสอบสถานะการชำระเงิน
            var service = new SessionService();
            Session session = service.Get(TempData["Session"].ToString());

            if (session.PaymentStatus != "paid")
            {
                ViewBag.ErrorMessage = "Payment not completed.";
                return RedirectToAction("Index", "Cart");
            }

            // Retrieve cartItems from Session
            var cartItemsJson = HttpContext.Session.GetString("CartItems");
            // หากไม่มีข้อมูลสินค้า จะสร้าง OrderItem ว่างเปล่าขึ้นมาแทน
            var cartItems = string.IsNullOrEmpty(cartItemsJson)
                ? new List<OrderItem>()
                : JsonConvert.DeserializeObject<List<OrderItem>>(cartItemsJson);

            if (cartItems.Count == 0)
            {
                ViewBag.ErrorMessage = "Your cart is empty.";
                return RedirectToAction("Index", "Cart");
            }

            //บอก Database ว่ามีข้อมูลสินค้านี้แล้วใน Database เพื่อไม่ให้ EF Framework เข้าใจผิดและเพิ่มข้อมูลนี้ไปใหม่ในฐานข้อมูลเอง
            foreach (var item in cartItems)
            {
                context.Products.Attach(item.Product);
            }

            string deliveryAddress = TempData["DeliveryAddress"] as string ?? "";
            string paymentMethod = TempData["PaymentMethod"] as string ?? "";

            if (cartItems.Count == 0 || deliveryAddress.Length == 0 || paymentMethod.Length == 0)
            {
                return RedirectToAction("Index", "Home");
            }

            // บันทึกคำสั่งซื้อในฐานข้อมูล
            var appUser = await userManager.GetUserAsync(User);
            if (appUser == null)
            {
                return RedirectToAction("Index", "Home");
            }

            var order = new Order
            {
                ClientId = appUser.Id,
                Items = cartItems,
                ShippingFee = shippingFee,
                DeliveryAddress = deliveryAddress,
                PaymentMethod = paymentMethod,
                PaymentStatus = "paid",
                PaymentDetails = session.Id,
                OrderStatus = "created",
                CreatedAt = DateTime.Now,
            };

            context.Orders.Add(order);
            await context.SaveChangesAsync();

            if (session.PaymentStatus == "paid")
            {
                // Clear Session and TempData
                HttpContext.Session.Remove("CartItems");
                TempData.Clear();

                // Delete shopping cart cookie
                Response.Cookies.Delete("shopping_cart");
            }

            ViewBag.SuccessMessage = "Order created successfully";
            ViewBag.OrderId = order.Id; // ส่งค่า Order ID ไปยัง View

            return View("Success");
        }

        public IActionResult Success()
		{
            List<OrderItem> cartItems = CartHelper.GetCartItems(Request, Response, context);
            decimal subtotal = CartHelper.GetSubtotal(cartItems);

            ViewBag.CartItems = cartItems;
            ViewBag.ShippingFee = shippingFee;
            ViewBag.Subtotal = subtotal;
            ViewBag.Total = subtotal + shippingFee;

            return View();
		}

        public IActionResult CancelPayment()
        {
            // ดึง CartItems จาก Cookies ก่อน
            List<OrderItem> cartItems = CartHelper.GetCartItems(Request, Response, context);

            if (cartItems == null || !cartItems.Any())
            {
                // ถ้าไม่มีใน Cookies ดึงจาก Session
                var cartItemsJson = HttpContext.Session.GetString("CartItems");
                if (!string.IsNullOrEmpty(cartItemsJson))
                {
                    cartItems = JsonConvert.DeserializeObject<List<OrderItem>>(cartItemsJson);
                }
            }

            decimal subtotal = CartHelper.GetSubtotal(cartItems);

            ViewBag.CartItems = cartItems;
            ViewBag.ShippingFee = shippingFee;
            ViewBag.Subtotal = subtotal;
            ViewBag.Total = subtotal + shippingFee;

            return View();
        }

        public async Task<IActionResult> CheckOut(CheckoutDto model)
		{
            // ดึงสินค้าจาก cartItems cookies
            var cartItems = CartHelper.GetCartItems(Request, Response, context);

            if (cartItems == null || !cartItems.Any())
            {
                ViewBag.ErrorMessage = "Your cart is empty.";
                return RedirectToAction("Index", "Cart");
            }

            // เก็บที่อยู่และวิธีการชำระเงิน
            TempData["DeliveryAddress"] = model.DeliveryAddress;
            TempData["PaymentMethod"] = model.PaymentMethod;

            // Save cartItems to Session เก็ยรายการสินค้าในรูปแบบ JSON เพื่อเรียกใช้ภายหลัง
            HttpContext.Session.SetString("CartItems", JsonConvert.SerializeObject(cartItems));

            var domain = "https://justjewel.azurewebsites.net/";
            var options = new SessionCreateOptions
            {
                SuccessUrl = domain + "Cart/OrderConfirmation",
                CancelUrl = domain + "Cart/CancelPayment",
                LineItems = new List<SessionLineItemOptions>(), //รายการสินค้าในตะกร้าที่จะส่งไปยัง Stripe
                Mode = "payment",
                PaymentMethodTypes = new List<string> { "card" }
            };

            foreach (var item in cartItems)
            {
                var sessionListItem = new SessionLineItemOptions
                {
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        UnitAmount = (long)(item.UnitPrice * 100),
                        Currency = "thb",
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = item.Product.Name,
                        }
                    },
                    Quantity = item.Quantity
                };
                options.LineItems.Add(sessionListItem);
            }

            if (shippingFee > 0)
            {
                var shippingItem = new SessionLineItemOptions
                {
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        UnitAmount = (long)(shippingFee * 100),
                        Currency = "thb",
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = "Shipping Fee",
                        }
                    },
                    Quantity = 1
                };
                options.LineItems.Add(shippingItem);
            }

            var service = new SessionService();
            Session session = service.Create(options);

            TempData["Session"] = session.Id;

            Response.Headers.Add("Location", session.Url); // เพิ่ม Location Header เพื่อไปยังหน้า Stripe
            return new StatusCodeResult(303); // 303 See other เพื่อเปลี่ยนเส้นทางไปยัง URL ที่กำหนด
        }

        public IActionResult Index()
        {
            // ดึง CartItems จาก Cookies ก่อน
            List<OrderItem> cartItems = CartHelper.GetCartItems(Request, Response, context);

            if (cartItems == null || !cartItems.Any())
            {
                ViewBag.ErrorMessage = "Your cart is empty.";
                return View();
            }

            decimal subtotal = CartHelper.GetSubtotal(cartItems);

            // อัปเดต ViewBag สำหรับ Cart Size
            int cartSize = cartItems.Sum(item => item.Quantity); // รวมจำนวนสินค้าทั้งหมด
            ViewBag.CartSize = cartSize;

            ViewBag.CartItems = cartItems;
            ViewBag.ShippingFee = shippingFee;
            ViewBag.Subtotal = subtotal;
            ViewBag.Total = subtotal + shippingFee;

            return View();
        }

        [Authorize]
        [HttpPost]
        public IActionResult Index(CheckoutDto model)
        {
            List<OrderItem> cartItems = CartHelper.GetCartItems(Request, Response, context);
            decimal subtotal = CartHelper.GetSubtotal(cartItems);

            ViewBag.CartItems = cartItems;
            ViewBag.ShippingFee = shippingFee;
            ViewBag.Subtotal = subtotal;
            ViewBag.Total = subtotal + shippingFee;

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Check if shopping cart is empty or not
            if (cartItems.Count == 0)
            {
                ViewBag.ErrorMessage = "Your cart is empty";
                return View(model);
            }

            TempData["DeliveryAddress"] = model.DeliveryAddress;
            TempData["PaymentMethod"] = model.PaymentMethod;

            return RedirectToAction("Confirm");
        }

        public IActionResult Confirm()
        {
            List<OrderItem> cartItems = CartHelper.GetCartItems(Request, Response, context);
            decimal total = CartHelper.GetSubtotal(cartItems) + shippingFee;
            int cartSize = 0;
            foreach (var item in cartItems)
            {
                cartSize += item.Quantity;
            }


            string deliveryAddress = TempData["DeliveryAddress"] as string ?? "";
            string paymentMethod = TempData["PaymentMethod"] as string ?? "";
            TempData.Keep();


            if (cartSize == 0 || deliveryAddress.Length == 0 || paymentMethod.Length == 0)
            {
                return RedirectToAction("Index", "Home");
            }

            ViewBag.DeliveryAddress = deliveryAddress;
            ViewBag.PaymentMethod = paymentMethod;
            ViewBag.Total = total;
            ViewBag.CartSize = cartSize;

            return View();
        }


        [Authorize]
        [HttpPost]
        public async Task<IActionResult> Confirm(int any)
        {
            var cartItems = CartHelper.GetCartItems(Request, Response, context);

            string deliveryAddress = TempData["DeliveryAddress"] as string ?? "";
            string paymentMethod = TempData["PaymentMethod"] as string ?? "";
            TempData.Keep();

            if (cartItems.Count == 0 || deliveryAddress.Length == 0 || paymentMethod.Length == 0)
            {
                return RedirectToAction("Index", "Home");
            }

            var appUser = await userManager.GetUserAsync(User);
            if (appUser == null)
            {
                return RedirectToAction("Index", "Home");
            }

            // save the order
            var order = new Order
            {
                ClientId = appUser.Id,
                Items = cartItems,
                ShippingFee = shippingFee,
                DeliveryAddress = deliveryAddress,
                PaymentMethod = paymentMethod,
                PaymentStatus = "pending",
                PaymentDetails = "",
                OrderStatus = "created",
                CreatedAt = DateTime.Now,
            };

            context.Orders.Add(order);
            context.SaveChanges();


            // delete the shopping cart cookie
            Response.Cookies.Delete("shopping_cart");

            ViewBag.SuccessMessage = "Order created successfully";
            ViewBag.OrderId = order.Id; // ส่งค่า Order ID ไปยัง View

            return View();
        }

    }

}

