using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace app.Models
{
    public class PostEntity
    {
        [Key]
        [Required]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public long PostId{ get; set; }
        [Required]
        public long UserId{ get; set; }
        public string PostUrl { get; set; } = string.Empty;
        public DateTime PostAt{ get; set; }
        public string PostAbout { get; set; } = string.Empty;
        public uint Likes { get; set; } = 0;
    }
}
