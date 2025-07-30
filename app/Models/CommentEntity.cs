using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace app.Models
{
    public class CommentEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        [Required]
        public long CommentId{ get; set; }
        [Required]
        public long PostId{ get; set; }
        [Required]
        public long UserId{ get; set; }
        public string Content { get; set; } = string.Empty;
        public DateTime CommentAt{ get; set; }
    }
}
