using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace todo_api.Models;

[Table("TodoItems")]
public class TodoItem
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(255)]
    public string Title { get; set; } = string.Empty;

    public string? Body { get; set; }

    public DateTime CreatedAt { get; set; }
}
