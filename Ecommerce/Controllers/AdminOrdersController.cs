﻿using ecommerce.Models;
using ecommerce.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ecommerce.Controllers
{
    [Authorize(Roles = "admin")]
    [Route("/Admin/Orders/{action=Index}/{id?}")]
    public class AdminOrdersController : Controller
    {
        private readonly ApplicationDbContext context;
        private readonly int pageSize = 10;
        private readonly decimal shippingFee;

        public AdminOrdersController(ApplicationDbContext context, IConfiguration configuration)
        {
            this.context = context;
            shippingFee = configuration.GetValue<decimal>("CartSettings:ShippingFee");
        }
        public IActionResult Index(int pageIndex)
        {
            IQueryable<Order> query = context.Orders.Include(o => o.Client)
                .Include(o => o.Items).OrderByDescending(o => o.Id);

            if (pageIndex <= 0)
            {
                pageIndex = 1;
            }

            decimal count = query.Count();
            int totalPages = (int)Math.Ceiling(count / pageSize);
            query = query.Skip((pageIndex - 1) * pageSize).Take(pageSize);

            var orders = query.ToList();

            ViewBag.Orders = orders;
            ViewBag.PageIndex = pageIndex;
            ViewBag.TotalPages = totalPages;

            List<OrderItem> cartItems = CartHelper.GetCartItems(Request, Response, context);
            decimal subtotal = CartHelper.GetSubtotal(cartItems);

            ViewBag.CartItems = cartItems;
            ViewBag.ShippingFee = shippingFee;
            ViewBag.Subtotal = subtotal;
            ViewBag.Total = subtotal + shippingFee;

            return View();
        }

        public IActionResult Details(int id)
        {
            var order = context.Orders.Include(o => o.Client).Include(o => o.Items)
                .ThenInclude(oi => oi.Product).FirstOrDefault(o => o.Id == id);

            if (order == null)
            {
                return RedirectToAction("Index");
            }

            ViewBag.NumOrders = context.Orders.Where(o => o.ClientId == order.ClientId).Count();

            List<OrderItem> cartItems = CartHelper.GetCartItems(Request, Response, context);
            decimal subtotal = CartHelper.GetSubtotal(cartItems);

            ViewBag.CartItems = cartItems;
            ViewBag.ShippingFee = shippingFee;
            ViewBag.Subtotal = subtotal;
            ViewBag.Total = subtotal + shippingFee;

            return View(order);
        }

        public IActionResult Edit(int id, string? payment_status, string? order_status)
        {
            var order = context.Orders.Find(id);
            if (order == null)
            {
                return RedirectToAction("Index");
            }

            if (payment_status == null && order_status == null)
            {
                return RedirectToAction("Details", new { id });
            }

            if (payment_status != null)
            {
                order.PaymentStatus = payment_status;
            }

            if (order_status != null)
            {
                order.OrderStatus = order_status;
            }
            context.SaveChanges();

            List<OrderItem> cartItems = CartHelper.GetCartItems(Request, Response, context);
            decimal subtotal = CartHelper.GetSubtotal(cartItems);

            ViewBag.CartItems = cartItems;
            ViewBag.ShippingFee = shippingFee;
            ViewBag.Subtotal = subtotal;
            ViewBag.Total = subtotal + shippingFee;

            return RedirectToAction("Details", new { id });
        }
    }
}
