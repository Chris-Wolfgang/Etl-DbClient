using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace Wolfgang.Etl.DbClient.Tests.Unit;

// UsedImplicitly: Dapper sets these properties via reflection during column->property
// mapping; ReSharper has no visibility into that code path.
[ExcludeFromCodeCoverage]
[Table("People")]
[UsedImplicitly(ImplicitUseKindFlags.Default, ImplicitUseTargetFlags.WithMembers)]
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
