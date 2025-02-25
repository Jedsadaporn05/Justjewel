using Microsoft.EntityFrameworkCore;

namespace ecommerce.Models
{
    public class TemporaryOrder
    {
        public int Id { get; set; }  // ID ของคำสั่งซื้อชั่วคราว
        public string UserId { get; set; }  // รหัสผู้ใช้
        public string CartItems { get; set; }  // ข้อมูลตะกร้าสินค้าในรูปแบบ JSON
        public string DeliveryAddress { get; set; }  // ที่อยู่จัดส่ง
        public string PaymentMethod { get; set; }  // วิธีการชำระเงิน
        public DateTime CreatedAt { get; set; }  // เวลาที่สร้างคำสั่งซื้อชั่วคราว
    }
}
