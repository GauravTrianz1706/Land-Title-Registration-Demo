using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LandTitleRegistration.Models
{
    [Table("TitleRegistrations")]
    public class TitleRegistration
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string TitleRef { get; set; }

        [Required]
        [MaxLength(200)]
        public string OwnerName { get; set; }

        [Required]
        [MaxLength(50)]
        public string ParcelId { get; set; }

        [Required]
        [MaxLength(500)]
        public string PropertyAddress { get; set; }

        [Required]
        [MaxLength(50)]
        public string TitleType { get; set; }

        public DateTimeOffset RegisteredDate { get; set; }
    }
}
