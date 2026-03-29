using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace Wolfgang.Etl.DbClient.Tests.Unit;

[ExcludeFromCodeCoverage]
[Table("People")]
public class PersonRecord
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public int Id { get; set; }



    [Column("first_name")]
    public string FirstName { get; set; } = string.Empty;



    [Column("last_name")]
    public string LastName { get; set; } = string.Empty;



    [Column("age")]
    public int Age { get; set; }
}
