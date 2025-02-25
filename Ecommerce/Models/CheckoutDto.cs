using System.ComponentModel.DataAnnotations;

namespace ecommerce.Models
{
    public class CheckoutDto
    {
        [Required(ErrorMessage = "The Delicery Address is required.")]
        [MaxLength(200)]
        public string DeliveryAddress { get; set; } = "";
        public string PaymentMethod { get; set; } = "";
    }
}
